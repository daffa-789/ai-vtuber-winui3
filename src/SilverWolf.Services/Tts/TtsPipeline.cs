using SilverWolf.Core.Configuration;
using SilverWolf.Core.Text;

namespace SilverWolf.Services.Tts;

/// <summary>
/// Sintesis satu kalimat pada satu waktu, lalu SELURUH hasilnya diputar sebagai
/// satu berkas.
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
    private readonly Func<string, CancellationToken, Task<string?>> _hasilkan;
    private readonly Func<string, CancellationToken, Action, Task<bool>> _putar;
    private readonly Action _buangPemutar;
    private readonly Action<string>? _log;
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
        _siap = () => _pekerja.Siap;
        _hasilkan = _pekerja.HasilkanSatuAsync;
        _putar = (wav, ct, mulai) => _pemutar.PutarAsync(wav, 1.0f, ct, mulai);
        _buangPemutar = () =>
        {
            // Pekerja Python menetap menahan torch + model di memori. Melepas
            // pemutar audio saja tidak cukup: proses Python akan terus hidup
            // sebagai proses yatim dan menekan RAM — bahan bakar Mode B.
            _pekerja.Matikan();
            _pemutar.Dispose();
        };
    }

    // Titik injeksi untuk uji antrean tanpa Python, perangkat audio, atau model.
    internal TtsPipeline(
        Func<string, CancellationToken, Task<string?>> hasilkan,
        Func<string, CancellationToken, Action, Task<bool>> putar,
        Action? buangPemutar = null,
        Action<string>? log = null)
    {
        _siap = () => true;
        _hasilkan = hasilkan;
        _putar = putar;
        _buangPemutar = buangPemutar ?? (() => { });
        _log = log;
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
            var wavs = new List<string>();
            var total = potong.Kalimat.Count;
            var selesai = 0;
            foreach (var kalimat in potong.Kalimat)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var wav = await _hasilkan(kalimat, ct).ConfigureAwait(false);
                    ct.ThrowIfCancellationRequested();
                    if (wav is not null)
                    {
                        wavs.Add(wav);
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

            if (wavs.Count == 0)
            {
                // Tidak ada satu pun WAV: biarkan finally menandai tidak siap.
                return;
            }

            // Gabung menjadi satu berkas. Kalau formatnya ternyata tidak seragam,
            // pemanggil jatuh ke pemutaran berurutan — lebih baik ada jeda
            // daripada tidak ada suara sama sekali.
            var gabung = GabungWav.Gabungkan(wavs, Path.GetTempPath());
            if (gabung is null)
            {
                _log?.Invoke($"[tts] {wavs.Count} WAV tidak bisa digabung; diputar berurutan");
            }
            else if (wavs.Count > 1)
            {
                _log?.Invoke($"[tts] {wavs.Count} kalimat digabung menjadi satu berkas");
            }

            try
            {
                var berkasPutar = gabung is null ? wavs : new List<string> { gabung };
                foreach (var wav in berkasPutar)
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        var berhasil = await _putar(wav, ct, () =>
                        {
                            if (!ct.IsCancellationRequested) siap.TrySetResult(true);
                        }).ConfigureAwait(false);
                        if (!berhasil && !ct.IsCancellationRequested)
                            _log?.Invoke("[tts] satu berkas gagal diputar; lanjut berikutnya");
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception galat)
                    {
                        _log?.Invoke($"[tts] berkas gagal diputar: {galat.Message}");
                    }
                }
            }
            finally
            {
                // Berkas gabungan hanya perantara; WAV aslinya tetap di cache.
                if (gabung is not null)
                {
                    try { File.Delete(gabung); } catch (IOException) { }
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
