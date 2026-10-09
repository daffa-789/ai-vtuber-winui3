// Uji integrasi: panggil TtsWorker.HasilkanSatuAsync langsung dari rakitan
// yang sama dengan aplikasi. Ini membuktikan jalur pekerja benar-benar
// dipakai, bukan hanya skrip Python yang berdiri sendiri.
using System.Diagnostics;
using SilverWolf.Core.Configuration;
using SilverWolf.Services.Tts;

var akar = @"C:\Users\Daffa\Desktop\AI Vtuber Project\AI Vtuber WINUI3";

var env = new EnvSource();
env.File["VTUBER_PY_RVC"] =
    @"C:\Users\Daffa\Desktop\Folder Space AI\Folder Space Semester 6\voice changer3 glm\venv\Scripts\python.exe";

var konfig = ConfigReader.Baca(env, akar);
Console.WriteLine($"pekerja={konfig.TtsPekerja} batas={konfig.TtsBatasDetik} siap={konfig.TtsPekerjaSiapDetik}");
Console.WriteLine($"rvc={konfig.Rvc} python={konfig.PyRvc}");

var log = new List<string>();
var pekerja = new TtsWorker(konfig, s => { log.Add(s); Console.WriteLine($"  {s}"); });

if (!pekerja.Siap) { Console.WriteLine("TTS TIDAK SIAP"); return 1; }

// Kalimat sengaja diulang-ulang agar cache tidak menutupi pengukuran.
var kalimat = new[]
{
    "Halo Master, apa kabar hari ini?",
    "Aku sudah menunggu kamu dari tadi.",
};

var t0 = Stopwatch.StartNew();
for (int i = 0; i < kalimat.Length; i++)
{
    var t = Stopwatch.StartNew();
    var wav = await pekerja.HasilkanSatuAsync(kalimat[i]);
    Console.WriteLine($"[{i + 1}] {(wav is null ? "GAGAL" : "OK " + Path.GetFileName(wav))} dalam {t.Elapsed.TotalSeconds:F1} dtk");

    if (wav is not null)
    {
        using var r = new NAudio.Wave.WaveFileReader(wav);
        Console.WriteLine($"     durasi audio {r.TotalTime.TotalSeconds:F2} dtk @ {r.WaveFormat.SampleRate} Hz");
    }
}

Console.WriteLine($"TOTAL {t0.Elapsed.TotalSeconds:F1} dtk untuk {kalimat.Length} kalimat");
pekerja.Matikan();
Console.WriteLine("pekerja dihentikan");
return 0;
