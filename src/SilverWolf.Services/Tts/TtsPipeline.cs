using SilverWolf.Core.Configuration;
using SilverWolf.Core.Text;
using SilverWolf.Services.Configuration;

namespace SilverWolf.Services.Tts;

/// <summary>
/// Sintesis satu kalimat pada satu waktu, lalu SELURUH hasilnya diputar sebagai
/// satu klip yang hidup di memori.
///
/// <para>
/// <b>Audio tidak lagi menyentuh disk.</b> Sejak 2026-10-09 hasil sintesis
/// dibaca ke <see cref="KlipSuara"/> dan berkas WAV-nya dihapus segera; klip
/// digabung dengan <see cref="GabungWav.GabungkanMemori"/> dan diputar oleh
/// <see cref="PcmPlayer.PutarKlipAsync"/>. Tidak ada lagi
/// <c>sw-gabung-*.wav</c> di <c>%TEMP%</c> dan tidak ada folder cache yang
/// tumbuh terus.
/// </para>
///
/// <para>
/// <b>Kenapa tidak diputar sambil disintesis.</b> Sintesis jauh lebih lambat
/// daripada pemutaran (RVC di CPU: 6–13 dtk per kalimat, audionya 2–3 dtk).
/// Memutar sambil menyintesis membuat pemutar selalu kehabisan bahan dan diam
/// di setiap tanda baca, sehingga ucapan terdengar setengah-setengah. Karena
/// itu seluruh kalimat disintesis dulu, digabung menjadi satu WAV, baru
/// diputar — konsekuensinya waktu tunggu sebelum suara pertama lebih panjang,
/// dan itu disengaja.
/// </para>
///
/// Setiap ucapan dilacak sejak persiapan sampai pemutaran terakhir selesai.
/// </summary>
public sealed class TtsPipeline : IDisposable
{
    private readonly TtsWorker? _pekerja;
    private readonly PcmPlayer? _pemutar;
    private readonly Func<bool> _siap;

    /// <summary>Satu kalimat → satu klip WAV di memori (bukan jalur berkas).</summary>
    private readonly Func<string, CancellationToken, Task<KlipSuara?>> _hasilkan;

    private readonly Func<KlipSuara, CancellationToken, Action, Task<bool>> _putar;
    private readonly Action _buangPemutar;
    private readonly Action<string>? _log;
    private readonly SuaraArsip? _arsip;
    private readonly object _kunci = new();
    private readonly SemaphoreSlim _mulai = new(1, 1);
    private CancellationTokenSource? _cts;
    private Task _tugas = Task.CompletedTask;
    private bool _dibuang;

    public TtsPipeline(AppConfig konfig, Action<string>? log = null)
    {
        _log = log;
        _pekerja = new TtsWorker(konfig, log);
        _pemutar = new PcmPlayer(log);
        // Arsip hanya dibuat kalau VTUBER_TTS_ARSI=ya. Bawaannya MATI sejak
        // 2026-10-09: Master meminta audio hasil sintesis tidak menumpuk di
        // disk, dan arsip ini tidak dibuang saat aplikasi ditutup.
        _arsip = konfig.TtsArsip
            ? new SuaraArsip(AppPaths.Suara(konfig.Akar), SuaraArsip.MaksBawaan, log)
            : null;
        _siap = () => _pekerja.Siap;
        _hasilkan = _pekerja.HasilkanKlipAsync;
        _putar = (klip, ct, mulai) => _pemutar.PutarKlipAsync(klip, 1.0f, ct, mulai);
        _buangPemutar = () =>
        {
            // Pekerja Python menetap menahan torch + model di memori. Melepas
            // pemutar audio saja tidak cukup: proses Python akan terus hidup
            // sebagai proses yatim dan menekan RAM — bahan bakar Mode B.
            _pekerja.Matikan();
            _pemutar.Dispose();
            // Byte audio tidak lagi diperlukan sesudah jendela ditutup.
            _pekerja.BersihkanCache();
        };
    }

    // Titik injeksi untuk uji antrean tanpa Python, perangkat audio, atau model.
    internal TtsPipeline(
        Func<string, CancellationToken, Task<KlipSuara?>> hasilkan,
        Func<KlipSuara, CancellationToken, Action, Task<bool>> putar,
        Action? buangPemutar = null,
        Action<string>? log = null,
        SuaraArsip? arsip = null)
    {
        _siap = () => true;
        _hasilkan = hasilkan;
        _putar = putar;
        _buangPemutar = buangPemutar ?? (() => { });
        _log = log;
        _arsip = arsip;
    }

