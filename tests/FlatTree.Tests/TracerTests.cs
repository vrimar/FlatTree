using FlatTree.Debugging;

namespace FlatTree.Tests;

public sealed class TracerTests
{
    private sealed class Recorder : IBtTracer<FakeClock>
    {
        public List<(string Node, NodeStatus Previous, TickResult Result)> Ticks { get; } = [];

        public List<(string Node, NodeStatus Previous)> Resets { get; } = [];

        public void Ticked(
            BtNode<FakeClock> node,
            NodeStatus previous,
            TickResult result,
            in FakeClock ctx
        ) => Ticks.Add((node.Name, previous, result));

        public void Reset(BtNode<FakeClock> node, NodeStatus previous, in FakeClock ctx) =>
            Resets.Add((node.Name, previous));
    }

    [Test]
    public void EveryNodeTickReachesTheTracerWithItsResult()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        Harness h = new(
            n,
            n.Sequence(
                "root",
                n.Do("first", static (in FakeClock _) => TickResult.Success),
                n.Wait("wait", TimeSpan.FromSeconds(1))
            )
        );
        Recorder tracer = new();
        h.Tree.Tracer = tracer;

        h.Tick();
        h.Tick();

        tracer.Ticks.ShouldBe([
            ("first", NodeStatus.Fresh, TickResult.Success),
            ("wait", NodeStatus.Fresh, TickResult.Running),
            ("root", NodeStatus.Fresh, TickResult.Running),
            ("wait", NodeStatus.Running, TickResult.Running),
            ("root", NodeStatus.Running, TickResult.Running),
        ]);
    }

    [Test]
    public void ANodeTornDownMidFlightReportsItWasRunning()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        Harness h = new(n, n.Wait("wait", TimeSpan.FromSeconds(1)));
        Recorder tracer = new();
        h.Tree.Tracer = tracer;

        h.Tick();
        h.ResetTree();

        tracer.Resets.ShouldBe([("wait", NodeStatus.Running)]);
    }

    private sealed class Described() : LeafNode<FakeClock>("described")
    {
        protected override TickResult Update(Span<NodeState> s, in FakeClock ctx) =>
            TickResult.Running;

        public override string Describe(in NodeState state) => "custom account";
    }

    [Test]
    public void TheDebugDumpUsesEachNodesOwnDescription()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        Harness h = new(n, new Described());

        h.Tick();

        h.Tree.ToDebugString(h.State).ShouldContain("custom account");
    }
}
