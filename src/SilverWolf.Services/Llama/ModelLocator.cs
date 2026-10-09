using SilverWolf.Core.Configuration;

namespace SilverWolf.Services.Llama;

/// <summary>
/// Pemilih model GGUF dan pencari <c>llama-server.exe</c> — port
/// <c>daftarModel</c>, <c>cariModel</c>, <c>aliasModel</c>, dan
/// <c>cariLlamaServer</c> dari <c>apps/server-node/src/llama.js</c>.
///
/// Urutan pemindaian dan kedalaman (2 untuk model, 3 untuk binary) dipertahankan:
/// mengubahnya bisa membuat aplikasi memilih model yang berbeda dari aplikasi
/// lama di mesin yang sama.
/// </summary>
public static class ModelLocator
{
    private const int KedalamanModel = 2;
    private const int KedalamanBinary = 3;

    private static bool Gguf(string nama) =>
        Path.GetExtension(nama).Equals(".gguf", StringComparison.OrdinalIgnoreCase);

    /// <summary>Port <c>daftarModel(k)</c>.</summary>
    public static List<string> DaftarModel(AppConfig k)
    {
        var hasil = new List<string>();

        if (!string.IsNullOrEmpty(k.LocalModelPath))
        {
            var eksplisit = Path.Combine(k.Akar, k.LocalModelPath);
            if (File.Exists(eksplisit) && !hasil.Contains(eksplisit))
            {
                hasil.Add(eksplisit);
            }
        }

        foreach (var dir in new[] { Path.Combine(k.Akar, "model"), Path.Combine(k.Akar, "assets", "llm") })
        {
            foreach (var f in CariSemua(dir, Gguf, KedalamanModel))
            {
                if (!hasil.Contains(f))
                {
                    hasil.Add(f);
                }
            }
        }

        return [.. hasil.OrderBy(f => Path.GetFileName(f).ToLowerInvariant(), StringComparer.Ordinal)];
    }

    /// <summary>Port <c>cariModel(k, log)</c>.</summary>
    public static (string Path, bool Ok) CariModel(AppConfig k, Action<string>? log = null)
    {
        if (!string.IsNullOrEmpty(k.LocalModelPath))
        {
            var eksplisit = Path.Combine(k.Akar, k.LocalModelPath);
            if (File.Exists(eksplisit))
            {
                return (eksplisit, true);
            }
        }

        var semua = DaftarModel(k);
        if (semua.Count == 0)
        {
            return (string.Empty, false);
        }

        if (semua.Count == 1)
        {
            return (semua[0], true);
        }

        log?.Invoke(
            $"! {semua.Count} model GGUF ditemukan dan VTUBER_LOCAL_MODEL_PATH tidak menunjuk berkas: " +
            $"memilih \"{Path.GetFileName(semua[0])}\". Setel VTUBER_LOCAL_MODEL_PATH agar tidak menebak. " +
            $"Kandidat: {string.Join(", ", semua.Select(Path.GetFileName))}");

        return (semua[0], true);
    }

    /// <summary>Port <c>aliasModel(k, model)</c>.</summary>
    public static string AliasModel(AppConfig k, string model)
    {
        var eksplisit = k.LocalModelAlias?.Trim() ?? string.Empty;
        if (eksplisit.Length != 0)
        {
            return eksplisit;
        }

        var berkas = model;
        if (string.IsNullOrEmpty(berkas))
        {
            berkas = CariModel(k).Path;
        }

        return string.IsNullOrEmpty(berkas)
            ? "gguf"
            : Path.GetFileNameWithoutExtension(berkas);
    }

