using NAudio.Wave;

namespace SilverWolf.Services.Tts;

/// <summary>
/// Pemutar WAV berurutan memakai NAudio.
///
/// <para>
/// <b>Kenapa NAudio dan bukan MediaPlayer WinUI:</b> berkas keluaran RVC adalah
/// WAV mono 40.000 Hz. <c>WaveOutEvent</c> memutarnya apa adanya tanpa
/// transcoding, tunduk pada latensi kecil, dan — yang paling penting —
/// memberi kejadian <see cref="PlaybackStopped"/> yang akurat sehingga antrean
/// kalimat tidak saling menimpa.
/// </para>
///
/// <para>
/// Satu berkas diputar pada satu waktu. Perangkat dibuka per WAV untuk
/// menangani format keluaran yang dapat berbeda; semaphore menjaga urutan.
/// </para>
/// </summary>
public sealed class PcmPlayer : IDisposable
{
    /// <summary>Berapa kali satu berkas dicoba diputar sebelum menyerah.</summary>
    private const int MaksPercobaan = 2;

    private readonly Action<string>? _log;
    private readonly SemaphoreSlim _kunci = new(1, 1);
    private WaveOutEvent? _keluaran;
    private AudioFileReader? _pembaca;
    private bool _dibuang;

    /// <summary>
    /// Penantian <c>PlaybackStopped</c> dari berkas terakhir. Dipakai supaya
    /// perangkat tidak dibongkar selagi utas audio masih berjalan.
    /// </summary>
    private Task _berhentiTerakhir = Task.CompletedTask;

    public PcmPlayer(Action<string>? log = null)
    {
        _log = log;
    }

    /// <summary>Kejadian saat volume keluaran berubah, untuk menggerakkan mulut.</summary>
    public event Action<float>? LevelBerubah;

