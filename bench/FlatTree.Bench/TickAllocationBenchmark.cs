namespace FlatTree.Bench;

/// <summary>
/// Builds one tree containing every node type, allocates the agent state once in
/// <see cref="Setup"/>, then ticks it. The <c>[MemoryDiagnoser]</c> "Allocated" column must
/// read <b>0 B</b> — the per-tick hot path is allocation-free (including the
/// <c>RandomSelector</c> <c>stackalloc</c> shuffle).
/// </summary>
[MemoryDiagnoser]
public class TickAllocationBenchmark
{
    private BehaviourTree<BenchContext> _tree = null!;
    private NodeState[] _state = null!;
    private long _now;

    [GlobalSetup]
    public void Setup()
    {
        _tree = BenchTrees.EveryNodeType();
        _state = _tree.NewState();
        _now = 0;
    }

    [Benchmark]
    public TickResult Tick()
    {
        _now += 50;
        return _tree.Tick(_state, new BenchContext(_now));
    }
}
