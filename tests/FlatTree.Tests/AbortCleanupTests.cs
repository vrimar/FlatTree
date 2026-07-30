namespace FlatTree.Tests;

/// <summary>
/// The teardown hooks receive the context, so a node preempted by a reactive parent can release
/// what it acquired. Without this, an abort silently strands state shared with other agents.
/// </summary>
public sealed class AbortCleanupTests
{
    private static bool GuardOpen(in RecordingClock c) => c.NowMs >= 1000;

    [Test]
    public void PrioritySelector_AbortingARunningBranch_ReachesTheLeafWithALiveContext()
    {
        var n = Bt.For<RecordingClock>();
        var work = new CleanupLeaf("trade-window");
        var tree = n.Build(
            n.PrioritySelector(
                "root",
                n.Sequence("high", n.Condition("guard", GuardOpen), n.Do("act", Succeed)),
                n.Sequence("low", work)
            )
        );

        var state = tree.NewState();
        var clock = new RecordingClock();

        tree.Tick(state, clock).ShouldBe(TickResult.Running);
        state[work.Id].Status.ShouldBe(NodeStatus.Running);
        clock.Released.ShouldBeEmpty();

        clock.Advance(1000);
        tree.Tick(state, clock).ShouldBe(TickResult.Success);

        clock.Released.ShouldBe(new[] { "trade-window:reset" });
        state[work.Id].Status.ShouldBe(NodeStatus.Fresh);
    }

    [Test]
    public void ExplicitTreeReset_ReachesADeeplyNestedRunningLeaf()
    {
        var n = Bt.For<RecordingClock>();
        var work = new CleanupLeaf("barrier");
        var tree = n.Build(
            n.Selector("root", n.Sequence("outer", n.AlwaysSucceed("wrap", n.Sequence("inner", work))))
        );

        var state = tree.NewState();
        var clock = new RecordingClock();

        tree.Tick(state, clock).ShouldBe(TickResult.Running);
        tree.Reset(state, clock);

        clock.Released.ShouldBe(new[] { "barrier:reset" });
    }

    [Test]
    public void Reset_SkipsFreshNodes_SoAnUntouchedBranchIsNeverNotified()
    {
        var n = Bt.For<RecordingClock>();
        var touched = new CleanupLeaf("touched");
        var untouched = new CleanupLeaf("untouched");
        var tree = n.Build(n.Selector("root", n.Sequence("a", touched), n.Sequence("b", untouched)));

        var state = tree.NewState();
        var clock = new RecordingClock();

        tree.Tick(state, clock).ShouldBe(TickResult.Running);
        state[untouched.Id].Status.ShouldBe(NodeStatus.Fresh);

        tree.Reset(state, clock);

        clock.Released.ShouldBe(new[] { "touched:reset" });
    }

    [Test]
    public void OnTerminate_DoesNotFireWhileRunning()
    {
        var n = Bt.For<RecordingClock>();
        var work = new CleanupLeaf("job");
        var tree = n.Build(n.Sequence("root", work));

        var state = tree.NewState();
        var clock = new RecordingClock();

        tree.Tick(state, clock);
        tree.Tick(state, clock);
        tree.Tick(state, clock);

        clock.Released.ShouldBeEmpty();
    }

    [Test]
    public void OnTerminate_FiresWithTheContextWhenTheLeafCompletes()
    {
        var n = Bt.For<RecordingClock>();
        var work = new CleanupLeaf("job", TickResult.Success);
        var tree = n.Build(n.Sequence("root", work));

        var state = tree.NewState();
        var clock = new RecordingClock();

        tree.Tick(state, clock).ShouldBe(TickResult.Success);

        clock.Released.ShouldContain("job:terminate");
    }

    [Test]
    public void PrioritySequence_AbortingALowerPriorityBranch_ReachesTheLeaf()
    {
        var n = Bt.For<RecordingClock>();
        var work = new CleanupLeaf("commit");
        var tree = n.Build(
            n.PrioritySequence(
                "root",
                n.Condition("guard", static (in RecordingClock c) => c.NowMs < 1000),
                n.Sequence("low", work)
            )
        );

        var state = tree.NewState();
        var clock = new RecordingClock();

        tree.Tick(state, clock).ShouldBe(TickResult.Running);
        clock.Released.ShouldBeEmpty();

        clock.Advance(1000);
        tree.Tick(state, clock).ShouldBe(TickResult.Failure);

        clock.Released.ShouldBe(new[] { "commit:reset" });
    }

    [Test]
    public void PoolReset_ReachesTheLeafWithALiveContext()
    {
        var n = Bt.For<RecordingClock>();
        var work = new CleanupLeaf("slot-work");
        var tree = n.Build(n.Sequence("root", work));
        var pool = new BehaviourTreePool<RecordingClock>(tree, 2);
        var clock = new RecordingClock();

        var slot = pool.Rent();
        pool.Tick(slot, clock).ShouldBe(TickResult.Running);
        pool.Reset(slot, clock);

        clock.Released.ShouldBe(new[] { "slot-work:reset" });
        pool.StatusOf(slot, work).ShouldBe(NodeStatus.Fresh);
    }

    private static TickResult Succeed(in RecordingClock c) => TickResult.Success;
}
