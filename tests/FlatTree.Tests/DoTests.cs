namespace FlatTree.Tests;

public sealed class DoTests
{
    [Test]
    public void ReturnsSuccess()
    {
        var n = Bt.For<FakeClock>();
        var h = new Harness(n, n.Do("Do", static (in FakeClock _) => TickResult.Success));

        h.Tick().ShouldBe(TickResult.Success);
    }

    [Test]
    public void ReturnsFailure()
    {
        var n = Bt.For<FakeClock>();
        var h = new Harness(n, n.Do("Do", static (in FakeClock _) => TickResult.Failure));

        h.Tick().ShouldBe(TickResult.Failure);
    }

    [Test]
    public void ReturnsRunning()
    {
        var n = Bt.For<FakeClock>();
        var h = new Harness(n, n.Do("Do", static (in FakeClock _) => TickResult.Running));

        h.Tick().ShouldBe(TickResult.Running);
    }

    [Test]
    public void ActionReadsContextClock()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        FakeClock clock = new FakeClock();
        Harness h = new Harness(
            n,
            n.Do("Do", static (in FakeClock c) => c.NowMs >= 1000 ? TickResult.Success : TickResult.Failure),
            clock
        );

        h.Tick().ShouldBe(TickResult.Failure);
        clock.Advance(1000);
        h.Tick().ShouldBe(TickResult.Success);
    }

    [Test]
    public void TheActionReceivesTheAuthoredState()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        Do<FakeClock, TickResult> sut = n.Do("scripted", TickResult.Running, Echo);
        Harness h = new Harness(n, sut);

        h.Tick().ShouldBe(TickResult.Running);
        sut.State.ShouldBe(TickResult.Running);
        sut.Name.ShouldBe("scripted");
    }

    [Test]
    public void TheNamelessOverloadDefaultsTheName()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        Do<FakeClock, TickResult> sut = n.Do(TickResult.Failure, Echo);
        Harness h = new Harness(n, sut);

        h.Tick().ShouldBe(TickResult.Failure);
        sut.Name.ShouldBe("Do");
    }

    private static TickResult Echo(in FakeClock c, in TickResult scripted) => scripted;
}
