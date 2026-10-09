using System.Threading.Channels;
using SilverWolf.Core.Configuration;
using SilverWolf.Core.Text;

namespace SilverWolf.Services.Tts;

/// <summary>
/// Sintesis satu kalimat pada satu waktu, dengan antrean WAV terbatas.
/// Pemutaran kalimat pertama dapat bertumpang tindih dengan sintesis berikutnya.
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
        var ucapan = await MulaiAsync(teks, ct).ConfigureAwait(false);
        await ucapan.Selesai.ConfigureAwait(false);
    }

    /// <summary>
    /// Kembali setelah perangkat menerima Play() untuk WAV pertama yang bisa
    /// diputar. Ini bukan bukti bahwa speaker pengguna terdengar. Kembali false
    /// bila tidak ada audio yang dapat dimulai; pembatalan tetap dilempar.
    /// </summary>
    public async Task<bool> SiapkanDanPutarAsync(string teks, CancellationToken ct = default)
    {
        var ucapan = await MulaiAsync(teks, ct).ConfigureAwait(false);
        return await ucapan.Siap.ConfigureAwait(false);
    }

    private async Task<(Task<bool> Siap, Task Selesai)> MulaiAsync(
        string teks, CancellationToken ct)
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
                _tugas = Task.Run(() => JalankanAsync(teks, lokal, siap));
                return (siap.Task, _tugas);
            }
        }
        finally
        {
            _mulai.Release();
        }
    }

    private async Task JalankanAsync(
        string teks, CancellationTokenSource lokal, TaskCompletionSource<bool> siap)
    {
        var ct = lokal.Token;
        // Maksimal satu WAV menunggu; tidak menumpuk seluruh balasan di memori.
        var antrean = Channel.CreateBounded<string>(new BoundedChannelOptions(1)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait,
        });
        using var daftar = ct.Register(() => siap.TrySetCanceled(ct));
        try
        {
            await Task.WhenAll(
                HasilkanAsync(teks, antrean.Writer, ct),
                PutarAsync(antrean.Reader, siap, ct)).ConfigureAwait(false);
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
            // Registrasi dan CTS baru dibuang setelah produsen/konsumen selesai.
            daftar.Dispose();
            lokal.Dispose();
        }
    }

    private async Task HasilkanAsync(
        string teks, ChannelWriter<string> antrean, CancellationToken ct)
    {
        try
        {
            var potong = SentenceSplitter.PotongKalimat(teks);
            if (!string.IsNullOrWhiteSpace(potong.Sisa)) potong.Kalimat.Add(potong.Sisa);
            foreach (var kalimat in potong.Kalimat)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var wav = await _hasilkan(kalimat, ct).ConfigureAwait(false);
                    ct.ThrowIfCancellationRequested();
                    if (wav is not null)
                        await antrean.WriteAsync(wav, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception galat)
                {
                    _log?.Invoke($"[tts] kalimat gagal disiapkan: {galat.Message}");
                }
            }
        }
        finally
        {
            antrean.TryComplete();
        }
    }

    private async Task PutarAsync(
        ChannelReader<string> antrean, TaskCompletionSource<bool> siap, CancellationToken ct)
    {
        await foreach (var wav in antrean.ReadAllAsync(ct).ConfigureAwait(false))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var berhasil = await _putar(wav, ct, () =>
                {
                    if (!ct.IsCancellationRequested) siap.TrySetResult(true);
                }).ConfigureAwait(false);
                if (!berhasil && !ct.IsCancellationRequested)
                    _log?.Invoke("[tts] satu kalimat gagal diputar; lanjut kalimat berikutnya");
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception galat)
            {
                _log?.Invoke($"[tts] kalimat gagal diputar: {galat.Message}");
            }
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
