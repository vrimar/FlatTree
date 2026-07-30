namespace FlatTree.Tests;

/// <summary>F2: time-based nodes reject nonsensical durations at construction time.</summary>
public sealed class DurationValidationTests
{
    [Test]
    public void Wait_RejectsNegativeDuration()
    {
        var n = Bt.For<FakeClock>();

        Should.Throw<ArgumentOutOfRangeException>(() => n.Wait("w", TimeSpan.FromMilliseconds(-1)));
    }

    [Test]
    public void Wait_AllowsZeroDuration()
    {
        var n = Bt.For<FakeClock>();

        Should.NotThrow(() => n.Wait("w", TimeSpan.Zero));
    }

    [Test]
    [Arguments(0)]
    [Arguments(-1)]
    public void Cooldown_RejectsNonPositiveDuration(int ms)
    {
        var n = Bt.For<FakeClock>();
        var child = new MockNode();

        Should.Throw<ArgumentOutOfRangeException>(() =>
            n.Cooldown("c", TimeSpan.FromMilliseconds(ms), child)
        );
    }

    [Test]
    [Arguments(0)]
    [Arguments(-1)]
    public void RateLimiter_RejectsNonPositiveInterval(int ms)
    {
        var n = Bt.For<FakeClock>();
        var child = new MockNode();

        Should.Throw<ArgumentOutOfRangeException>(() =>
            n.RateLimiter("r", TimeSpan.FromMilliseconds(ms), child)
        );
    }

    [Test]
    [Arguments(0)]
    [Arguments(-1)]
    public void TimeLimit_RejectsNonPositiveLimit(int ms)
    {
        var n = Bt.For<FakeClock>();
        var child = new MockNode();

        Should.Throw<ArgumentOutOfRangeException>(() =>
            n.TimeLimit("t", TimeSpan.FromMilliseconds(ms), child)
        );
    }

    // A sub-millisecond duration truncates to 0 ms, which reads as "already elapsed": Cooldown and
    // RateLimiter would silently become no-ops and TimeLimit an unconditional Failure that never
    // ticks its child. Reject it rather than shipping a node that does nothing.
    [Test]
    public void Cooldown_RejectsSubMillisecondDuration()
    {
        var n = Bt.For<FakeClock>();

        Should.Throw<ArgumentOutOfRangeException>(() =>
            n.Cooldown("c", TimeSpan.FromMicroseconds(500), new MockNode())
        );
    }

    [Test]
    public void RateLimiter_RejectsSubMillisecondInterval()
    {
        var n = Bt.For<FakeClock>();

        Should.Throw<ArgumentOutOfRangeException>(() =>
            n.RateLimiter("r", TimeSpan.FromMicroseconds(500), new MockNode())
        );
    }

    [Test]
    public void TimeLimit_RejectsSubMillisecondLimit()
    {
        var n = Bt.For<FakeClock>();

        Should.Throw<ArgumentOutOfRangeException>(() =>
            n.TimeLimit("t", TimeSpan.FromMicroseconds(500), new MockNode())
        );
    }

    [Test]
    public void OneMillisecondIsTheSmallestAcceptedDuration()
    {
        var n = Bt.For<FakeClock>();

        Should.NotThrow(() => n.Cooldown("c", TimeSpan.FromMilliseconds(1), new MockNode()));
        Should.NotThrow(() => n.RateLimiter("r", TimeSpan.FromMilliseconds(1), new MockNode()));
        Should.NotThrow(() => n.TimeLimit("t", TimeSpan.FromMilliseconds(1), new MockNode()));
    }

    [Test]
    public void Wait_WithZeroDuration_SucceedsOnTheFirstTick()
    {
        var n = Bt.For<FakeClock>();
        var h = new Harness(n, n.Wait("w", TimeSpan.Zero));

        h.Tick().ShouldBe(TickResult.Success);
    }

    [Test]
    public void DurationProperty_RoundTripsTheConfiguredValue()
    {
        var n = Bt.For<FakeClock>();

        n.Wait("w", TimeSpan.FromSeconds(2)).Duration.ShouldBe(TimeSpan.FromSeconds(2));
        n.Cooldown("c", TimeSpan.FromSeconds(3), new MockNode())
            .Duration.ShouldBe(TimeSpan.FromSeconds(3));
        n.RateLimiter("r", TimeSpan.FromSeconds(4), new MockNode())
            .Interval.ShouldBe(TimeSpan.FromSeconds(4));
        n.TimeLimit("t", TimeSpan.FromSeconds(5), new MockNode())
            .Limit.ShouldBe(TimeSpan.FromSeconds(5));
    }
}
