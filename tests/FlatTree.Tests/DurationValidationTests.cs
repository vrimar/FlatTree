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

    [Test]
    public void DurationProperty_RoundTripsTheConfiguredValue()
    {
        var n = Bt.For<FakeClock>();

        n.Wait("w", TimeSpan.FromSeconds(2)).Duration.ShouldBe(TimeSpan.FromSeconds(2));
        n.Cooldown("c", TimeSpan.FromSeconds(3), new MockNode())
            .Duration.ShouldBe(TimeSpan.FromSeconds(3));
    }
}
