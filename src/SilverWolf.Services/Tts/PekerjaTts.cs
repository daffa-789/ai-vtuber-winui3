using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SilverWolf.Core.Configuration;

namespace SilverWolf.Services.Tts;

/// <summary>
/// Pekerja suara Python yang menetap: model dimuat SEKALI, lalu setiap kalimat
/// dilayani lewat saluran JSON.
///
/// <para>
/// <b>Masalah yang diperbaiki.</b> Cara lama menjalankan proses Python baru
/// untuk tiap kalimat, sehingga seluruh model RVC dimuat ulang tiap kali.
/// Diukur di mesin ini (4 core, CPU saja):
/// </para>
/// <list type="bullet">
///   <item><description>muat RVC + HuBERT: 0,6–1,0 dtk</description></item>
///   <item><description>inferensi pertama (memuat rmvpe): 21–24 dtk</description></item>
///   <item><description>inferensi berikutnya: 6–13 dtk</description></item>
/// </list>
/// <para>
/// Jadi setiap kalimat membayar ~20 dtk hanya untuk memuat rmvpe. Balasan dua
/// kalimat menembus batas 40 dtk dan antrean dibatalkan sebelum satu pun WAV
/// sampai ke pemutar — gejalanya "menyiapkan suara…" tanpa henti, tanpa suara.
/// </para>
///
/// <para>
/// <b>Kenapa bukan stdout.</b> Pustaka RVC mencetak sendiri ke stdout
/// (<c>rvc_python/configs/config.py:93</c>), jadi stdout tidak bisa dipakai
/// sebagai jalur data. Protokol lewat pipe bernama yang dibuka anak lewat
/// variabel lingkungan <c>SW_PROTO_PIPE</c>.
/// </para>
/// </summary>
public sealed class PekerjaTts : IDisposable
{
    private readonly AppConfig _konfig;
    private readonly string _akar;
    private readonly Action<string>? _log;
    private readonly string _skrip;
    private readonly SemaphoreSlim _kunci = new(1, 1);

    /// <summary>
    /// Dipakai <see cref="NyalakanAsync"/> saat dipanggil dari dalam
    /// <see cref="HasilkanAsync"/> — pemanggil sudah memegang <see cref="_kunci"/>,
    /// jadi memintanya lagi akan mengunci diri sendiri (deadlock). Bug ini nyata:
    /// gejalanya proses uji diam tanpa satu baris log sampai dibunuh.
    /// </summary>
    private bool _nyalaDalam;

    private Process? _proses;
    private StreamWriter? _masuk;
    private StreamReader? _keluar;
    private System.IO.Pipes.NamedPipeServerStream? _pipa;
    private bool _dibuang;

    /// <summary>Alasan terakhir pekerja gagal disiapkan, untuk ditampilkan di UI.</summary>
    public string? Galat { get; private set; }

    public PekerjaTts(AppConfig konfig, Action<string>? log = null)
    {
        _konfig = konfig;
        _akar = konfig.Akar;
        _log = log;
        _skrip = Path.Combine(_akar, "tools", "tts", "pekerja_tts.py");
    }

    /// <summary>Apakah pekerja bisa dijalankan pada konfigurasi ini.</summary>
    public bool Tersedia => _konfig.TtsPekerja && File.Exists(_skrip);

    /// <summary>Apakah proses pekerja sedang hidup.</summary>
    public bool Hidup
    {
        get
        {
            var p = _proses;
            try { return p is { HasExited: false }; }
            catch { return false; }
        }
    }

    /// <summary>
    /// Nyalakan pekerja dan tunggu sampai model selesai dimuat.
    ///
    /// <para>
    /// Batas tunggu memakai <see cref="AppConfig.TtsPekerjaSiapDetik"/>
    /// (bawaan 120 dtk), bukan <see cref="AppConfig.TtsBatasDetik"/>. Mencampur
    /// keduanya adalah kesalahan yang sudah pernah terjadi: batas yang cocok
    /// untuk satu kalimat terlalu pendek untuk memuat model, sehingga model
    /// yang sehat dimatikan di tengah pemuatan dan gejalanya menyerupai crash.
    /// </para>
    /// </summary>
    public async Task<bool> NyalakanAsync(CancellationToken ct = default)
    {
        // Sudah dipanggil dari dalam HasilkanAsync yang memegang _kunci:
        // kerjakan langsung tanpa mengambil kunci lagi.
        if (_nyalaDalam) return await NyalakanIntiAsync(ct).ConfigureAwait(false);

        await _kunci.WaitAsync(ct).ConfigureAwait(false);
        _nyalaDalam = true;
        try
        {
            return await NyalakanIntiAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _nyalaDalam = false;
            _kunci.Release();
        }
    }