    /// <summary>Sinyal aktivitas pemutaran untuk LipSync (belum RMS nyata).</summary>
    public event Action<float>? LevelBerubah
    {
        add { if (_pemutar is not null) _pemutar.LevelBerubah += value; }
        remove { if (_pemutar is not null) _pemutar.LevelBerubah -= value; }
    }

    public bool Sibuk => !Penyelesaian.IsCompleted;

    /// <summary>Snapshot tugas ucapan saat ini, termasuk persiapan Python.</summary>
    public Task Penyelesaian
    {
        get { lock (_kunci) return _tugas; }
    }

    public string? AlasanTidakSiap() => _pekerja?.AlasanTidakSiap();

    public async Task UcapkanAsync(string teks, CancellationToken ct = default)
    {
        var ucapan = await MulaiAsync(teks, ct, null).ConfigureAwait(false);
        await ucapan.Selesai.ConfigureAwait(false);
    }

    /// <summary>
    /// Kembali setelah perangkat menerima Play() untuk WAV yang bisa diputar.
    /// Ini bukan bukti bahwa speaker pengguna terdengar. Kembali false bila tidak
    /// ada audio yang dapat dimulai; pembatalan tetap dilempar.
    ///
    /// <para>
    /// <b>Perubahan 2026-10-09:</b> SELURUH kalimat disintesis lebih dulu, baru
    /// diputar sebagai satu berkas. Sebelumnya kalimat pertama diputar sambil
    /// kalimat berikutnya disintesis — dan karena sintesis (6–13 dtk) jauh lebih
    /// lambat daripada pemutaran (2–3 dtk), pemutar selalu kehabisan bahan dan
    /// berhenti di setiap tanda baca. Konsekuensinya waktu tunggu sebelum suara
    /// pertama memang lebih panjang; itu ditukar dengan ucapan yang mengalir
    /// tanpa jeda.
    /// </para>
    /// </summary>
    /// <param name="progres">
    /// Dipanggil setiap satu kalimat selesai disintesis: (selesai, total).
    /// Dipakai UI untuk menunjukkan kemajuan, bukan indikator waktu.
    /// </param>
    public async Task<bool> SiapkanDanPutarAsync(
        string teks, CancellationToken ct = default, Action<int, int>? progres = null)
    {
        var ucapan = await MulaiAsync(teks, ct, progres).ConfigureAwait(false);
        return await ucapan.Siap.ConfigureAwait(false);
    }

    private async Task<(Task<bool> Siap, Task Selesai)> MulaiAsync(
        string teks, CancellationToken ct, Action<int, int>? progres)
    {
        ct.ThrowIfCancellationRequested();
        await _mulai.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            Hentikan();
            // Jangan mulai Python atau pemutaran baru sebelum yang lama berhenti.
            await Penyelesaian.WaitAsync(ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();

            lock (_kunci)
            {
                ObjectDisposedException.ThrowIf(_dibuang, this);
                if (!_siap() || string.IsNullOrWhiteSpace(teks))
                    return (Task.FromResult(false), Task.CompletedTask);

                var lokal = CancellationTokenSource.CreateLinkedTokenSource(ct);
                var siap = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                _cts = lokal;
                // Publikasikan tugas sebelum pekerjaan mulai; cache hit pun dilacak.
                _tugas = Task.Run(() => JalankanAsync(teks, lokal, siap, progres));
                return (siap.Task, _tugas);
            }
        }
        finally
        {
            _mulai.Release();
        }
    }

