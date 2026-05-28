namespace FlatTree.Tests;

public sealed class DoTests
{
    [Test]
    public void ReturnsSuccess()
    {
        var n = Bt.For<FakeClock>();
        var h = new Harness(n, n.Do("Do", static _ => TickResult.Success));

        h.Tick().ShouldBe(TickResult.Success);
    }

    [Test]
    public void ReturnsFailure()
    {
        var n = Bt.For<FakeClock>();
        var h = new Harness(n, n.Do("Do", static _ => TickResult.Failure));

        h.Tick().ShouldBe(TickResult.Failure);
    }

    [Test]
    public void ReturnsRunning()
    {
        var n = Bt.For<FakeClock>();
        var h = new Harness(n, n.Do("Do", static _ => TickResult.Running));

        h.Tick().ShouldBe(TickResult.Running);
    }

    [Test]
    public void ActionReadsContextClock()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        FakeClock clock = new FakeClock();
        Harness h = new Harness(
            n,
            n.Do("Do", static c => c.NowMs >= 1000 ? TickResult.Success : TickResult.Failure),
            clock
        );

        h.Tick().ShouldBe(TickResult.Failure);
        clock.Advance(1000);
        h.Tick().ShouldBe(TickResult.Success);
    }
}
