namespace FlatTree.Tests;

/// <summary>
/// Pins the central design claim: ONE immutable tree is shared across agents, and ALL
/// per-agent mutable state lives in that agent's own <c>NodeState[]</c>. Two agents ticking
/// the same tree must never observe each other's state.
/// </summary>
public sealed class MultiAgentIsolationTests
{
    [Test]
    public void WaitStampIsPerAgentNotPerTree()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        BehaviourTree<FakeClock> tree = n.Build(n.Wait("w", TimeSpan.FromMilliseconds(1000)));

        NodeState[] a = tree.NewState();
        NodeState[] b = tree.NewState();
        FakeClock clockA = new FakeClock();
        FakeClock clockB = new FakeClock();
        clockB.Advance(500);

        // A starts its wait at t=0; B starts its wait at t=500. Same shared tree.
        tree.Tick(a, clockA).ShouldBe(TickResult.Running);
        tree.Tick(b, clockB).ShouldBe(TickResult.Running);

        clockA.Advance(1000); // A at 1000 => elapsed 1000
        clockB.Advance(400); // B at 900  => elapsed 400

        tree.Tick(a, clockA).ShouldBe(TickResult.Success);
        tree.Tick(b, clockB).ShouldBe(TickResult.Running);

        a.ShouldNotBeSameAs(b);
    }

    [Test]
    public void ResettingOneAgentDoesNotDisturbAnother()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        Wait<FakeClock> wait = n.Wait("w", TimeSpan.FromMilliseconds(1000));
        BehaviourTree<FakeClock> tree = n.Build(
            n.Sequence("seq", wait, n.Do("d", static (in FakeClock _) => TickResult.Success))
        );

        NodeState[] a = tree.NewState();
        NodeState[] b = tree.NewState();
        FakeClock clock = new FakeClock();

        // Both agents start the wait at t=0.
        tree.Tick(a, clock).ShouldBe(TickResult.Running);
        tree.Tick(b, clock).ShouldBe(TickResult.Running);

        // Reset only agent A.
        tree.Reset(a, clock);
        a.ShouldAllBe(slot => slot.Status == NodeStatus.Fresh);

        clock.Advance(2000);

        // B's wait (started at t=0) has elapsed and completes; A's wait restarts from t=2000.
        tree.Tick(b, clock).ShouldBe(TickResult.Success);
        tree.Tick(a, clock).ShouldBe(TickResult.Running);
        a[wait.Id].Stamp.ShouldBe(3000L);
    }

    [Test]
    public void CompositeCursorIsPerAgent()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        // Selector resumes from the running child; the resume cursor is per-agent.
        Selector<FakeClock> root = n.Selector(
            "sel",
            n.Do("a", static (in FakeClock _) => TickResult.Failure),
            n.Wait("hold", TimeSpan.FromMilliseconds(1000)),
            n.Do("c", static (in FakeClock _) => TickResult.Success)
        );
        BehaviourTree<FakeClock> tree = n.Build(root);

        NodeState[] a = tree.NewState();
        NodeState[] b = tree.NewState();
        FakeClock clock = new FakeClock();

        // A advances to (and parks on) the Wait at cursor 1.
        tree.Tick(a, clock).ShouldBe(TickResult.Running);
        a[root.Id].Cursor.ShouldBe(1);

        // B has never been ticked: its cursor is still 0 (fresh), unaffected by A.
        b[root.Id].Cursor.ShouldBe(0);
        b[root.Id].Status.ShouldBe(NodeStatus.Fresh);
    }
}
