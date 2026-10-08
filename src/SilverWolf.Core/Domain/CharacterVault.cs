using System.Globalization;
using System.Text.RegularExpressions;

namespace SilverWolf.Core.Domain;

/// <summary>
/// Gudang memori jangka panjang — port <c>CharacterVault</c> dari
/// <c>apps/server-node/src/character.js</c>.
///
/// Berkas yang ditulis (Fakta.md, Mood.md, Riwayat/YYYY-MM-DD.md) berformat
/// Markdown dengan front-matter. Formatnya harus tetap identik: berkas-berkas
/// ini sudah ada di mesin pengguna dari aplikasi Electron dan dibaca balik oleh
/// aplikasi baru.
///
/// Penulisan memakai <b>temp + atomic rename</b> seperti aslinya, supaya berkas
/// tidak pernah terbaca setengah jadi kalau aplikasi mati di tengah tulis.
/// </summary>
public sealed class CharacterVault
{
    private const string Tautan = "silverwolf-persona";

    public string Root { get; }

    public CharacterVault(string root) => Root = root;

    public bool Available() => Directory.Exists(Root);

    public string UnavailableReason() => $"folder {Root} tidak ada";

    public async Task<string?> BacaAsync(string nama, CancellationToken ct = default)
    {
        try
        {
            return await File.ReadAllTextAsync(Path.Combine(Root, nama), ct).ConfigureAwait(false);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }

    public async Task TulisAsync(string nama, string isi, CancellationToken ct = default)
    {
        var path = Path.Combine(Root, nama);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var temp = $"{path}.{Environment.ProcessId}.{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.tmp";
        await File.WriteAllTextAsync(temp, isi, ct).ConfigureAwait(false);
        File.Move(temp, path, overwrite: true);
    }

    /// <summary>Port <c>kerangka()</c>: front-matter + judul + isi.</summary>
    public static string Kerangka(string nama, string judul, string isi, IEnumerable<string>? links = null)
    {
        var semua = new List<string> { Tautan };
        if (links is not null)
        {
            semua.AddRange(links);
        }

        var unik = semua.Distinct(StringComparer.Ordinal).ToList();

        var baris = new List<string>
        {
            "---",
            "type: memory",
            "kind: karakter",
            "wilayah: waifu",
            $"name: \"{nama}\"",
            $"description: \"{judul}\"",
            "project: \"Desktop AI VTUBER\"",
            $"updated: \"{Tanggal()}\"",
            "tags:",
            "  - \"memory/karakter\"",
            "  - \"wilayah/waifu\"",
            "  - \"project/Desktop AI VTUBER\"",
            "links:",
        };

        baris.AddRange(unik.Select(x => $"  - \"[[{x}]]\""));
        baris.AddRange(["---", string.Empty, $"# {judul}", string.Empty, isi.Trim(), string.Empty]);

        return string.Join('\n', baris);
    }

    /// <summary>Tanggal UTC <c>YYYY-MM-DD</c> — port <c>tanggal()</c>.</summary>
    public static string Tanggal(DateTime? utc = null) =>
        (utc ?? DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Port <c>bacaFakta()</c>: baris berawalan "- ", abaikan baris "_".</summary>
    public async Task<List<string>> BacaFaktaAsync(CancellationToken ct = default)
    {
        var teks = await BacaAsync("Fakta.md", ct).ConfigureAwait(false);
        if (teks is null)
        {
            return [];
        }

        return
        [
            .. teks
                .Split('\n')
                .Where(x => x.StartsWith("- ", StringComparison.Ordinal))
                .Select(x => x[2..].Trim())
                .Where(x => x.Length != 0 && !x.StartsWith('_')),
        ];
    }

    public async Task SimpanFaktaAsync(IEnumerable<string> fakta, CancellationToken ct = default)
    {
        const string panduan =
            "Setiap baris di bawah masuk ke prompt sebagai sesuatu yang **dia ingat benar**.\n" +
            "Hanya simpan yang pernah Master tulis sendiri atau yang terukur dari mesin ini.\n\n";

        var daftar = fakta.ToList();
        var isi = panduan + (daftar.Count != 0
            ? string.Join('\n', daftar.Select(x => $"- {x}"))
            : "_Belum ada fakta tersimpan._");

        await TulisAsync(
            "Fakta.md",
            Kerangka("fakta-silverwolf", "Fakta yang Silver Wolf ingat tentang Master", isi, ["Mood", "Riwayat"]),
            ct).ConfigureAwait(false);
    }

    /// <summary>Port <c>bacaMood()</c>. Null bika berkas tidak ada atau angkanya tidak lengkap.</summary>
    public async Task<Mood?> BacaMoodAsync(CancellationToken ct = default)
    {
        var teks = await BacaAsync("Mood.md", ct).ConfigureAwait(false);
        if (teks is null)
        {
            return null;
        }

        static double Ambil(string teks, string kunci)
        {
            var m = Regex.Match(teks, $@"{kunci}: (-?[\d.]+)");
            return m.Success && double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
                ? v
                : double.NaN;
        }

        var valensi = Ambil(teks, "Valensi");
        var energi = Ambil(teks, "Energi");
        var afinitas = Ambil(teks, "Afinitas");

        if (!double.IsFinite(valensi) || !double.IsFinite(energi) || !double.IsFinite(afinitas))
        {
            return null;
        }

        var pertukaran = Regex.Match(teks, @"Pertukaran tercatat: (\d+)");
        var jumlah = pertukaran.Success
            && int.TryParse(pertukaran.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var p)
            ? p
            : 0;

        return new Mood
        {
            Valensi = valensi,
            Energi = energi,
            Afinitas = afinitas,
            Pertukaran = jumlah,
        };
    }

    public async Task SimpanMoodAsync(Mood mood, CancellationToken ct = default)
    {
        var baris = new List<string>
        {
            $"Valensi: {mood.Valensi.ToString("0.00", CultureInfo.InvariantCulture)} (-1 berat .. +1 senang)",
            $"Energi: {mood.Energi.ToString("0.00", CultureInfo.InvariantCulture)}",
            $"Afinitas: {mood.Afinitas.ToString("0.00", CultureInfo.InvariantCulture)} (0 jauh .. 1 dekat)",
            $"Pertukaran tercatat: {mood.Pertukaran}",
            $"Terakhir diperbarui: {DateTime.UtcNow:o}",
        };

        if (!string.IsNullOrEmpty(mood.Alasan))
        {
            baris.Add($"Alasan: {mood.Alasan}");
        }

        await TulisAsync(
            "Mood.md",
            Kerangka("mood-silverwolf", "Suasana hati Silver Wolf saat ini", string.Join('\n', baris), ["Fakta", "Riwayat"]),
            ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Port baru: baca <c>Profil.md</c> — fakta terstruktur tentang Master yang
    /// butuh hitungan (tanggal lahir untuk ulang tahun &amp; umur).
    /// Null bila berkasnya tidak ada atau tidak memuat satu pun kunci yang dikenal.
    /// </summary>
    public async Task<MasterProfile?> BacaProfilAsync(CancellationToken ct = default) =>
        MasterProfile.Parse(await BacaAsync(MasterProfile.Berkas, ct).ConfigureAwait(false));

    /// <summary>Tulis <c>Profil.md</c> memakai kerangka front-matter yang sama.</summary>
    public async Task SimpanProfilAsync(MasterProfile profil, CancellationToken ct = default) =>
        await TulisAsync(
            MasterProfile.Berkas,
            Kerangka(
                "profil-master",
                "Profil Master yang diingat Silver Wolf",
                profil.KeTeks(),
                ["Fakta", "Mood", "Riwayat"]),
            ct).ConfigureAwait(false);

    /// <summary>Port <c>catatHari(baris)</c>: tambahkan satu baris ke Riwayat/hari-ini.md.</summary>
    public async Task CatatHariAsync(string baris, CancellationToken ct = default)
    {
        var hari = Tanggal();
        var nama = $"Riwayat/{hari}.md";
        var lama = await BacaAsync(nama, ct).ConfigureAwait(false);

        var badan = (lama ?? string.Empty)
            .Split('\n')
            .Where(x => x.StartsWith("- ", StringComparison.Ordinal))
            .ToList();

        badan.Add($"- {baris}");

        await TulisAsync(
            nama,
            Kerangka($"riwayat-{hari}", $"Riwayat percakapan {hari}", string.Join('\n', badan), ["Fakta", "Mood", "Riwayat"]),
            ct).ConfigureAwait(false);
    }
}
