namespace FlatTree.Bench;

/// <summary>
/// Ticks one SHARED tree across N agents, each with its own <c>NodeState[]</c>. Confirms the
/// allocation profile (per-agent = one <c>NodeState[]</c>, tree shared) and measures per-agent
/// tick cost as agent count scales.
/// </summary>
[MemoryDiagnoser]
public class TickThroughputBenchmark
{
    [Params(100, 1000, 5000)]
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
