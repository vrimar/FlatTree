namespace FlatTree.Tests;

public sealed class TimeLimitTests
{
    [Test]
    [Arguments(TickResult.Success)]
    [Arguments(TickResult.Failure)]
    [Arguments(TickResult.Running)]
    public void WhileTimeLimitHasNotExpired_ReturnChildStatus(TickResult status)
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode child = new MockNode { ReturnStatus = status };
        Harness h = new Harness(
            n,
            n.TimeLimit("TimeLimit", TimeSpan.FromMilliseconds(1000), child)
        );

        h.Tick().ShouldBe(status);
        child.UpdateCallCount.ShouldBe(1);
    }

    [Test]
    [Arguments(TickResult.Success)]
    [Arguments(TickResult.Failure)]
    public void WhenTimeLimitHasExpired_ReturnFailureAndResetChild(TickResult status)
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode child = new MockNode { ReturnStatus = TickResult.Running };
        FakeClock clock = new FakeClock();
        Harness h = new Harness(
            n,
            n.TimeLimit("TimeLimit", TimeSpan.FromMilliseconds(1000), child),
            clock
        );

        h.Tick();
        clock.Advance(2000);

        h.Tick().ShouldBe(TickResult.Failure);
        child.UpdateCallCount.ShouldBe(1);

        child.ReturnStatus = status;
        h.Tick().ShouldBe(status);
        child.UpdateCallCount.ShouldBe(2);
    }

    [Test]
    public void WhenResettingWhileRunning_ReInitializeTimer()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode child = new MockNode { ReturnStatus = TickResult.Running };
        FakeClock clock = new FakeClock();
        Harness h = new Harness(
            n,
            n.TimeLimit("TimeLimit", TimeSpan.FromMilliseconds(1000), child),
            clock
        );

        h.Tick();
        clock.Advance(2000);
        h.ResetTree();

        h.Tick().ShouldBe(TickResult.Running);
    }
}
