// Satu-satunya global using di proyek ini. Sengaja ditulis eksplisit (bukan
// lewat <Using> di csproj) supaya pembaca langsung melihat dari mana [Fact]
// dan [Theory] berasal — proyek uji tidak punya implicit using untuk Xunit.
global using Xunit;