    /// <summary>
    /// Ubah jalur absolut menjadi jalur relatif terhadap <see cref="AppConfig.Akar"/>.
    /// Mengembalikan jalur apa adanya bila tidak berada di bawah akar.
    ///
    /// <para>
    /// Preferensi model disimpan dalam bentuk relatif agar tetap berlaku
    /// sekalipun folder proyek dipindah atau disalin ke mesin lain — menyimpan
    /// <c>C:\Users\...\model\foo.gguf</c> akan langsung basi begitu jalurnya
    /// bergeser sedikit saja.
    /// </para>
    /// </summary>
    public static string Relatif(AppConfig k, string absolut)
    {
        if (string.IsNullOrWhiteSpace(absolut))
        {
            return absolut;
        }

        try
        {
            var penuh = Path.GetFullPath(absolut);
            var akar = Path.GetFullPath(k.Akar).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (!penuh.StartsWith(akar + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                return absolut;
            }

            var rel = penuh[(akar.Length + 1)..];
            return rel.Replace(Path.DirectorySeparatorChar, '/');
        }
        catch (ArgumentException)
        {
            return absolut;
        }
        catch (PathTooLongException)
        {
            return absolut;
        }
        catch (NotSupportedException)
        {
            return absolut;
        }
    }

    /// <summary>
    /// Kebalikan <see cref="Relatif"/>: jalur relatif (atau absolut) menjadi
    /// jalur absolut. Tidak memeriksa keberadaan berkas — pemanggil yang
    /// memutuskan.
    /// </summary>
    public static string Absolut(AppConfig k, string relatif) =>
        Path.IsPathRooted(relatif) ? relatif : Path.GetFullPath(Path.Combine(k.Akar, relatif));

    /// <summary>
    /// Pilihan model yang bisa ditawarkan ke pengguna. Berbeda dari
    /// <see cref="DaftarModel"/>: yang ini selalu menyertakan model yang sedang
    /// dipakai sekalipun berkasnya sudah dipindah, supaya ComboBox tidak pernah
    /// kehilangan pilihan aktifnya.
    /// </summary>
    public static List<string> Pilihan(AppConfig k)
    {
        var hasil = new List<string>();
        var terlihat = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Penyebab duplikat yang nyata: model yang dipilih lewat .env masuk
        // sebagai "<akar>\model/nama.gguf" (Path.Combine mempertahankan '/'),
        // sedangkan hasil pemindaian disk berbentuk "<akar>\model\nama.gguf".
        // Keduanya berkas yang sama, tapi bukan string yang sama — ComboBox
        // akan menampilkannya dua kali. Karena itu semua jalur dinormalisasi
        // lebih dulu.
        foreach (var jalur in DaftarModel(k))
        {
            var norm = Normal(jalur);
            if (terlihat.Add(norm))
            {
                hasil.Add(norm);
            }
        }

        var aktif = Absolut(k, k.LocalModelPath);
        if (aktif.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase))
        {
            var norm = Normal(aktif);
            if (terlihat.Add(norm))
            {
                hasil.Add(norm);
            }
        }

        // Urutan HARUS sama dengan DaftarModel: menurut nama berkas. Diurutkan
        // Ordinal (bukan OrdinalIgnoreCase) supaya huruf besar/kecil tidak
        // membuat urutan bergantung lokal mesin.
        return [.. hasil.OrderBy(
            f => Path.GetFileName(f).ToLowerInvariant(), StringComparer.Ordinal)];
    }

    /// <summary>
    /// Bentuk kanonik sebuah jalur, supaya dua jalur yang menunjuk berkas sama
    /// bisa dibandingkan sebagai string. Mengembalikan input apa adanya bila
    /// jalurnya tidak valid — lebih baik tampil apa adanya daripada gagal.
    /// </summary>
    private static string Normal(string jalur)
    {
        try
        {
            // GetFullPath TIDAK menyeragamkan pemisah: "akar\model/nama.gguf"
            // tetap mengandung '/' sesudahnya. Tanpa penggantian ini,
            // "<akar>\model/nama.gguf" (dari .env) dan "<akar>\model\nama.gguf"
            // (dari pemindaian disk) dianggap dua jalur berbeda — persis
            // penyebab ComboBox menampilkan model yang sama dua kali.
            return Path.GetFullPath(jalur)
                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        }
        catch (ArgumentException)
        {
            return jalur;
        }
        catch (PathTooLongException)
        {
            return jalur;
        }
        catch (NotSupportedException)
        {
            return jalur;
        }
    }

    /// <summary>Port <c>cariLlamaServer(k)</c>.</summary>
    public static string? CariLlamaServer(AppConfig k)
    {
        var root = Path.Combine(k.Akar, k.LlamaServer);
        return Cari(root, n => n.Equals("llama-server.exe", StringComparison.OrdinalIgnoreCase), KedalamanBinary);
    }

    private static string? Cari(string root, Func<string, bool> cocok, int kedalaman)
    {
        if (!Directory.Exists(root))
        {
            return File.Exists(root) && cocok(Path.GetFileName(root)) ? root : null;
        }

        IEnumerable<string> entri;
        try
        {
            entri = Directory.EnumerateFileSystemEntries(root);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }

        var direktori = new List<string>();
        foreach (var path in entri)
        {
            var nama = Path.GetFileName(path);
            if (File.Exists(path))
            {
                if (cocok(nama))
                {
                    return path;
                }
            }
            else
            {
                direktori.Add(path);
            }
        }

        if (kedalaman <= 0)
        {
            return null;
        }

        foreach (var dir in direktori)
        {
            if (Cari(dir, cocok, kedalaman - 1) is { } ketemu)
            {
                return ketemu;
            }
        }

        return null;
    }

    private static List<string> CariSemua(string root, Func<string, bool> cocok, int kedalaman)
    {
        var hasil = new List<string>();
        if (!Directory.Exists(root))
        {
            return hasil;
        }

        IEnumerable<string> entri;
        try
        {
            entri = Directory.EnumerateFileSystemEntries(root);
        }
        catch (IOException)
        {
            return hasil;
        }
        catch (UnauthorizedAccessException)
        {
            return hasil;
        }

        foreach (var path in entri)
        {
            if (File.Exists(path))
            {
                if (cocok(Path.GetFileName(path)))
                {
                    hasil.Add(path);
                }
            }
            else if (kedalaman > 0)
            {
                hasil.AddRange(CariSemua(path, cocok, kedalaman - 1));
            }
        }

        return hasil;
    }
}