    private async Task JalankanAsync(
        string teks, CancellationTokenSource lokal, TaskCompletionSource<bool> siap,
        Action<int, int>? progres)
    {
        var ct = lokal.Token;
        using var daftar = ct.Register(() => siap.TrySetCanceled(ct));
        try
        {
            var potong = SentenceSplitter.PotongKalimat(teks);
            if (!string.IsNullOrWhiteSpace(potong.Sisa)) potong.Kalimat.Add(potong.Sisa);

            // ── Seluruh kalimat disintesis DULU, baru diputar ────────────────
            // Sintesis jauh lebih lambat daripada pemutaran (RVC di CPU 6–13 dtk
            // per kalimat, audionya hanya 2–3 dtk). Memutar sambil menyintesis
            // membuat pemutar selalu kehabisan bahan dan berhenti di setiap tanda
            // baca — itulah "dia ngomong setengah-setengah" yang dikeluhkan
            // Master. Menyintesis semuanya dulu menghapus jeda itu.
            var klips = new List<KlipSuara>();
            var total = potong.Kalimat.Count;
            var selesai = 0;
            foreach (var kalimat in potong.Kalimat)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var klip = await _hasilkan(kalimat, ct).ConfigureAwait(false);
                    ct.ThrowIfCancellationRequested();
                    if (klip is not null)
                    {
                        klips.Add(klip);
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception galat)
                {
                    _log?.Invoke($"[tts] kalimat gagal disiapkan: {galat.Message}");
                }

                selesai++;
                LaporProgres(progres, selesai, total);
            }

            if (klips.Count == 0)
            {
                // Tidak ada satu pun klip: biarkan finally menandai tidak siap.
                return;
            }

            // Gabung menjadi satu klip, SELURUHNYA di memori — tidak ada lagi
            // berkas perantara sw-gabung-*.wav di %TEMP%. Kalau formatnya
            // ternyata tidak seragam, pemanggil jatuh ke pemutaran berurutan —
            // lebih baik ada jeda daripada tidak ada suara sama sekali.
            var gabung = klips.Count > 1
                ? GabungWav.GabungkanMemori(klips.Select(k => k.Data).ToList())
                : null;

            if (klips.Count > 1 && gabung is null)
            {
                _log?.Invoke($"[tts] {klips.Count} klip tidak bisa digabung; diputar berurutan");
            }
            else if (gabung is not null)
            {
                _log?.Invoke($"[tts] {klips.Count} kalimat digabung di memori");
            }

            // Arsipkan balasan utuh (hanya kalau VTUBER_TTS_ARSI=ya). Kegagalan
            // penggabungan dilewati: mengarsipkan sebagian balasan lebih
            // menyesatkan daripada tidak mengarsipkan sama sekali.
            if (gabung is not null && _arsip is not null)
            {
                _arsip.Simpan(new KlipSuara(gabung, "balasan"));
            }

            var daftarPutar = gabung is not null
                ? new List<KlipSuara> { new(gabung, "balasan") }
                : klips;

            foreach (var klip in daftarPutar)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var berhasil = await _putar(klip, ct, () =>
                    {
                        if (!ct.IsCancellationRequested) siap.TrySetResult(true);
                    }).ConfigureAwait(false);
                    if (!berhasil && !ct.IsCancellationRequested)
                        _log?.Invoke("[tts] satu klip gagal diputar; lanjut berikutnya");
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception galat)
                {
                    _log?.Invoke($"[tts] klip gagal diputar: {galat.Message}");
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            siap.TrySetCanceled(ct);
        }
        catch (Exception galat)
        {
            _log?.Invoke($"[tts] pipeline gagal: {galat.Message}");
        }
        finally
        {
            if (ct.IsCancellationRequested) siap.TrySetCanceled(ct);
            else siap.TrySetResult(false);
            lock (_kunci)
            {
                if (ReferenceEquals(_cts, lokal)) _cts = null;
            }
            // Registrasi dan CTS baru dibuang setelah seluruh pekerjaan selesai.
            daftar.Dispose();
            lokal.Dispose();
        }
    }

    /// <summary>
    /// Laporkan kemajuan tanpa membiarkan galat UI menggagalkan sintesis.
    /// Pemanggil bertanggung jawab memindahkannya ke utas UI.
    /// </summary>
    private void LaporProgres(Action<int, int>? progres, int selesai, int total)
    {
        if (progres is null)
        {
            return;
        }

        try
        {
            progres(selesai, total);
        }
        catch (Exception galat)
        {
            _log?.Invoke($"[tts] laporan kemajuan gagal: {galat.Message}");
        }
    }

    public void Hentikan()
    {
        lock (_kunci)
        {
            // Jangan Dispose CTS saat Python/pemutaran masih memakai tokennya.
            _cts?.Cancel();
        }
    }

    public async Task TungguSelesaiAsync(TimeSpan batas)
    {
        try { await Penyelesaian.WaitAsync(batas).ConfigureAwait(false); }
        catch (TimeoutException) { _log?.Invoke("[tts] pembongkaran masih menunggu pekerja"); }
    }

    public void Dispose()
    {
        Task tugas;
        lock (_kunci)
        {
            if (_dibuang) return;
            _dibuang = true;
            _cts?.Cancel();
            tugas = _tugas;
        }
        if (tugas.IsCompleted) _buangPemutar();
        else _ = tugas.ContinueWith(_ => _buangPemutar(), CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }
}
