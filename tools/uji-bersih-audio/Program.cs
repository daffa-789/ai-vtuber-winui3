// Sapu bersih audio sementara TTS memakai kode produksi yang sama.
// Dipakai sekali untuk membersihkan sisa sesi-sesi lama, dan bisa dijalankan
// lagi kapan saja — TempAudio.Bersihkan idempoten dan tidak pernah melempar.
using SilverWolf.Services.Tts;

var temp = Path.GetTempPath();
long Sebelum()
{
    long n = 0;
    try
    {
        var d = Path.Combine(temp, "silverwolf-tts");
        if (Directory.Exists(d))
            foreach (var f in Directory.GetFiles(d)) n += new FileInfo(f).Length;
        foreach (var f in Directory.GetFiles(temp, "sw-tts-*.wav")) n += new FileInfo(f).Length;
        foreach (var f in Directory.GetFiles(temp, "sw-gabung-*.wav")) n += new FileInfo(f).Length;
    }
    catch (Exception) { }
    return n;
}

var sebelum = Sebelum();
Console.WriteLine($"sebelum: {sebelum / 1024} KiB");
TempAudio.Bersihkan(s => Console.WriteLine($"  {s}"));
var sesudah = Sebelum();
Console.WriteLine($"sesudah: {sesudah / 1024} KiB");
Console.WriteLine($"dibebaskan: {(sebelum - sesudah) / 1024} KiB");
Console.WriteLine(Directory.Exists(Path.Combine(temp, "silverwolf-tts"))
    ? "folder cache masih ada (mungkin terkunci proses lain)"
    : "folder cache %TEMP%\\silverwolf-tts dihapus");
