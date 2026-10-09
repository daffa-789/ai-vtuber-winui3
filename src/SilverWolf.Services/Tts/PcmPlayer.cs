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

    /// <summary>
    /// Sumber audio yang sedang diputar. Sengaja <see cref="WaveStream"/> agar
    /// dua jalur bisa dipakai bergantian: <see cref="AudioFileReader"/> untuk
    /// berkas di disk, <see cref="WaveFileReader"/> atas <see cref="MemoryStream"/>
    /// untuk klip yang hidup di memori (lihat <see cref="PutarKlipAsync"/>).
    /// </summary>
    private WaveStream? _pembaca;
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

    /// <summary>
    /// Amplitudo audio 0..1 pada posisi pemutaran saat ini, untuk menggerakkan
    /// mulut (LipSync).
    ///
    /// <para>
    /// <b>Ini RMS nyata, bukan perkiraan.</b> Nilainya dihitung sekali di muka
    /// dari isi berkas WAV, lalu dibaca mengikuti <c>CurrentTime</c> pemutar.
    /// Versi lama memakai gelombang sinus dari waktu berjalan — mulutnya
    /// bergerak, tetapi tidak ada hubungannya dengan suara yang keluar.
    /// </para>
    /// </summary>
    public event Action<float>? LevelBerubah;

    /// <summary>Berapa kali per detik amplitudo dilaporkan.</summary>
    private const int LajuBingkai = 60;

    /// <summary>RMS per bingkai untuk berkas yang sedang diputar.</summary>
    private float[]? _bingkaiLevel;

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

        return await PutarBersama(
            () => new AudioFileReader(jalur) { Volume = 1.0f },
            () => AnalisisAmplitudo(jalur),
            Path.GetFileName(jalur),
            ct, saatMulai).ConfigureAwait(false);
    }

    /// <summary>
    /// Putar WAV yang sudah ada di memori dan tunggu sampai selesai.
    ///
    /// <para>
    /// Inilah jalur utama sejak 2026-10-09: hasil sintesis tidak lagi ditulis
    /// ke <c>%TEMP%</c> untuk diputar — <see cref="KlipSuara"/> dibaca langsung
    /// lewat <see cref="MemoryStream"/>, sehingga tidak ada berkas yang
    /// tertinggal sesudahnya.
    /// </para>
    /// </summary>
    public async Task<bool> PutarKlipAsync(KlipSuara klip, float kecepatan = 1.0f,
        CancellationToken ct = default, Action? saatMulai = null)
    {
        if (_dibuang || klip is null || klip.Data.Length == 0)
        {
            _log?.Invoke("[tts] klip kosong atau pemutar sudah dibuang");
            return false;
        }

        var data = klip.Data;
        return await PutarBersama(
            () => new WaveFileReader(new MemoryStream(data, writable: false)),
            () => AnalisisAmplitudo(new MemoryStream(data, writable: false)),
            klip.Label,
            ct, saatMulai).ConfigureAwait(false);
    }

    /// <summary>Rangka pemutaran yang sama untuk berkas maupun klip memori.</summary>
    private async Task<bool> PutarBersama(
        Func<WaveStream> buatSumber, Func<float[]> hitungLevel, string label,
        CancellationToken ct, Action? saatMulai)
    {
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

                var hasil = await PutarSekaliAsync(buatSumber, hitungLevel, label, percobaan, ct, saatMulai)
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
                    _log?.Invoke($"[tts] percobaan putar ke-{percobaan} gagal; menyerah pada \"{label}\"");
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
        Func<WaveStream> buatSumber, Func<float[]> hitungLevel, string label,
        int percobaan, CancellationToken ct, Action? saatMulai)
    {
        // Dihitung sekali di muka, sebelum perangkat dibuka. Hasilnya dipakai
        // sepanjang pemutaran dengan membaca CurrentTime.
        _bingkaiLevel = hitungLevel();

        _pembaca = buatSumber();
        _keluaran = new WaveOutEvent
        {
            // 150 ms: cukup untuk mencegah putus di mesin sibuk, tetapi
            // tidak menambah latensi yang terasa.
            DesiredLatency = 150,
            NumberOfBuffers = 3,
        };

        _keluaran.Init(_pembaca);

        _log?.Invoke($"[tts] putar #{percobaan}: {label} "
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
        WaveStream pembaca, Task sampai, CancellationToken ct)
    {
        // 60 Hz cukup untuk mulut bergerak mulus pada 30 fps render.
        var jeda = TimeSpan.FromMilliseconds(16);

        while (!sampai.IsCompleted && !ct.IsCancellationRequested)
        {
            try
            {
                // Jangan membaca dari pembaca yang sedang diputar — itu mencuri
                // sampel dan membuat suaranya putus. Yang dibaca hanya posisi
                // waktu, lalu diambil bingkai RMS yang sudah dihitung di muka.
                LevelBerubah?.Invoke(AmbilLevel(pembaca));
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

    /// <summary>Ambil bingkai RMS pada posisi pemutaran saat ini.</summary>
    private float AmbilLevel(WaveStream pembaca)
    {
        var bingkai = _bingkaiLevel;
        if (bingkai is null || bingkai.Length == 0)
        {
            return 0f;
        }

        var i = (int)(pembaca.CurrentTime.TotalSeconds * LajuBingkai);
        if (i < 0) i = 0;
        if (i >= bingkai.Length) i = bingkai.Length - 1;
        return bingkai[i];
    }

    /// <summary>
    /// Hitung RMS per bingkai dari isi berkas WAV, sekali di muka.
    ///
    /// <para>
    /// <b>Kenapa di muka.</b> Membaca dari <see cref="AudioFileReader"/> yang
    /// sedang diputar akan mencuri sampel dan membuat suaranya putus. Karena itu
    /// berkasnya dibaca lewat pembaca TERPISAH sebelum pemutaran dimulai; cara
    /// ini memberi amplitudo yang benar tanpa mengganggu apa pun.
    /// </para>
    ///
    /// <para>
    /// Hasilnya dinormalkan ke puncak tertinggi = 1, lalu diambil akar
    /// kuadratnya supaya bagian yang pelan tetap terlihat di mulut. Perhatikan
    /// bahwa puncaknya dijadikan acuan, jadi balasan yang memang direkam pelan
    /// tetap membuka mulut penuh — itu disengaja untuk model ini.
    /// </para>
    /// </summary>
    internal static float[] AnalisisAmplitudo(string jalur)
    {
        try
        {
            using var baca = new AudioFileReader(jalur);
            return HitungBingkai(baca);
        }
        catch (Exception)
        {
            // Berkas tidak terbaca: mulut cukup diam, suaranya tetap diputar.
            return Array.Empty<float>();
        }
    }

    /// <summary>
    /// Varian untuk WAV yang hidup di memori. Sengaja memakai
    /// <see cref="WaveFileReader"/> (bukan <see cref="AudioFileReader"/>) karena
    /// itu yang bisa dibuka dari <see cref="Stream"/>.
    /// </summary>
    internal static float[] AnalisisAmplitudo(Stream aliran)
    {
        try
        {
            using var baca = new WaveFileReader(aliran);
            return HitungBingkai(baca);
        }
        catch (Exception)
        {
            return Array.Empty<float>();
        }
    }

    /// <summary>Baca seluruh sampel sebagai float lalu hitung RMS per bingkai.</summary>
    private static float[] HitungBingkai(WaveStream sumber)
    {
        var bingkai = new List<float>();
        try
        {
            var contoh = sumber.ToSampleProvider();
            var perBingkai = Math.Max(1, sumber.WaveFormat.SampleRate / LajuBingkai);
            var buffer = new float[perBingkai];

            int dibaca;
            while ((dibaca = contoh.Read(buffer, 0, buffer.Length)) > 0)
            {
                double jumlah = 0;
                for (var i = 0; i < dibaca; i++)
                {
                    jumlah += (double)buffer[i] * buffer[i];
                }

                bingkai.Add((float)Math.Sqrt(jumlah / dibaca));
            }
        }
        catch (Exception)
        {
            return Array.Empty<float>();
        }

        if (bingkai.Count == 0)
        {
            return Array.Empty<float>();
        }

        var puncak = 0f;
        foreach (var nilai in bingkai)
        {
            if (nilai > puncak) puncak = nilai;
        }

        if (puncak <= 0.0001f)
        {
            // Hening total; jangan bagi nol.
            return bingkai.ToArray();
        }

        for (var i = 0; i < bingkai.Count; i++)
        {
            bingkai[i] = MathF.Sqrt(Math.Min(1f, bingkai[i] / puncak));
        }

        return bingkai.ToArray();
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
