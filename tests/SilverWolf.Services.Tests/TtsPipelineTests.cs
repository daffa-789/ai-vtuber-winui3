using System.Collections.Concurrent;
using System.Text;
using SilverWolf.Services.Tts;
using Xunit;

namespace SilverWolf.Services.Tests;

public sealed class TtsPipelineTests
{
    private static TaskCompletionSource<bool> Signal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Membuat WAV PCM kecil di <b>memori</b> — bentuk yang dipakai
    /// <see cref="KlipSuara"/> sejak audio tidak lagi ditulis ke disk.
    /// Isinya harus WAV sungguhan: penggabungan membaca header
    /// <c>fmt </c>/<c>data</c>, jadi isi palsu membuat penggabungan menyerah
    /// dan pemutaran jatuh ke jalur berurutan.
    /// </summary>
    private static byte[] WavDiMemori(int sampel)
    {
        var data = new byte[sampel * 2];
        using var aliran = new MemoryStream();
        using var tulis = new BinaryWriter(aliran, Encoding.ASCII);
        tulis.Write(Encoding.ASCII.GetBytes("RIFF"));
        tulis.Write(36 + data.Length);
        tulis.Write(Encoding.ASCII.GetBytes("WAVE"));
        tulis.Write(Encoding.ASCII.GetBytes("fmt "));
        tulis.Write(16);
        tulis.Write((short)1);            // PCM
        tulis.Write((short)1);            // mono
        tulis.Write(40_000);              // laju sampel
        tulis.Write(40_000 * 2);          // laju bita
        tulis.Write((short)2);            // perataan blok
        tulis.Write((short)16);           // bit per sampel
        tulis.Write(Encoding.ASCII.GetBytes("data"));
        tulis.Write(data.Length);
        tulis.Write(data);
        tulis.Flush();
        return aliran.ToArray();
    }

    /// <summary>Klip sungguhan yang bisa digabung.</summary>
    private static KlipSuara Klip(int sampel = 800) => new(WavDiMemori(sampel), "uji");

    /// <summary>
    /// Klip berlabel teks tetapi isinya BUKAN WAV — dipakai pada uji yang
    /// sengaja ingin penggabungan gagal sehingga pemutaran berurutan.
    /// </summary>
    private static KlipSuara KlipPalsu(string label) =>
        new(Encoding.UTF8.GetBytes(label), label);

    [Fact]
    public async Task WholeReplyIsSynthesizedBeforeAnyPlayback()
    {
        // Kontrak 2026-10-09. Sintesis jauh lebih lambat daripada pemutaran
        // (RVC di CPU 6-13 dtk per kalimat, audionya 2-3 dtk), jadi memutar
        // sambil menyintesis membuat pemutar kehabisan bahan dan diam di setiap
        // tanda baca. Seluruh kalimat harus selesai dulu, lalu digabung menjadi
        // SATU berkas supaya perangkat audio hanya dibuka sekali.
        var disintesis = new ConcurrentQueue<string>();
        var diputar = new ConcurrentQueue<string>();

        using var pipeline = new TtsPipeline((text, _) =>
        {
            disintesis.Enqueue(text);
            return Task.FromResult<KlipSuara?>(Klip());
        }, (klip, _, started) =>
        {
            // Pemutaran pertama hanya boleh mulai setelah KEDUA kalimat selesai.
            Assert.Equal(2, disintesis.Count);
            diputar.Enqueue(klip.Label);
            started();
            return Task.FromResult(true);
        });

        Assert.True(await pipeline.SiapkanDanPutarAsync(
            "Kalimat pertama selesai. Kalimat kedua selesai.").WaitAsync(Limit));
        await pipeline.Penyelesaian.WaitAsync(Limit);

        Assert.Equal(2, disintesis.Count);
        // Dua klip berformat sama digabung di memori jadi SATU pemutaran.
        Assert.Single(diputar);
        Assert.False(pipeline.Sibuk);
    }

    [Fact]
    public async Task ProgressIsReportedPerSentence()
    {
        // UI memakai ini untuk menampilkan "menyiapkan suara… (2/3)"; tanpa
        // laporan kemajuan gelembung tampak menggantung selama seluruh sintesis.
        var laporan = new ConcurrentQueue<(int Selesai, int Total)>();
        using var pipeline = new TtsPipeline(
            (text, _) => Task.FromResult<KlipSuara?>(KlipPalsu(text)),
            (_, _, started) => { started(); return Task.FromResult(true); });

        await pipeline.SiapkanDanPutarAsync(
            "Satu selesai. Dua selesai. Tiga selesai.",
            progres: (selesai, total) => laporan.Enqueue((selesai, total))).WaitAsync(Limit);
        await pipeline.Penyelesaian.WaitAsync(Limit);

        Assert.Equal(new[] { (1, 3), (2, 3), (3, 3) }, laporan.ToArray());
    }

