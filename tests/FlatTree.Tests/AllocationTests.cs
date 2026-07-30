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
