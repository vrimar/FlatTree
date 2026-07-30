namespace FlatTree.Bench;

/// <summary>
/// The allocation contract with consumers. Both trees allocate their agent state once in
/// <see cref="Setup"/>, then tick. The <c>[MemoryDiagnoser]</c> "Allocated" column must read
/// <b>0 B</b> for every case — the per-tick hot path is allocation-free, including the
/// <c>RandomSelector</c> <c>stackalloc</c> shuffle, the reset cascade, and abort-time teardown.
/// </summary>
[MemoryDiagnoser]
public class TickAllocationBenchmark
{
    private BehaviourTree<BenchContext> _everyNode = null!;
    private BehaviourTree<BenchContext> _deepReactive = null!;
    private NodeState[] _everyNodeState = null!;
    private NodeState[] _deepReactiveState = null!;
    private long _now;

    [GlobalSetup]
    public void Setup()
    {
        _everyNode = BenchTrees.EveryNodeType();
        _everyNodeState = _everyNode.NewState();
        _deepReactive = BenchTrees.DeepReactive();
        _deepReactiveState = _deepReactive.NewState();
        _now = 0;
    }

    [Benchmark]
    public TickResult Tick()
    {
        _now += 50;
        return _everyNode.Tick(_everyNodeState, new BenchContext(_now));
    }

    /// <summary>A reactive root over running subtrees — the consumer's tick shape.</summary>
    [Benchmark]
    public TickResult TickDeepReactive()
    {
        _now += 50;
        return _deepReactive.Tick(_deepReactiveState, new BenchContext(_now));
    }

    /// <summary>Tick then abort, so the reset cascade is on the measured path every iteration.</summary>
    [Benchmark]
    public TickResult TickDeepReactiveWithReset()
    {
        _now += 50;
        var ctx = new BenchContext(_now);
        var result = _deepReactive.Tick(_deepReactiveState, in ctx);
        _deepReactive.Reset(_deepReactiveState, in ctx);
        return result;
    }
}
