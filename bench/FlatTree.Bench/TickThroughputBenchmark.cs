namespace FlatTree.Bench;

/// <summary>
/// Ticks one SHARED tree across N agents, each with its own <c>NodeState[]</c>. Measures how
/// per-agent tick cost scales with agent count, and confirms that ticking at scale stays
/// allocation-free.
/// </summary>
/// <remarks>
/// The per-agent spawn allocation is NOT measured here — every <c>NewState()</c> runs in
/// <c>[GlobalSetup]</c>, outside the measured region. See <see cref="AgentSpawnBenchmark"/>.
/// The top count is deliberately past the cache hierarchy: a <c>Boss</c> agent is 10
/// <c>NodeState</c> slots = 160 B, so 5000 agents (~800 KB) still fits in L2 and the curve stays
/// flat, while 50,000 (~8 MB) does not.
/// </remarks>
[MemoryDiagnoser]
public class TickThroughputBenchmark
{
    [Params(100, 1000, 5000, 50_000)]
    public int AgentCount { get; set; }

    private BehaviourTree<BenchContext> _tree = null!;
    private NodeState[][] _agents = null!;
    private long _now;

    [GlobalSetup]
    public void Setup()
    {
        _tree = BenchTrees.Boss();
        _agents = new NodeState[AgentCount][];
        for (int i = 0; i < AgentCount; i++)
        {
            _agents[i] = _tree.NewState();
        }

        _now = 0;
    }

    [Benchmark]
    public void TickAllAgents()
    {
        _now += 50;
        var ctx = new BenchContext(_now);
        var agents = _agents;
        for (int i = 0; i < agents.Length; i++)
        {
            _tree.Tick(agents[i], in ctx);
        }
    }
}