    /// <summary>
    /// Putar satu berkas WAV dan tunggu sampai selesai. Mengembalikan
    /// <c>true</c> kalau benar-benar diputar sampai habis.
    ///
    /// <para>
    /// Selama pemutaran, <see cref="LevelBerubah"/> dipanggil berkala dengan
    /// aktivitas 0..1 untuk LipSync. Sinyal ini masih perkiraan sintetis,
    /// bukan pengukuran RMS sampel audio.
    /// </para>
    /// </summary>
    public async Task<bool> PutarAsync(string jalur, float kecepatan = 1.0f,
        CancellationToken ct = default, Action? saatMulai = null)
    {
        if (_dibuang || !File.Exists(jalur))
        {
            _log?.Invoke($"[tts] berkas tidak ada atau pemutar sudah dibuang: {jalur}");
            return false;
        }

        await _kunci.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Stop() TIDAK menunggu utas audio. Membongkar WaveOutEvent sebelum
            // PlaybackStopped benar-benar tiba pernah membuat Play() berikutnya
            // diam saja — tanpa galat, tanpa bunyi. Inilah salah satu sumber
            // "kadang suaranya tidak keluar". Tunggu dulu, baru bongkar.
            await TungguBerhentiAsync().ConfigureAwait(false);
            Bersihkan();

            // Perangkat audio sesekali menolak dibuka tepat sesudah dipakai,
            // terutama menyusul Hentikan() dari balasan baru yang memotong
            // kalimat sebelumnya. Satu ulangan cukup dan murah.
            for (var percobaan = 1; percobaan <= MaksPercobaan; percobaan++)
            {
                ct.ThrowIfCancellationRequested();

                var hasil = await PutarSekaliAsync(jalur, percobaan, ct, saatMulai)
                    .ConfigureAwait(false);
                if (hasil)
                {
                    return true;
                }

                if (percobaan < MaksPercobaan)
                {
                    _log?.Invoke($"[tts] percobaan putar ke-{percobaan} gagal; ulangi setelah 300 ms");
                    Bersihkan();
                    await Task.Delay(300, ct).ConfigureAwait(false);
                }
                else
                {
                    _log?.Invoke($"[tts] percobaan putar ke-{percobaan} gagal; menyerah pada berkas ini");
                }
            }

            return false;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception galat)
        {
            _log?.Invoke($"[tts] gagal memutar: {galat.Message}");
            return false;
        }
        finally
        {
            Bersihkan();
            _kunci.Release();
        }
    }

    /// <summary>Satu percobaan buka-perangkat lalu putar sampai habis.</summary>
    private async Task<bool> PutarSekaliAsync(
        string jalur, int percobaan, CancellationToken ct, Action? saatMulai)
    {
        _pembaca = new AudioFileReader(jalur) { Volume = 1.0f };
        _keluaran = new WaveOutEvent
        {
            // 150 ms: cukup untuk mencegah putus di mesin sibuk, tetapi
            // tidak menambah latensi yang terasa.
            DesiredLatency = 150,
            NumberOfBuffers = 3,
        };

        _keluaran.Init(_pembaca);

        _log?.Invoke($"[tts] putar #{percobaan}: {Path.GetFileName(jalur)} "
            + $"{_pembaca.WaveFormat.SampleRate} Hz, {_pembaca.WaveFormat.Channels} kanal, "
            + $"{_pembaca.TotalTime.TotalSeconds:F2} dtk, perangkat={HitungPerangkat()})");

        var selesai = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _berhentiTerakhir = selesai.Task;

        _keluaran.PlaybackStopped += (_, e) =>
        {
            if (e.Exception is not null)
            {
                _log?.Invoke($"[tts] perangkat audio berhenti karena galat: {e.Exception.Message}");
            }

            selesai.TrySetResult(e.Exception is null);
        };

        var keluaran = _keluaran;
        var kunciPutar = new object();
        using var daftar = ct.Register(() =>
        {
            lock (kunciPutar)
            {
                // Stop() tidak menunggu utas audio. Tunggu PlaybackStopped,
                // jangan selesaikan task/bongkar buffer lebih awal.
                keluaran.Stop();
            }
        });

        lock (kunciPutar)
        {
            ct.ThrowIfCancellationRequested();
            keluaran.Play();
            saatMulai?.Invoke();
        }

        // Selama diputar, laporkan level sesekali untuk LipSync.
        var tugasLevel = LaporkanLevelAsync(_pembaca, selesai.Task, ct);

        var berhasil = await selesai.Task.ConfigureAwait(false);
        await tugasLevel.ConfigureAwait(false);

        if (!berhasil)
        {
            _log?.Invoke($"[tts] pemutaran berhenti tidak wajar pada percobaan ke-{percobaan}");
        }

        return berhasil && !ct.IsCancellationRequested;
    }

    /// <summary>
    /// Tunggu <c>PlaybackStopped</c> terakhir, paling lama 2 dtk. Melewati batas
    /// lebih baik daripada menggantung seluruh antrean suara.
    /// </summary>
    private async Task TungguBerhentiAsync()
    {
        var tunggu = _berhentiTerakhir;
        if (tunggu.IsCompleted)
        {
            return;
        }

        try
        {
            await tunggu.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            _log?.Invoke("[tts] perangkat tidak melapor berhenti dalam 2 dtk; lanjutkan");
        }
        catch (Exception galat)
        {
            _log?.Invoke($"[tts] penantian berhenti gagal: {galat.Message}");
        }
    }

    private static string HitungPerangkat()
    {
        try
        {
            var jumlah = WaveOut.DeviceCount;
            if (jumlah <= 0)
            {
                return "tidak ada perangkat WaveOut";
            }

            var kemampuan = WaveOut.GetCapabilities(0);
            return $"0/{jumlah - 1} '{kemampuan.ProductName}'";
        }
        catch (Exception galat)
        {
            return $"tidak diketahui ({galat.Message})";
        }
    }

    private async Task LaporkanLevelAsync(
        AudioFileReader pembaca, Task sampai, CancellationToken ct)
    {
        // 60 Hz cukup untuk mulut bergerak mulus pada 30 fps render.
        var jeda = TimeSpan.FromMilliseconds(16);

        while (!sampai.IsCompleted && !ct.IsCancellationRequested)
        {
            try
            {
                // Perkiraan aktivitas; jangan membaca/mencuri sampel pemutar.
                var level = HitungLevel(pembaca);
                LevelBerubah?.Invoke(level);
            }
            catch
            {
                break;
            }

            try
            {
                await Task.Delay(jeda, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        LevelBerubah?.Invoke(0f);
    }

    /// <summary>
    /// Perkiraan amplitudo tanpa memindahkan posisi baca.
    ///
    /// <para>
    /// <b>Jangan memanggil Read() di sini.</b> Membaca dari
    /// <see cref="AudioFileReader"/> saat sedang diputar akan mencuri sampel
    /// dari pemutaran — suara jadi putus-putus. Karena itu kita hanya memakai
    /// <c>CurrentTime</c> relatif terhadap durasi sebagai perkiraan aktivitas,
    /// lalu memperhalusnya supaya mulut tidak berkedip.
    /// </para>
    /// </summary>
    private static float HitungLevel(AudioFileReader pembaca)
    {
        if (pembaca.TotalTime.TotalSeconds <= 0)
        {
            return 0f;
        }

        // Aktivitas = gelombang sinus lembut sepanjang durasi; cukup untuk
        // mulut yang terlihat hidup tanpa artefak.
        var t = pembaca.CurrentTime.TotalSeconds;
        var dasar = 0.55f + (0.45f * (float)Math.Abs(Math.Sin(t * 8.0)));
        return Math.Clamp(dasar, 0f, 1f);
    }

    private void Bersihkan()
    {
        try
        {
            _keluaran?.Stop();
        }
        catch
        {
            // Diabaikan.
        }

        _keluaran?.Dispose();
        _keluaran = null;
        _pembaca?.Dispose();
        _pembaca = null;
    }

    public void Dispose()
    {
        if (_dibuang)
        {
            return;
        }

        _dibuang = true;
        Bersihkan();
        _kunci.Dispose();
    }
}
