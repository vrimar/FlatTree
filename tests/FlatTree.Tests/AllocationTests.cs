namespace FlatTree.Tests;

/// <summary>
/// Pins the library's headline guarantee: ticking allocates nothing. The benchmarks assert this too,
/// but CI does not run them, so without these a regression (a closure, a LINQ call, a boxed
/// enumerator on the hot path) would ship green.
/// </summary>
public sealed class AllocationTests
{
    private const int WarmupTicks = 32;
    private const int MeasuredTicks = 256;

    private static TickResult Succeed(in FakeClock c) => TickResult.Success;

    private static TickResult Run(in FakeClock c) => TickResult.Running;

    private static TickResult Step(in FakeClock c, ref int cursor, ref long stamp)
    {
        cursor++;
        stamp = c.NowMs;
        return cursor >= 3 ? TickResult.Success : TickResult.Running;
    }

    private static long MeasureTickAllocation(BehaviourTree<FakeClock> tree)
    {
        NodeState[] state = tree.NewState();
        FakeClock clock = new FakeClock();

        for (int i = 0; i < WarmupTicks; i++)
        {
            tree.Tick(state, clock);
            clock.Advance(10);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();

        for (int i = 0; i < MeasuredTicks; i++)
        {
            tree.Tick(state, clock);
            clock.Advance(10);
        }

        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    [Test]
    public void TickingATreeWithEveryNodeTypeAllocatesNothing()
    {
        BehaviourTree<FakeClock> tree = SampleTrees.EveryNodeType(out _);

        MeasureTickAllocation(tree).ShouldBe(0);
    }

    [Test]
    public void TickingAReactiveTreeWithResetCascadesAllocatesNothing()
    {
        var n = Bt.For<FakeClock>();
        var tree = n.Build(
            n.PrioritySelector(
                "root",
                n.Sequence(
                    "guarded",
                    n.Condition("gate", static (in FakeClock c) => (c.NowMs / 100) % 3 == 0),
                    n.Do("act", Run)
                ),
                n.Sequence("fallback", n.Wait("dwell", TimeSpan.FromMilliseconds(50)), n.Do("go", Succeed))
            )
        );

        MeasureTickAllocation(tree).ShouldBe(0);
    }

    [Test]
    public void TickingScratchLeavesAndShuffledCompositesAllocatesNothing()
    {
        var n = Bt.For<FakeClock>(new SeededRandomProvider(12345));
        var tree = n.Build(
            n.Sequence(
                "root",
                n.RandomSelector("pick", n.Do("a", Succeed), n.Do("b", Run), n.Do("c", Succeed)),
                n.RandomSequence("all", n.Do("d", Succeed), n.Do("e", Succeed)),
                n.Do("multi-tick", Step)
            )
        );

        MeasureTickAllocation(tree).ShouldBe(0);
    }

    [Test]
    public void TickingTheStatefulLeavesWaitsAndLoopsThroughTickOrRecoverAllocatesNothing()
    {
        var n = Bt.For<FakeClock>();
        var tree = n.Build(
            n.Forever(
                n.Sequence(
                    "lap",
                    n.Condition(0L, static (in FakeClock c, in long at) => c.NowMs >= at),
                    n.Do(TickResult.Success, static (in FakeClock _, in TickResult r) => r),
                    n.Act(static (in FakeClock _) => { }),
                    n.OnComplete(n.Do(Succeed), static (in FakeClock _, TickResult r) => r),
                    n.WaitUntil(Periodic, TimeSpan.FromMilliseconds(50), Recover),
                    n.WaitUntil(1, PeriodicSite, TimeSpan.FromMilliseconds(50), RecoverSite),
                    n.Repeat(3, n.Do(Succeed), Periodic),
                    n.AlwaysSucceed(n.While(Periodic, n.Do(Succeed), 3))
                )
            )
        );
        NodeState[] state = tree.NewState();
        FakeClock clock = new FakeClock();

        for (int i = 0; i < WarmupTicks; i++)
        {
            tree.TickOrRecover(state, clock);
            clock.Advance(10);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();

        for (int i = 0; i < MeasuredTicks; i++)
        {
            tree.TickOrRecover(state, clock);
            clock.Advance(10);
        }

        (GC.GetAllocatedBytesForCurrentThread() - before).ShouldBe(0);
    }

    private static bool Periodic(in FakeClock c) => (c.NowMs / 100) % 2 == 0;

    private static TickResult Recover(in FakeClock c) => TickResult.Success;

    private static bool PeriodicSite(in FakeClock c, in int site) => Periodic(in c);

    private static TickResult RecoverSite(in FakeClock c, in int site) => TickResult.Success;

    [Test]
    public void TickingThroughThePoolAllocatesNothing()
    {
        BehaviourTree<FakeClock> tree = SampleTrees.EveryNodeType(out _);
        var pool = new BehaviourTreePool<FakeClock>(tree, 4);
        FakeClock clock = new FakeClock();
        int slot = pool.Rent();

        for (int i = 0; i < WarmupTicks; i++)
        {
            pool.Tick(slot, clock);
            clock.Advance(10);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();

        for (int i = 0; i < MeasuredTicks; i++)
        {
            pool.Tick(slot, clock);
            clock.Advance(10);
        }

        (GC.GetAllocatedBytesForCurrentThread() - before).ShouldBe(0);
    }
}
