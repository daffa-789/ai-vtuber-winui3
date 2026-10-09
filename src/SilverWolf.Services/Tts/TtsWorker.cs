using System.Diagnostics;
using NAudio.Wave;
using System.Security.Cryptography;
using System.Text;
using SilverWolf.Core.Configuration;
using SilverWolf.Core.Text;

namespace SilverWolf.Services.Tts;

/// <summary>
/// Menjalankan rantai suara (Piper → RVC) sebagai proses Python lalu
/// mengembalikan jalur WAV yang siap diputar.
///
/// <para>
/// <b>Kenapa lewat proses, bukan P/Invoke:</b> rantai suara sudah terbukti
/// berjalan sebagai skrip Python (<c>tools/tts/</c>) dengan torch + fairseq +
/// rvc-python di venv terpisah. Memindahkannya ke C# berarti mengikat proyek
/// ke ONNX Runtime + implementasi RVC sendiri — pekerjaan besar yang tidak
/// menambah kualitas suara sedikit pun. Proses terpisah juga menjaga aplikasi
/// tetap hidup kalau RVC jatuh (masalah lama: RVC di CPU rapuh).
/// </para>
///
/// <para>
/// <b>Batas waktu itu wajib.</b> RVC di CPU berjalan RTF 1,1–2,5× realtime,
/// jadi kalimat 5 detik butuh 6–12 detik. Tanpa batas, satu kalimat yang macet
/// akan menggantung antrean suara selamanya.
/// </para>
/// </summary>
public sealed class TtsWorker
{
    private readonly AppConfig _konfig;
    private readonly string _akar;
    private readonly Action<string>? _log;

    /// <summary>Jalur skrip yang dijalankan. Diisi saat konstruksi.</summary>
    private readonly string _skripPiper;

    /// <summary>Cache: hash teks+profil → jalur WAV yang sudah ada.</summary>
    private readonly string _folderCache;

    /// <summary>
    /// Pekerja Python menetap. Dimuat sekali, dipakai untuk semua kalimat.
    /// Lihat <see cref="PekerjaTts"/> untuk alasan lengkapnya.
    /// </summary>
    private readonly PekerjaTts? _pekerja;

    public TtsWorker(AppConfig konfig, Action<string>? log = null)
    {
        _konfig = konfig;
        _akar = konfig.Akar;
        _log = log;

        _skripPiper = Path.Combine(_akar, "tools", "tts", "buat_suara.py");
        _folderCache = Path.Combine(Path.GetTempPath(), "silverwolf-tts");

        // Pekerja hanya dibuat kalau diizinkan. Kalau tidak, jalur lama
        // (proses per kalimat) tetap dipakai — lambat tapi masih berfungsi,
        // dan berguna untuk membandingkan saat mendiagnosis.
        _pekerja = konfig.TtsPekerja ? new PekerjaTts(konfig, log) : null;
    }

    /// <summary>Apakah TTS diizinkan berjalan pada konfigurasi ini.</summary>
    public bool Siap => _konfig.TtsHidup && File.Exists(_skripPiper);

    /// <summary>
    /// Kalau TTS tidak siap, kembalikan alasannya supaya bisa ditampilkan di
    /// UI. Sebelumnya seluruh jalur suara senyap tanpa satu pun petunjuk —
    /// inilah yang membuat "TTS tidak balas" sulit didiagnosis.
    /// </summary>
    public string? AlasanTidakSiap()
    {
        if (!_konfig.TtsHidup)
        {
            return "TTS dimatikan di .env (VTUBER_TTS)";
        }

        if (!File.Exists(_skripPiper))
        {
            return $"skrip tidak ditemukan: {_skripPiper}";
        }

        var py = CariPython();
        return py is null
            ? "Python RVC tidak ditemukan (set VTUBER_PY_RVC di .env)"
            : null;
    }

    /// <summary>
    /// Cari interpreter Python untuk rantai RVC. Urutan:
    /// 1. <c>VTUBER_PY_RVC</c> di .env (satu-satunya sumber kebenaran);
    /// 2. kandidat yang dikenal di mesin ini (venv proyek "voice changer3 glm").
    /// </summary>
    private string? CariPython()
    {
        if (!string.IsNullOrWhiteSpace(_konfig.PyRvc) && File.Exists(_konfig.PyRvc))
        {
            return _konfig.PyRvc;
        }

        string[] kandidat =
        [
            @"C:/Users/Daffa/Desktop/Folder Space AI/Folder Space Semester 6/voice changer3 glm/venv/Scripts/python.exe",
        ];

        foreach (var k in kandidat)
        {
            if (File.Exists(k))
            {
                return k;
            }
        }

        return null;
    }

