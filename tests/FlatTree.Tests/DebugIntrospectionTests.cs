using FlatTree.Debugging;

namespace FlatTree.Tests;

/// <summary>The opt-in <c>FlatTree.Debugging</c> helpers: indented state dump and DOT export.</summary>
public sealed class DebugIntrospectionTests
{
    [Test]
    public void ToDebugString_ShowsNamesTypesAndLiveStatus()
    {
        var n = Bt.For<FakeClock>();
        var wait = n.Wait("w", TimeSpan.FromMilliseconds(1000));
        var done = n.Do("done", static _ => TickResult.Success);
        var root = n.Selector("root", wait, done);
        var tree = n.Build(root);

        var state = tree.NewState();
        tree.Tick(state, new FakeClock()); // root parks on the wait => Running

        var dump = tree.ToDebugString(state);

        dump.ShouldContain("root");
        dump.ShouldContain("Selector");
        dump.ShouldContain("w");
        dump.ShouldContain("Running");
    }

    [Test]
    public void ToDebugString_StructureOnly_OmitsStatus()
    {
        var n = Bt.For<FakeClock>();
        var tree = n.Build(n.Do("only", static _ => TickResult.Success));

        var dump = tree.ToDebugString();

        dump.ShouldContain("only");
        dump.ShouldNotContain(" = ");
    }

    [Test]
    public void ToDot_ContainsDigraphAndEdges()
    {
        var n = Bt.For<FakeClock>();
        var root = n.Sequence(
            "seq",
            n.Do("a", static _ => TickResult.Success),
            n.Do("b", static _ => TickResult.Success)
        );
        var tree = n.Build(root);

        var dot = tree.ToDot();

        dot.ShouldContain("digraph BehaviourTree");
        dot.ShouldContain("seq");
        dot.ShouldContain("->");
    }

    [Test]
    public void ToDot_WithState_ColorsNodes()
    {
        var n = Bt.For<FakeClock>();
        var tree = n.Build(n.Do("d", static _ => TickResult.Success));

        var state = tree.NewState();
        tree.Tick(state, new FakeClock());

        var dot = tree.ToDot(state);

        dot.ShouldContain("fillcolor=green");
    }
}
