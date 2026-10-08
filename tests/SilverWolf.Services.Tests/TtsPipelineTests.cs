using System.Collections.Concurrent;
using SilverWolf.Services.Tts;
using Xunit;

namespace SilverWolf.Services.Tests;

public sealed class TtsPipelineTests
{
    private static TaskCompletionSource<bool> Signal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task FirstPlaybackDoesNotWaitForSecondSynthesis()
    {
        var secondEntered = Signal();
        var releaseSecond = Signal();
        var releasePlayback = Signal();
        var played = new ConcurrentQueue<string>();
        using var pipeline = new TtsPipeline(async (text, ct) =>
        {
            if (text.StartsWith("Kalimat kedua"))
            {
                secondEntered.TrySetResult(true);
                await releaseSecond.Task.WaitAsync(ct);
            }
            return text;
        }, async (wav, ct, started) =>
        {
            played.Enqueue(wav);
            started();
            if (wav.StartsWith("Kalimat pertama"))
                await releasePlayback.Task.WaitAsync(ct);
            return true;
        });
        try
        {
            Assert.True(await pipeline.SiapkanDanPutarAsync(
                "Kalimat pertama selesai. Kalimat kedua selesai.").WaitAsync(Limit));
            await secondEntered.Task.WaitAsync(Limit);
            Assert.True(pipeline.Sibuk);
            Assert.Single(played);
            releaseSecond.TrySetResult(true);
            releasePlayback.TrySetResult(true);
            await pipeline.Penyelesaian.WaitAsync(Limit);
            Assert.Equal(2, played.Count);
            Assert.False(pipeline.Sibuk);
        }
        finally { releaseSecond.TrySetResult(true); releasePlayback.TrySetResult(true); }
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
            return "never.wav";
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
            return Task.FromResult<string?>(text.StartsWith("Kalimat pertama") ? null : text);
        }, (wav, _, started) =>
        {
            played.Enqueue(wav);
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
            (_, _) => Task.FromResult<string?>(null),
            (_, _, _) => throw new InvalidOperationException("Must not play"));
        Assert.False(await pipeline.SiapkanDanPutarAsync("Tidak ada WAV tersedia.").WaitAsync(Limit));
        await pipeline.Penyelesaian.WaitAsync(Limit);
        Assert.False(pipeline.Sibuk);
    }

    [Fact]
    public async Task PlaybackFailureBeforeStartDoesNotReportReady()
    {
        using var pipeline = new TtsPipeline(
            (text, _) => Task.FromResult<string?>(text),
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
            return text;
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
            return "never.wav";
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