    /// <summary>
    /// Pecah teks menjadi kalimat lalu hasilkan WAV untuk tiap kalimat.
    /// Mengembalikan daftar jalur WAV berurutan yang siap diputar.
    ///
    /// <para>
    /// <b>Kenapa per kalimat:</b> RVC tidak realtime di CPU. Memotong balasan
    /// menjadi kalimat membuat kalimat pertama bisa diputar sementara kalimat
    /// berikutnya masih dikonversi — persepsi latensi turun drastis
    /// dibanding menunggu seluruh balasan.
    /// </para>
    /// </summary>
    public async Task<List<string>> HasilkanAsync(string teks, CancellationToken ct = default)
    {
        var hasil = new List<string>();
        if (!Siap || string.IsNullOrWhiteSpace(teks))
        {
            return hasil;
        }

        var potong = SentenceSplitter.PotongKalimat(teks);
        var kalimat = new List<string>(potong.Kalimat);

        // Sisa yang belum diakhiri tanda baca tetap dibacakan — kalau tidak,
        // kalimat terakhir balasan akan hilang tanpa suara.
        if (!string.IsNullOrWhiteSpace(potong.Sisa))
        {
            kalimat.Add(potong.Sisa);
        }

        foreach (var k in kalimat)
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }

            var wav = await HasilkanSatuAsync(k, ct).ConfigureAwait(false);
            if (wav is not null)
            {
                hasil.Add(wav);
            }
        }

        return hasil;
    }

    /// <summary>
    /// Hasilkan satu WAV untuk satu potongan teks. Mengembalikan <c>null</c>
    /// kalau gagal — pemanggil tetap lanjut ke kalimat berikutnya, bukan
    /// menggagalkan seluruh balasan.
    /// </summary>
    public async Task<string?> HasilkanSatuAsync(string kalimat, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        // Dua tahap, dan urutannya penting:
        //   1. BuangTag   — tag [emosi] dari protokol balasan;
        //   2. Bersihkan  — penanda Markdown (*, `, #, —) supaya TTS tidak
        //                   mengucapkannya sebagai "bintang" (keluhan Master).
        // Dipasang di sini karena INI satu-satunya titik yang dilewati semua
        // jalur sintesis — TtsPipeline maupun HasilkanAsync.
        var teks = TeksUcapan.Bersihkan(SentenceSplitter.BuangTag(kalimat)).Trim();
        if (teks.Length == 0)
        {
            return null;
        }

        // Intonasi. Tag emosi dibaca dari teks ASLI (sebelum dibersihkan),
        // karena BuangTag sudah menghapusnya.
        //
        // Hanya dipakai kalau emosinya dikenal — kalau tidak, null dikirim dan
        // pekerja memakai tempo bawaan sesi. Ini menjaga perilaku lama tetap
        // sama untuk balasan yang tidak bertag.
        var emosi = EmotionParser.ExtractEmotion(kalimat).Emotion;
        var tempo = Intonasi.Dikenali(emosi) ? Intonasi.Tempo(emosi) : (double?)null;
        // Jeda eksplisit hanya bila tempo memang dipakai, supaya kalimat tanpa
        // emosi tidak berubah bentuknya.
        if (tempo is not null)
        {
            teks = Intonasi.BeriJeda(teks, emosi);
        }

        // ── Cache ────────────────────────────────────────────────────────────
        // Kunci cache HARUS memuat seluruh parameter yang memengaruhi suara.
        // Kalau tidak, mengubah transpose di .env akan tetap memakai WAV lama
        // dan Master akan melihat "perubahan tidak berefek" — bug yang sangat
        // membingungkan dan pernah terjadi di aplikasi lama.
        var keluaran = _konfig.TtsCache ? JalurCache(teks) : JalurSementara();
        if (_konfig.TtsCache && WavSah(keluaran))
        {
            return keluaran;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(keluaran)!);

        // Penulisan selalu lewat berkas sementara, lalu dipindahkan. Dengan
        // begitu pembaca tidak pernah melihat WAV setengah jadi, dan kegagalan
        // di tengah jalan tidak meninggalkan cache yang rusak.
        var sementara = keluaran + $".{Guid.NewGuid():N}.wav";

        // ── Jalur pekerja menetap ────────────────────────────────────────────
        // Model RVC dimuat SEKALI untuk seluruh sesi. Ini yang membuat suara
        // benar-benar keluar: tanpa ini setiap kalimat membayar ~20 dtk hanya
        // untuk memuat rmvpe, dan balasan dua kalimat menembus batas waktu.
        if (_pekerja is { Tersedia: true })
        {
            var wav = await _pekerja
                .HasilkanAsync(teks, sementara, tanpaRvc: !_konfig.Rvc, ct, tempo)
                .ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            if (wav is not null && WavSah(sementara))
            {
                File.Move(sementara, keluaran, overwrite: true);
                return keluaran;
            }
            _log?.Invoke($"[tts] pekerja tidak menghasilkan WAV: {Potong(teks, 40)}"
                + (_pekerja.Galat is { } g ? $" ({g})" : ""));
            // Tidak jatuh ke jalur lama untuk kalimat ini: itu berarti memuat
            // ulang model penuh dan justru memperparah. Kalimat berikutnya
            // akan menyalakan pekerja lagi.
            try { if (File.Exists(sementara)) File.Delete(sementara); }
            catch (IOException) { }
            return null;
        }

        // ── Jalur lama: proses Python baru per kalimat ───────────────────────
        // Dipakai hanya kalau VTUBER_TTS_PEKERJA=tidak.
        var argumen = new List<string> { _skripPiper };
        if (!_konfig.Rvc) argumen.Add("--no-rvc");
        argumen.Add(teks);
        argumen.Add(sementara);

        var py = CariPython();
        if (py is null)
        {
            _log?.Invoke("[tts] Python RVC tidak ditemukan");
            return null;
        }
        try
        {
            var hasil = await JalankanAsync(py, argumen, Path.GetDirectoryName(_skripPiper)!, ct)
                .ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            if (hasil && WavSah(sementara))
            {
                // Publikasikan cache hanya sesudah Python selesai dan WAV sah.
                File.Move(sementara, keluaran, overwrite: true);
                return keluaran;
            }
            _log?.Invoke($"[tts] gagal menghasilkan: {Potong(teks, 40)}");
            return null;
        }
        finally
        {
            // Hanya file sementara yang dibuat operasi ini; bukan file pengguna.
            try { if (File.Exists(sementara)) File.Delete(sementara); }
            catch (IOException) { }
        }
    }

    /// <summary>
    /// Nama berkas cache = hash dari teks + seluruh parameter suara.
    /// SHA-256 dipakai (bukan string.GetHashCode) karena hash .NET tidak stabil
    /// antar-jalan, sehingga cache tidak akan pernah kena.
    /// </summary>
    private string JalurCache(string teks)
    {
        var kunci = string.Join('|',
            teks,
            _konfig.Rvc ? "rvc" : "piper",
            _konfig.RvcModel,
            _konfig.RvcVersi,
            _konfig.RvcF0,
            _konfig.RvcTranspose.ToString(),
            _konfig.RvcIndeksLaju.ToString("F2"));

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(kunci)))[..16];
        return Path.Combine(_folderCache, $"{hash}.wav");
    }

    private static string JalurSementara() =>
        Path.Combine(Path.GetTempPath(), $"sw-tts-{Guid.NewGuid():N}.wav");

    private async Task<bool> JalankanAsync(
        string python, IReadOnlyList<string> argumen, string kerja, CancellationToken ct)
    {
        if (!File.Exists(python))
        {
            // Tanpa pre-check, Start() melempar Win32Exception yang mudah
            // tertukar dengan "RVC lambat". Pesan ini membuat penyebabnya jelas.
            _log?.Invoke($"[tts] Python tidak ditemukan: {python}");
            return false;
        }

        var psi = new ProcessStartInfo
        {
            FileName = python,
            WorkingDirectory = kerja,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        foreach (var nilai in argumen) psi.ArgumentList.Add(nilai);
        psi.Environment["PYTHONIOENCODING"] = "utf-8";
        psi.Environment["PYTHONUNBUFFERED"] = "1";

        // Batas waktu dan pembatalan sama-sama menghentikan proses anak.
        using var proses = new Process { StartInfo = psi };

        // ── WAJIB: kuras stdout & stderr ─────────────────────────────────
        // Stream redirect wajib dibaca: bila buffer penuh, anak memblokir
        // di write(). Ini memperbaiki risiko deadlock yang nyata pada kode lama;
        // bukan bukti bahwa setiap timeout sebelumnya berasal dari pipa penuh.
        string? galatTerakhir = null;
        proses.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                _log?.Invoke($"[tts-py] {e.Data}");
            }
        };
        proses.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                galatTerakhir = e.Data;
                _log?.Invoke($"[tts-py!] {e.Data}");
            }
        };

        if (_konfig.TtsBatasDetik <= 0)
        {
            _log?.Invoke("[tts] VTUBER_TTS_BATAS_DETIK harus lebih besar dari nol");
            return false;
        }
        ct.ThrowIfCancellationRequested();
        using var batas = CancellationTokenSource.CreateLinkedTokenSource(ct);
        batas.CancelAfter(TimeSpan.FromSeconds(_konfig.TtsBatasDetik));
        var mulai = DateTimeOffset.Now;

        try
        {
            if (!proses.Start())
            {
                return false;
            }
        }
        catch (Exception galat)
        {
            _log?.Invoke($"[tts] gagal menjalankan Python: {galat.Message}");
            return false;
        }

        try
        {
            proses.BeginOutputReadLine();
            proses.BeginErrorReadLine();
            await proses.WaitForExitAsync(batas.Token).ConfigureAwait(false);

            // Drain tuntas: WaitForExitAsync selesai sebelum handler async selesai
            // menelan baris terakhir. Overload tanpa argumen menunggunya.
            proses.WaitForExit();
        }
        catch (OperationCanceledException)
        {
            Matikan(proses);
            if (ct.IsCancellationRequested)
            {
                _log?.Invoke("[tts] dibatalkan; proses Python dihentikan");
                ct.ThrowIfCancellationRequested();
            }
            var nyata = (DateTimeOffset.Now - mulai).TotalSeconds;
            // Waktu saja tidak membuktikan deadlock vs RVC yang lambat.
            _log?.Invoke($"[tts] batas {_konfig.TtsBatasDetik} dtk terlampaui "
                + $"(nyata {nyata:F1} dtk), proses dihentikan");
            return false;
        }
        catch (Exception galat)
        {
            _log?.Invoke($"[tts] galat saat menunggu Python: {galat.Message}");
            Matikan(proses);
            return false;
        }

        if (proses.ExitCode != 0)
        {
            var petunjuk = galatTerakhir is null ? "" : $" — {Potong(galatTerakhir, 200)}";
            _log?.Invoke($"[tts] keluar kode {proses.ExitCode}{petunjuk}");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Hentikan proses Python beserta anaknya. RVC memuat torch + fairseq
    /// (beberapa GB), jadi proses yatim harus benar-benar dimatikan, bukan
    /// hanya ditinggalkan.
    /// </summary>
    private static void Matikan(Process proses)
    {
        try
        {
            if (!proses.HasExited)
            {
                proses.Kill(entireProcessTree: true);
                proses.WaitForExit(5000);
            }
        }
        catch
        {
            // Proses mungkin sudah mati sendiri; tidak ada yang bisa dilakukan.
        }
    }

    internal static bool WavSah(string jalur)
    {
        try
        {
            using var wav = new WaveFileReader(jalur);
            if (wav.Length <= 0 || wav.TotalTime <= TimeSpan.Zero
                || wav.WaveFormat.BlockAlign <= 0
                || wav.Length % wav.WaveFormat.BlockAlign != 0) return false;
            // Length dapat berasal dari header yang mengklaim data lebih banyak
            // daripada berkasnya. Baca dengan buffer kecil sampai data tuntas.
            var buffer = new byte[8192];
            long terbaca = 0;
            int jumlah;
            while ((jumlah = wav.Read(buffer, 0, buffer.Length)) > 0) terbaca += jumlah;
            return terbaca == wav.Length;
        }
        catch (Exception)
        {
            // Berkas kosong/terpotong dari proses gagal bukan cache yang sah.
            return false;
        }
    }

    /// <summary>
    /// Hentikan pekerja menetap. Dipanggil saat aplikasi ditutup — tanpa ini
    /// proses Python beserta torch tetap hidup dan menahan ratusan MB memori,
    /// yang pada mesin ini (16 GB, sering hanya ~3,7 GB bebas) langsung
    /// memperburuk masalah kehabisan memori.
    /// </summary>
    public void Matikan() => _pekerja?.Matikan();

    private static string Potong(string teks, int n) =>
        teks.Length <= n ? teks : teks[..n] + "…";
}