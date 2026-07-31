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

    // Expiring tears down a Running child, which is why TimeLimit declares PreemptsRunningChildren:
    // without the reset the child keeps whatever it acquired and resumes mid-flight.
    [Test]
    public void WhenTimeLimitExpires_TheRunningChildIsResetSoItCanRelease()
    {
        var n = Bt.For<RecordingClock>();
        var work = new CleanupLeaf("channel");
        var tree = n.Build(n.TimeLimit("limit", TimeSpan.FromMilliseconds(1000), work));
        var state = tree.NewState();
        var clock = new RecordingClock();

        tree.Tick(state, clock).ShouldBe(TickResult.Running);
        clock.Released.ShouldBeEmpty();

        clock.Advance(2000);
        tree.Tick(state, clock).ShouldBe(TickResult.Failure);

        clock.Released.ShouldBe(new[] { "channel:reset" });
        state[work.Id].Status.ShouldBe(NodeStatus.Fresh);
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

    [Test]
    public void AtTheExactLimit_TheNodeHasExpired()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode child = new MockNode { ReturnStatus = TickResult.Running };
        FakeClock clock = new FakeClock();
        Harness h = new Harness(
            n,
            n.TimeLimit("TimeLimit", TimeSpan.FromMilliseconds(1000), child),
            clock
        );

        h.Tick().ShouldBe(TickResult.Running);

        clock.Advance(999);
        h.Tick().ShouldBe(TickResult.Running);
        child.UpdateCallCount.ShouldBe(2);

        clock.Advance(1);
        h.Tick().ShouldBe(TickResult.Failure);
        child.UpdateCallCount.ShouldBe(2);
    }
}
