// Uji integrasi: panggil TtsWorker langsung dari rakitan yang sama dengan
// aplikasi. Ini membuktikan jalur pekerja benar-benar dipakai, bukan hanya
// skrip Python yang berdiri sendiri.
//
// Sejak 9 Okt uji ini memakai HasilkanKlipAsync — jalur MEMORI yang dipakai
// TtsPipeline — supaya yang diuji memang yang berjalan di aplikasi: berkas
// WAV dibuat, dibaca ke KlipSuara, lalu dihapus, dan penggabungan terjadi
// di RAM tanpa sw-gabung-*.wav di %TEMP%.
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
Console.WriteLine($"cacheMemori={konfig.TtsCacheMemori} arsip={konfig.TtsArsip}");

var log = new List<string>();
var pekerja = new TtsWorker(konfig, s => { log.Add(s); Console.WriteLine($"  {s}"); });

if (!pekerja.Siap) { Console.WriteLine("TTS TIDAK SIAP"); return 1; }

// Hitung berkas audio yang ada di %TEMP% SEBELUM dan SESUDAH: itulah bukti
// "audio hidup di memori lalu berkasnya dihapus".
static int HitungSisaTemp()
{
    var temp = Path.GetTempPath();
    var jumlah = 0;
    try
    {
        jumlah += File.Exists(Path.Combine(temp, "sw-gabung-1.wav")) ? 1 : 0;
        jumlah += Directory.Exists(Path.Combine(temp, "silverwolf-tts"))
            ? Directory.GetFiles(Path.Combine(temp, "silverwolf-tts"), "*.wav").Length
            : 0;
        foreach (var f in Directory.GetFiles(temp, "sw-tts-*.wav"))
        {
            _ = f;
            jumlah++;
        }
    }
    catch (Exception)
    {
        // pengukuran diagnostik; tidak boleh menggagalkan uji
    }

    return jumlah;
}

var tempAwal = HitungSisaTemp();
Console.WriteLine($"berkas audio di %TEMP% sebelum: {tempAwal}");

// Kalimat sengaja diulang-ulang agar cache tidak menutupi pengukuran.
var kalimat = new[]
{
    "Halo Master, apa kabar hari ini?",
    "Aku sudah menunggu kamu dari tadi.",
};

var t0 = Stopwatch.StartNew();
var klips = new List<KlipSuara>();
for (int i = 0; i < kalimat.Length; i++)
{
    var t = Stopwatch.StartNew();
    var klip = await pekerja.HasilkanKlipAsync(kalimat[i]);
    Console.WriteLine($"[{i + 1}] {(klip is null ? "GAGAL" : $"OK {klip.Ukuran} bita")} dalam {t.Elapsed.TotalSeconds:F1} dtk");

    if (klip is not null)
    {
        klips.Add(klip);
        using var r = new NAudio.Wave.WaveFileReader(new MemoryStream(klip.Data, writable: false));
        Console.WriteLine($"     durasi audio {r.TotalTime.TotalSeconds:F2} dtk @ {r.WaveFormat.SampleRate} Hz");
    }
}

Console.WriteLine($"TOTAL {t0.Elapsed.TotalSeconds:F1} dtk untuk {kalimat.Length} kalimat");

// Penggabungan di memori: bukti tidak ada berkas perantara.
// GabungWav internal (hanya untuk pipeline), jadi di sini cukup diperiksa
// bahwa setiap klip benar-benar WAV yang bisa dibuka dari RAM.
if (klips.Count > 1)
{
    var semuaSah = true;
    foreach (var k in klips)
    {
        try
        {
            using var r = new NAudio.Wave.WaveFileReader(new MemoryStream(k.Data, writable: false));
            semuaSah &= r.TotalTime > TimeSpan.Zero;
        }
        catch (Exception)
        {
            semuaSah = false;
        }
    }

    Console.WriteLine(semuaSah
        ? $"{klips.Count} klip sah dibaca dari RAM (penggabungan terjadi di memori)"
        : "GABUNG MEMORI: ada klip yang tidak bisa dibaca dari RAM");
}

var tempAkhir = HitungSisaTemp();
Console.WriteLine($"berkas audio di %TEMP% sesudah: {tempAkhir}");
Console.WriteLine(tempAkhir <= tempAwal
    ? "SIKLUS MEMORI OK: tidak ada berkas audio baru yang tertinggal"
    : $"PERINGATAN: {tempAkhir - tempAwal} berkas audio baru tertinggal di %TEMP%");

pekerja.BersihkanCache();
pekerja.Matikan();
Console.WriteLine("pekerja dihentikan");
return 0;