    private async Task<bool> NyalakanIntiAsync(CancellationToken ct)
    {
        if (_dibuang || Hidup) return Hidup;
        if (!Tersedia)
        {
            Galat = $"pekerja tidak tersedia ({_skrip})";
            return false;
        }

        try
        {
            var py = CariPython();
            if (py is null)
            {
                Galat = "Python RVC tidak ditemukan (set VTUBER_PY_RVC di .env)";
                _log?.Invoke($"[tts] {Galat}");
                return false;
            }

            // Pipa bernama untuk jalur protokol. Nama unik supaya dua instans
            // aplikasi tidak saling mencuri saluran.
            var namaPipa = $"sw-tts-{Environment.ProcessId}-{Guid.NewGuid():N}";

            var psi = new ProcessStartInfo
            {
                FileName = py,
                WorkingDirectory = Path.GetDirectoryName(_skrip)!,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                // stdout dibuang: pustaka RVC mencetak ke sini.
                RedirectStandardOutput = true,
                CreateNoWindow = true,
                // Tanpa BOM: anak membaca baris pertama dengan json.loads, dan
                // BOM UTF-8 membuat baris itu ditolak ("Unexpected UTF-8 BOM").
                StandardInputEncoding = new UTF8Encoding(false),
                StandardErrorEncoding = Encoding.UTF8,
                StandardOutputEncoding = Encoding.UTF8,
            };

            psi.ArgumentList.Add("-u");
            psi.ArgumentList.Add(_skrip);
            psi.Environment["PYTHONIOENCODING"] = "utf-8";
            psi.Environment["PYTHONUNBUFFERED"] = "1";
            psi.Environment["SW_PROTO_PIPE"] = namaPipa;
            psi.Environment["SW_PIPER_EXE"] = CariPiper();

            var proses = new Process { StartInfo = psi };

            // ── Pipa protokol disiapkan SEBELUM Start() ──────────────────────
            // Server harus ada lebih dulu; anak membuka pipa ini saat mulai.
            // Kalau dibalik, anak gagal menyambung dan mati di awal.
            var pipaKeluar = new System.IO.Pipes.NamedPipeServerStream(
                namaPipa, System.IO.Pipes.PipeDirection.InOut, 1,
                System.IO.Pipes.PipeTransmissionMode.Byte,
                System.IO.Pipes.PipeOptions.Asynchronous);

            try
            {
                if (!proses.Start())
                {
                    Galat = "proses pekerja gagal dimulai";
                    pipaKeluar.Dispose();
                    proses.Dispose();
                    return false;
                }
            }
            catch (Exception galat)
            {
                Galat = $"gagal menjalankan pekerja: {galat.Message}";
                _log?.Invoke($"[tts] {Galat}");
                pipaKeluar.Dispose();
                proses.Dispose();
                return false;
            }

            // WAJIB: kuras stderr. Menyetel redirect tanpa pembaca membuat
            // anak memblokir saat buffer pipa penuh — bug nyata yang pernah
            // terjadi pada rantai TTS ini.
            proses.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data)) _log?.Invoke($"[tts-py] {e.Data}");
            };
            proses.OutputDataReceived += (_, e) =>
            {
                // stdout milik pustaka RVC yang mencetak sendiri. Dibuang ke log
                // bertanda # supaya jelas ini bukan data protokol.
                if (!string.IsNullOrEmpty(e.Data)) _log?.Invoke($"[tts-py#] {e.Data}");
            };
            proses.BeginErrorReadLine();
            proses.BeginOutputReadLine();

            var masuk = new StreamWriter(proses.StandardInput.BaseStream, new UTF8Encoding(false))
            {
                AutoFlush = true,
            };

            // Tunggu anak menyambung ke pipa, tapi jangan selamanya: kalau anak
            // mati di awal (mis. impor torch gagal), kita harus tahu cepat.
            var sambung = pipaKeluar.WaitForConnectionAsync(CancellationToken.None);
            var mati = proses.WaitForExitAsync(CancellationToken.None);
            var batasSiap = TimeSpan.FromSeconds(Math.Max(10, _konfig.TtsPekerjaSiapDetik));

            var pemenang = await Task.WhenAny(sambung, mati, Task.Delay(batasSiap)).ConfigureAwait(false);
            if (pemenang != sambung || !pipaKeluar.IsConnected)
            {
                Galat = pemenang == mati
                    ? "pekerja berhenti sebelum menyambung (lihat log)"
                    : $"pekerja tidak menyambung dalam {batasSiap.TotalSeconds:F0} dtk";
                _log?.Invoke($"[tts] {Galat}");
                Bersihkan(proses, masuk, null, pipaKeluar);
                return false;
            }

            var keluar = new StreamReader(pipaKeluar, Encoding.UTF8, false, 4096, leaveOpen: true);

            // Tunggu sapaan "siap" — bukti model benar-benar termuat, bukan
            // sekadar proses hidup.
            var baris = await BacaDenganBatasAsync(keluar, batasSiap, ct).ConfigureAwait(false);
            var sapaan = Urai(baris);
            if (sapaan is null || Jenis(sapaan) != "siap")
            {
                var pesan = sapaan is null ? baris : Pesan(sapaan);
                Galat = $"pekerja tidak melaporkan siap: {Potong(pesan, 200)}";
                _log?.Invoke($"[tts] {Galat}");
                Bersihkan(proses, masuk, keluar, pipaKeluar);
                return false;
            }

            _proses = proses;
            _masuk = masuk;
            _keluar = keluar;
            _pipa = pipaKeluar;
            Galat = null;
            _log?.Invoke($"[tts] pekerja siap (model dimuat sekali, pid {proses.Id})");
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception galat)
        {
            Galat = galat.Message;
            _log?.Invoke($"[tts] gagal menyalakan pekerja: {galat.Message}");
            return false;
        }
    }

    /// <summary>
    /// Minta satu kalimat disintesis. Mengembalikan jalur WAV, atau null kalau
    /// gagal. Pekerja tetap hidup setelah kegagalan; hanya kegagalan protokol
    /// yang mematikannya supaya panggilan berikutnya menyalakannya ulang.
    /// </summary>
    public async Task<string?> HasilkanAsync(
        string teks, string keluaran, bool tanpaRvc, CancellationToken ct = default)
    {
        await _kunci.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!Hidup)
            {
                // _kunci SUDAH dipegang di sini. NyalakanAsync akan memintanya
                // lagi dan menggantung selamanya kalau _nyalaDalam belum diset.
                // Versi lama menyetel bendera itu DI DALAM NyalakanAsync —
                // terlalu telat: pemeriksaannya sudah lewat. Akibatnya kalimat
                // pertama selalu deadlock dan proses uji diam tanpa satu baris
                // log sampai dibunuh.
                _nyalaDalam = true;
                try
                {
                    if (!await NyalakanAsync(ct).ConfigureAwait(false)) return null;
                }
                finally
                {
                    _nyalaDalam = false;
                }
            }

            var permintaan = JsonSerializer.Serialize(new
            {
                teks,
                keluar = keluaran,
                piper = CariPiper(),
                tanpa_rvc = tanpaRvc,
            });

            try
            {
                await _masuk!.WriteLineAsync(permintaan).ConfigureAwait(false);
            }
            catch (Exception galat)
            {
                _log?.Invoke($"[tts] gagal mengirim ke pekerja: {galat.Message}");
                Matikan();
                return null;
            }

            // Tunggu "selesai" atau "galat". "progres" hanya diteruskan ke log.
            var batas = TimeSpan.FromSeconds(Math.Max(5, _konfig.TtsBatasDetik));
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                var baris = await BacaDenganBatasAsync(_keluar!, batas, ct).ConfigureAwait(false);
                if (baris is null)
                {
                    _log?.Invoke($"[tts] pekerja tidak menjawab dalam {batas.TotalSeconds:F0} dtk");
                    Matikan();
                    return null;
                }

                var pesan = Urai(baris);
                if (pesan is null)
                {
                    // Baris bukan JSON (mis. cetakan pustaka) — abaikan, jangan
                    // menganggapnya jawaban.
                    _log?.Invoke($"[tts-py?] {Potong(baris, 160)}");
                    continue;
                }

                var el = pesan.Value;
                switch (Jenis(pesan))
                {
                    case "progres":
                        var tahap = el.TryGetProperty("tahap", out var th) ? th.GetString() : "?";
                        _log?.Invoke($"[tts] tahap {tahap}…");
                        continue;
                    case "selesai":
                        var jalur = el.TryGetProperty("keluar", out var k) ? k.GetString() : null;
                        var detik = el.TryGetProperty("detik", out var d) ? d.GetDouble() : 0;
                        _log?.Invoke($"[tts] kalimat selesai dalam {detik:F1} dtk");
                        return jalur;
                    case "galat":
                        _log?.Invoke($"[tts] pekerja melaporkan galat: {Pesan(pesan)}");
                        // Galat sintesis tidak mematikan pekerja: model masih
                        // termuat, kalimat berikutnya masih bisa berhasil.
                        return null;
                    default:
                        _log?.Invoke($"[tts-py?] {Potong(baris, 160)}");
                        continue;
                }
            }
        }
        finally
        {
            _kunci.Release();
        }
    }

    /// <summary>
    /// Baca satu baris dengan batas waktu. <see cref="StreamReader.ReadLineAsync"/>
    /// tidak punya batas sendiri, jadi penantian tanpa batas harus dicegah di
    /// sini — kalau tidak, satu pipa yang membeku menggantung seluruh antrean
    /// suara.
    /// </summary>
    private static async Task<string?> BacaDenganBatasAsync(
        StreamReader pembaca, TimeSpan batas, CancellationToken ct)
    {
        var tugas = pembaca.ReadLineAsync(ct).AsTask();
        var selesai = await Task.WhenAny(tugas, Task.Delay(batas, ct)).ConfigureAwait(false);
        if (selesai != tugas) return null;
        try { return await tugas.ConfigureAwait(false); }
        catch (OperationCanceledException) { return null; }
    }

    private static JsonElement? Urai(string? baris)
    {
        if (string.IsNullOrWhiteSpace(baris)) return null;
        try
        {
            var dokumen = JsonDocument.Parse(baris.Trim());
            return dokumen.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Jenis(JsonElement? pesan) =>
        pesan is { } p && p.TryGetProperty("jenis", out var j) ? j.GetString() ?? "" : "";

    private static string Pesan(JsonElement? pesan) =>
        pesan is { } p && p.TryGetProperty("pesan", out var m) ? m.GetString() ?? "" : "";

    /// <summary>Cari interpreter Python untuk rantai RVC (sama dengan TtsWorker).</summary>
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

        return kandidat.FirstOrDefault(File.Exists);
    }

    private static string CariPiper() =>
        Environment.GetEnvironmentVariable("SW_PIPER_EXE")
        ?? @"C:\Users\Daffa\.workbuddy-ai\binaries\python\envs\default\Scripts\piper.exe";

    /// <summary>Hentikan pekerja dengan rapi, lalu paksa kalau perlu.</summary>
    public void Matikan()
    {
        var proses = _proses;
        var masuk = _masuk;
        var keluar = _keluar;
        var pipa = _pipa;
        _proses = null;
        _masuk = null;
        _keluar = null;
        _pipa = null;
        Bersihkan(proses, masuk, keluar, pipa);
    }

    private void Bersihkan(
        Process? proses, StreamWriter? masuk, StreamReader? keluar,
        System.IO.Pipes.NamedPipeServerStream? pipa)
    {
        try
        {
            if (proses is { HasExited: false })
            {
                // Minta berhenti baik-baik dulu supaya torch membebaskan memori.
                try
                {
                    masuk?.WriteLine("{\"keluar_akhir\":true}");
                    masuk?.Flush();
                }
                catch { /* pipa mungkin sudah tutup */ }

                if (!proses.WaitForExit(3000))
                {
                    proses.Kill(entireProcessTree: true);
                    proses.WaitForExit(5000);
                }
            }
        }
        catch
        {
            // Proses mungkin sudah mati sendiri.
        }
        finally
        {
            try { masuk?.Dispose(); } catch { }
            try { keluar?.Dispose(); } catch { }
            try { pipa?.Dispose(); } catch { }
            try { proses?.Dispose(); } catch { }
        }
    }

    private static string Potong(string? teks, int n)
    {
        if (string.IsNullOrEmpty(teks)) return "";
        return teks.Length <= n ? teks : teks[..n] + "…";
    }

    public void Dispose()
    {
        if (_dibuang) return;
        _dibuang = true;
        Matikan();
        _kunci.Dispose();
    }
}
