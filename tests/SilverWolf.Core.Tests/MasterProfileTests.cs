using SilverWolf.Core.Domain;

namespace SilverWolf.Core.Tests;

/// <summary>
/// Uji baca/tulis <c>Profil.md</c>. Bentuk berkasnya harus tetap bisa dibaca
/// meski disisipi front-matter dan penjelasan — berkas ini ada di mesin
/// pengguna dan bisa disunting manual.
/// </summary>
public class MasterProfileTests
{
    private const string ContohBerkas =
        """
        ---
        type: memory
        name: "profil-master"
        ---

        # Profil Master yang diingat Silver Wolf

        Fakta terstruktur tentang Master — bukan teks bebas.

        Tanggal lahir: 2005-10-16
        Nama panggilan: Master
        """;

    [Fact]
    public void Parse_MembacaTanggalLahirDariBerkasBerfrontMatter()
    {
        var profil = MasterProfile.Parse(ContohBerkas);

        Assert.NotNull(profil);
        Assert.Equal(new DateOnly(2005, 10, 16), profil.Lahir);
        Assert.Equal("Master", profil.Panggilan);
    }

    [Fact]
    public void Parse_TeksKosongAtauNullMenghasilkanNull()
    {
        Assert.Null(MasterProfile.Parse(null));
        Assert.Null(MasterProfile.Parse(string.Empty));
        Assert.Null(MasterProfile.Parse("   "));
    }

    [Fact]
    public void Parse_TeksTanpaKunciDikenalMenghasilkanNull()
    {
        // Berkas ada tetapi tidak memuat apa pun yang bisa dipakai — pemanggil
        // harus bisa membedakan ini dari "berkas tidak ada".
        Assert.Null(MasterProfile.Parse("# Judul\n\nIsi bebas tanpa kunci."));
    }

    [Fact]
    public void Parse_TanggalTidakValidDiabaikanBukanMelempar()
    {
        var profil = MasterProfile.Parse("Tanggal lahir: 16 Oktober 2005\nNama panggilan: Master");

        Assert.NotNull(profil);
        Assert.Null(profil.Lahir);          // formatnya bukan ISO
        Assert.Equal("Master", profil.Panggilan);
    }

    [Fact]
    public void Parse_TandaStripBerartiBelumDiisi()
    {
        var profil = MasterProfile.Parse("Tanggal lahir: -\nNama panggilan: -");

        Assert.Null(profil);
    }

    [Fact]
    public void KeTeks_DanParse_BolakBalik()
    {
        var asli = new MasterProfile { Lahir = new DateOnly(2005, 10, 16), Panggilan = "Master" };

        var balik = MasterProfile.Parse(asli.KeTeks());

        Assert.NotNull(balik);
        Assert.Equal(asli.Lahir, balik.Lahir);
        Assert.Equal(asli.Panggilan, balik.Panggilan);
    }

    [Fact]
    public void KeTeks_TanpaIsiMenulisTandaStrip()
    {
        Assert.Contains("Tanggal lahir: -", new MasterProfile().KeTeks(), StringComparison.Ordinal);
    }
}
