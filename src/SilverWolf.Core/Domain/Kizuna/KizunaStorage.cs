using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace SilverWolf.Core.Domain.Kizuna;

/// <summary>Tempat simpan data ikatan.</summary>
public interface IKizunaStorage
{
    Task<KizunaEnvelope?> LoadAsync(string key, CancellationToken ct = default);

    Task SaveAsync(string key, KizunaEnvelope envelope, CancellationToken ct = default);
}

public static class KizunaJson
{
    /// <summary>
    /// Opsi yang meniru <c>JSON.stringify(data, null, 2)</c> milik pustaka asli:
    /// indentasi 2 spasi, kunci camelCase, dan properti null tidak ditulis
    /// (pustaka asli menyebar properti secara bersyarat).
    /// </summary>
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}

/// <summary>Penyimpanan di memori — dipakai unit test dan mode tanpa berkas.</summary>
public sealed class InMemoryKizunaStorage : IKizunaStorage
{
    private readonly Dictionary<string, KizunaEnvelope> _data = new(StringComparer.Ordinal);

    public Task<KizunaEnvelope?> LoadAsync(string key, CancellationToken ct = default) =>
        Task.FromResult(_data.TryGetValue(key, out var e) ? e : null);

    public Task SaveAsync(string key, KizunaEnvelope envelope, CancellationToken ct = default)
    {
        _data[key] = envelope;
        return Task.CompletedTask;
    }
}

/// <summary>
/// Penyimpanan berkas — port <c>ExternalStorageProvider</c> dari
/// <c>@aituber-onair/kizuna</c>.
///
/// Nama berkas harus identik dengan yang sudah ditulis aplikasi Electron:
/// <c>&lt;dataDir&gt;/&lt;storageKey dengan karakter non-[A-Za-z0-9_-] jadi "_"&gt;.json</c>.
/// Kalau tidak, data ikatan pengguna akan hilang saat migrasi.
/// </summary>
public sealed class FileKizunaStorage : IKizunaStorage
{
    public string DataDir { get; }

    public FileKizunaStorage(string dataDir) => DataDir = dataDir;

    public static string SafeKey(string key) => Regex.Replace(key, "[^a-zA-Z0-9_-]", "_");

    private string JalurBerkas(string key) => Path.Combine(DataDir, $"{SafeKey(key)}.json");

    public async Task<KizunaEnvelope?> LoadAsync(string key, CancellationToken ct = default)
    {
        var path = JalurBerkas(key);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var isi = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(isi))
            {
                return null;
            }

            return JsonSerializer.Deserialize<KizunaEnvelope>(isi, KizunaJson.Options);
        }
        catch (JsonException)
        {
            // Berkas rusak: perlakukan sebagai "belum ada data", sama seperti
            // aplikasi lama yang menangkap error saat load lalu lanjut.
            return null;
        }
    }

    public async Task SaveAsync(string key, KizunaEnvelope envelope, CancellationToken ct = default)
    {
        Directory.CreateDirectory(DataDir);
        var isi = JsonSerializer.Serialize(envelope, KizunaJson.Options);
        await File.WriteAllTextAsync(JalurBerkas(key), isi, ct).ConfigureAwait(false);
    }
}
