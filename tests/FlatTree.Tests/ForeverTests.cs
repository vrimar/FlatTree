namespace FlatTree.Tests;

/// <summary>
/// <c>Forever</c> loops its child indefinitely: it never reports a terminal status, and restarts
/// the child from fresh after each completion.
/// </summary>
public sealed class ForeverTests
{
    [Test]
    [Arguments(TickResult.Success)]
    [Arguments(TickResult.Failure)]
    public void WhenChildCompletes_ReturnRunningAndResetChild(TickResult childStatus)
    {
        var n = Bt.For<FakeClock>();
        var child = new MockNode { ReturnStatus = childStatus };
        var harness = new Harness(n, n.Forever("loop", child));

        harness.Tick().ShouldBe(TickResult.Running);

        child.ResetCount.ShouldBe(1);
        harness.StatusOf(child).ShouldBe(NodeStatus.Fresh);
    }

    [Test]
    public void WhenChildIsRunning_ReturnRunningWithoutResettingChild()
    {
        var n = Bt.For<FakeClock>();
        var child = new MockNode { ReturnStatus = TickResult.Running };
        var harness = new Harness(n, n.Forever("loop", child));

        harness.Tick().ShouldBe(TickResult.Running);
        harness.Tick().ShouldBe(TickResult.Running);

        child.ResetCount.ShouldBe(0);
        child.UpdateCallCount.ShouldBe(2);
    }

    [Test]
    public void TicksTheChildExactlyOncePerTick()
    {
        var n = Bt.For<FakeClock>();
        var child = new MockNode { ReturnStatus = TickResult.Success };
        var harness = new Harness(n, n.Forever("loop", child));

        for (var i = 0; i < 5; i++)
        {
            harness.Tick().ShouldBe(TickResult.Running);
        }

        child.UpdateCallCount.ShouldBe(5);
        child.InitializeCallCount.ShouldBe(5);
    }

    [Test]
    public void NeverTerminates_SoOnTerminateNeverFires()
    {
        var n = Bt.For<FakeClock>();
        var child = new MockNode { ReturnStatus = TickResult.Success };
        var forever = n.Forever("loop", child);
        var harness = new Harness(n, forever);

        for (var i = 0; i < 10; i++)
        {
            harness.Tick();
        }

        harness.StatusOf(forever).ShouldBe(NodeStatus.Running);
    }

    [Test]
    public void ExplicitReset_StopsTheLoopAndResetsTheChild()
    {
        var n = Bt.For<FakeClock>();
        var child = new MockNode { ReturnStatus = TickResult.Running };
        var forever = n.Forever("loop", child);
        var harness = new Harness(n, forever);

        harness.Tick().ShouldBe(TickResult.Running);
        harness.ResetTree();

        harness.StatusOf(forever).ShouldBe(NodeStatus.Fresh);
        harness.StatusOf(child).ShouldBe(NodeStatus.Fresh);
    }

    [Test]
    public void AbortedByAPriorityParent_ReleasesTheChildsResources()
    {
        var n = Bt.For<RecordingClock>();
        var work = new CleanupLeaf("loop-work");
        var tree = n.Build(
            n.PrioritySelector(
                "root",
                n.Condition("guard", static (in RecordingClock c) => c.NowMs >= 1000),
                n.Forever("loop", work)
            )
        );

        var state = tree.NewState();
        var clock = new RecordingClock();

        tree.Tick(state, clock).ShouldBe(TickResult.Running);
        clock.Advance(1000);
        tree.Tick(state, clock).ShouldBe(TickResult.Success);

        clock.Released.ShouldBe(new[] { "loop-work:reset" });
    }
}
