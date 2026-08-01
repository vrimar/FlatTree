namespace FlatTree.Bench;

/// <summary>
/// Head-to-head per-tick cost of the two layouts over the same tree shape and agent count:
/// FlatTree's shared node graph + contiguous <c>NodeState[]</c> against a classic
/// object-per-node-per-agent graph (<see cref="NaiveTrees.Boss"/>). Both graphs are fully
/// constructed in <see cref="Setup"/>, so this measures traversal and memory locality only, not
/// spawn cost — see <see cref="AgentSpawnBenchmark"/> for that.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="AgentCount"/> is chosen to straddle the cache hierarchy on a typical desktop CPU.
/// A <c>Boss</c> agent is 10 <c>NodeState</c> slots = 160 B, so 1000 agents (~160 KB) still fits in
/// L2 while 50,000 (~8 MB) does not. The flattened layout's advantage is a locality argument, so it
/// only shows up once the working set stops fitting.
/// </para>
/// <para>
/// The naive side gets the benefit of the doubt: each agent's nodes are allocated consecutively in
/// <see cref="Setup"/> and never collected, so its object graph is about as contiguous as a heap
/// layout ever gets. Real workloads spawn and despawn agents over time and fragment; treat the
/// naive numbers as a lower bound on the penalty, not a typical one.
/// </para>
/// </remarks>
[MemoryDiagnoser]
public class LayoutComparisonBenchmark
{
    [Params(1000, 50_000)]
    public int AgentCount { get; set; }

    private BehaviourTree<BenchContext> _tree = null!;
    private NodeState[][] _flatAgents = null!;
    private NaiveNode[] _naiveAgents = null!;
    private long _now;

    [GlobalSetup]
    public void Setup()
    {
        _tree = BenchTrees.Boss();

        // The two populations MUST be allocated in separate loops. Interleaving them separates each
        // 160 B state array by ~496 B of naive node objects, which costs the flat layout its
        // sequential prefetch (~28% at 50k agents) while barely touching the pointer-chasing side.
        _flatAgents = new NodeState[AgentCount][];
        for (int i = 0; i < AgentCount; i++)
        {
            _flatAgents[i] = _tree.NewState();
        }

        _naiveAgents = new NaiveNode[AgentCount];
        for (int i = 0; i < AgentCount; i++)
        {
            _naiveAgents[i] = NaiveTrees.Boss();
        }

        _now = 0;
    }

    [Benchmark(Baseline = true)]
    public void Flat()
    {
        _now += 50;
        var ctx = new BenchContext(_now);
        var agents = _flatAgents;
        for (int i = 0; i < agents.Length; i++)
        {
            _tree.Tick(agents[i], in ctx);
        }
    }

    [Benchmark]
    public void ObjectPerNodePerAgent()
    {
        _now += 50;
        var ctx = new BenchContext(_now);
        var agents = _naiveAgents;
        for (int i = 0; i < agents.Length; i++)
        {
            agents[i].Tick(in ctx);
        }
    }
}
