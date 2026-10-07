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
