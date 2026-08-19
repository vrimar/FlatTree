namespace FlatTree.Tests;

/// <summary>
/// End-to-end coverage of pitfall #3: a priority composite re-evaluates from index 0 every
/// tick and must explicitly reset lower-priority siblings (j &gt; i) when a higher-priority
/// child becomes non-failed — even when the composite itself returns Running (so no terminal
/// reset masks the behaviour). A lower-priority subtree carrying stateful, mid-progress nodes
/// (here a <c>Wait</c>) must be cleared so it restarts cleanly when next selected.
/// </summary>
public sealed class ResetCascadeTests
{
    // High-priority child is "active" (Running) only inside the [100, 200) window.
    private static TickResult HighRunningInWindowElseFailure(in FakeClock c) =>
        c.NowMs is >= 100 and < 200 ? TickResult.Running : TickResult.Failure;

    private static TickResult HighRunningInWindowElseSuccess(in FakeClock c) =>
        c.NowMs is >= 100 and < 200 ? TickResult.Running : TickResult.Success;

    [Test]
    public void PrioritySelector_ResetsRunningLowerPrioritySubtree_SoItsWaitRestarts()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        Wait<FakeClock> wait = n.Wait("w", TimeSpan.FromMilliseconds(1000));
        PrioritySelector<FakeClock> root = n.PrioritySelector(
            "root",
            n.Do("high", HighRunningInWindowElseFailure),
            n.Sequence("low", wait, n.Do("d", static (in FakeClock _) => TickResult.Success))
        );
        FakeClock clock = new FakeClock();
        Harness h = new Harness(n, root, clock);

        // t=0: high fails, low branch starts its wait.
        h.Tick().ShouldBe(TickResult.Running);
        h.StatusOf(wait).ShouldBe(NodeStatus.Running);

        // t=100: high goes Running (non-terminal for the PrioritySelector) -> low sibling is
        // reset mid-wait. The composite returns Running, so this is purely the j>i reset.
        clock.Advance(100);
        h.Tick().ShouldBe(TickResult.Running);
        h.StatusOf(wait).ShouldBe(NodeStatus.Fresh);

        // t=200: high fails again; the wait RESTARTS from t=200 (not resumed from t=0).
        clock.Advance(100);
        h.Tick().ShouldBe(TickResult.Running);
        h.StampOf(wait).ShouldBe(1200L);

        // t=1100: only 900ms since the restart (< 1000) => still Running. Had the sibling not
        // been reset at t=100, the wait would have elapsed (1100 >= 1000) and succeeded.
        clock.Advance(900);
        h.Tick().ShouldBe(TickResult.Running);
    }

    [Test]
    public void PrioritySequence_ResetsRunningLowerPrioritySubtree_SoItsWaitRestarts()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        Wait<FakeClock> wait = n.Wait("w", TimeSpan.FromMilliseconds(1000));
        PrioritySequence<FakeClock> root = n.PrioritySequence(
            "root",
            n.Do("high", HighRunningInWindowElseSuccess),
            n.Sequence("low", wait, n.Do("d", static (in FakeClock _) => TickResult.Success))
        );
        FakeClock clock = new FakeClock();
        Harness h = new Harness(n, root, clock);

        // t=0: high succeeds, sequence proceeds to low branch which starts its wait.
        h.Tick().ShouldBe(TickResult.Running);
        h.StatusOf(wait).ShouldBe(NodeStatus.Running);

        // t=100: high goes Running (non-Success) -> low sibling reset mid-wait, returns Running.
        clock.Advance(100);
        h.Tick().ShouldBe(TickResult.Running);
        h.StatusOf(wait).ShouldBe(NodeStatus.Fresh);

        // t=200: high succeeds; wait restarts from t=200.
        clock.Advance(100);
        h.Tick().ShouldBe(TickResult.Running);
        h.StampOf(wait).ShouldBe(1200L);

        // t=1100: 900ms since restart (< 1000) => still Running.
        clock.Advance(900);
        h.Tick().ShouldBe(TickResult.Running);
    }
}