    [Fact]
    public async Task PreparationIsTrackedAndCancellationIsNotReadiness()
    {
        var entered = Signal();
        var stopped = Signal();
        using var pipeline = new TtsPipeline(async (_, ct) =>
        {
            entered.TrySetResult(true);
            try { await Task.Delay(Timeout.Infinite, ct); }
            finally { stopped.TrySetResult(true); }
            return (KlipSuara?)Klip(10);
        }, (_, _, _) => throw new InvalidOperationException("Must not play"));
        var ready = pipeline.SiapkanDanPutarAsync("Balasan menunggu suara.");
        await entered.Task.WaitAsync(Limit);
        Assert.True(pipeline.Sibuk);
        pipeline.Hentikan();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ready.WaitAsync(Limit));
        await pipeline.Penyelesaian.WaitAsync(Limit);
        await stopped.Task.WaitAsync(Limit);
        Assert.False(pipeline.Sibuk);
    }

    [Fact]
    public async Task FailedSentenceDoesNotDiscardFollowingAudioOrTrailingText()
    {
        var made = new ConcurrentQueue<string>();
        var played = new ConcurrentQueue<string>();
        using var pipeline = new TtsPipeline((text, _) =>
        {
            made.Enqueue(text);
            // Klip palsu sengaja: isinya bukan WAV, jadi penggabungan gagal dan
            // kedua kalimat diputar BERURUTAN — itu yang ingin dibuktikan di sini.
            return Task.FromResult<KlipSuara?>(
                text.StartsWith("Kalimat pertama") ? null : KlipPalsu(text));
        }, (klip, _, started) =>
        {
            played.Enqueue(klip.Label);
            started();
            return Task.FromResult(true);
        });
        Assert.True(await pipeline.SiapkanDanPutarAsync(
            "Kalimat pertama gagal. Kalimat kedua berhasil. Sisa tanpa tanda").WaitAsync(Limit));
        await pipeline.Penyelesaian.WaitAsync(Limit);
        Assert.Equal(3, made.Count);
        Assert.Equal(new[] { "Kalimat kedua berhasil.", "Sisa tanpa tanda" }, played.ToArray());
    }

    [Fact]
    public async Task TotalSynthesisFailureReturnsFalse()
    {
        using var pipeline = new TtsPipeline(
            (_, _) => Task.FromResult<KlipSuara?>(null),
            (_, _, _) => throw new InvalidOperationException("Must not play"));
        Assert.False(await pipeline.SiapkanDanPutarAsync("Tidak ada WAV tersedia.").WaitAsync(Limit));
        await pipeline.Penyelesaian.WaitAsync(Limit);
        Assert.False(pipeline.Sibuk);
    }

    [Fact]
    public async Task PlaybackFailureBeforeStartDoesNotReportReady()
    {
        using var pipeline = new TtsPipeline(
            (text, _) => Task.FromResult<KlipSuara?>(KlipPalsu(text)),
            (_, _, _) => Task.FromResult(false));
        Assert.False(await pipeline.SiapkanDanPutarAsync("Perangkat gagal dibuka.").WaitAsync(Limit));
        await pipeline.Penyelesaian.WaitAsync(Limit);
    }

    [Fact]
    public async Task ReplacementWaitsForOldSynthesisToStop()
    {
        var oldEntered = Signal();
        var oldStopped = Signal();
        using var pipeline = new TtsPipeline(async (text, ct) =>
        {
            if (text.StartsWith("Balasan lama"))
            {
                oldEntered.TrySetResult(true);
                try { await Task.Delay(Timeout.Infinite, ct); }
                finally { oldStopped.TrySetResult(true); }
            }
            else Assert.True(oldStopped.Task.IsCompleted);
            return (KlipSuara?)KlipPalsu(text);
        }, (_, _, started) => { started(); return Task.FromResult(true); });
        var oldReady = pipeline.SiapkanDanPutarAsync("Balasan lama menunggu.");
        await oldEntered.Task.WaitAsync(Limit);
        Assert.True(await pipeline.SiapkanDanPutarAsync("Balasan baru tersedia.").WaitAsync(Limit));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => oldReady.WaitAsync(Limit));
        await pipeline.Penyelesaian.WaitAsync(Limit);
    }

    [Fact]
    public async Task DisposeCancelsPreparationAndDefersPlayerDisposal()
    {
        var entered = Signal();
        var disposed = Signal();
        var stopped = false;
        var pipeline = new TtsPipeline(async (_, ct) =>
        {
            entered.TrySetResult(true);
            try { await Task.Delay(Timeout.Infinite, ct); }
            finally { stopped = true; }
            return (KlipSuara?)Klip(10);
        }, (_, _, _) => Task.FromResult(true), () =>
        {
            Assert.True(stopped);
            disposed.TrySetResult(true);
        });
        var ready = pipeline.SiapkanDanPutarAsync("Persiapan saat penutupan.");
        await entered.Task.WaitAsync(Limit);
        pipeline.Dispose();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ready.WaitAsync(Limit));
        await disposed.Task.WaitAsync(Limit);
        await pipeline.Penyelesaian.WaitAsync(Limit);
    }

    [Fact]
    public async Task EmptyReplyCreatesNoWork()
    {
        using var pipeline = new TtsPipeline(
            (_, _) => throw new InvalidOperationException("Must not synthesize"),
            (_, _, _) => throw new InvalidOperationException("Must not play"));

        Assert.False(await pipeline.SiapkanDanPutarAsync(" ").WaitAsync(Limit));
        Assert.False(pipeline.Sibuk);
    }
}
