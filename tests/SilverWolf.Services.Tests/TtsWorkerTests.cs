using SilverWolf.Services.Tts;
using Xunit;

namespace SilverWolf.Services.Tests;

public sealed class TtsWorkerTests
{
    [Theory]
    [InlineData(8, 8, true)]
    [InlineData(8, 2, false)]
    [InlineData(8, 0, false)]
    [InlineData(0, 0, false)]
    [InlineData(3, 3, false)]
    public void CacheRequiresCompleteAlignedAudio(int declared, int actual, bool expected)
    {
        var path = Path.Combine(Path.GetTempPath(), $"sw-wav-test-{Guid.NewGuid():N}.wav");
        try
        {
            using (var file = File.Create(path))
            using (var writer = new BinaryWriter(file))
            {
                writer.Write("RIFF"u8.ToArray()); writer.Write(36 + declared);
                writer.Write("WAVEfmt "u8.ToArray()); writer.Write(16);
                writer.Write((short)1); writer.Write((short)1);
                writer.Write(16000); writer.Write(32000);
                writer.Write((short)2); writer.Write((short)16);
                writer.Write("data"u8.ToArray()); writer.Write(declared);
                writer.Write(new byte[actual]);
            }
            Assert.Equal(expected, TtsWorker.WavSah(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void MissingCacheIsInvalid() => Assert.False(TtsWorker.WavSah(
        Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.wav")));
}
