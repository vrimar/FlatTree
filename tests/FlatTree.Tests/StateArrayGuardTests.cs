namespace FlatTree.Tests;

/// <summary>
/// A wrong-sized state array is a caller error that used to surface as an
/// <see cref="IndexOutOfRangeException"/> from deep inside a span indexer (or, in Release, as
/// silent misbehaviour). It now throws a clear <see cref="ArgumentException"/> up front.
/// </summary>
public sealed class StateArrayGuardTests
{
    private static TickResult Succeed(in FakeClock c) => TickResult.Success;

    private static BehaviourTree<FakeClock> ThreeNodeTree()
    {
        var n = Bt.For<FakeClock>();
        return n.Build(n.Sequence("root", n.Do("a", Succeed), n.Do("b", Succeed)));
    }

    [Test]
    [Arguments(0)]
    [Arguments(2)]
    [Arguments(4)]
    public void Tick_WithAWrongLengthStateArray_Throws(int length)
    {
        var tree = ThreeNodeTree();
        var state = new NodeState[length];

        var ex = Should.Throw<ArgumentException>(() => tree.Tick(state, new FakeClock()));

        ex.Message.ShouldContain("NodeCount");
    }

    [Test]
    public void Reset_WithAWrongLengthStateArray_Throws()
    {
        var tree = ThreeNodeTree();

        Should.Throw<ArgumentException>(() => tree.Reset(new NodeState[1], new FakeClock()));
    }

    [Test]
    public void StatusOf_WithAWrongLengthStateArray_Throws()
    {
        var tree = ThreeNodeTree();

        Should.Throw<ArgumentException>(() => tree.StatusOf(new NodeState[1], tree.Root));
    }

    [Test]
    public void TheErrorNamesBothLengths()
    {
        var tree = ThreeNodeTree();

        var ex = Should.Throw<ArgumentException>(() => tree.Tick(new NodeState[7], new FakeClock()));

        ex.Message.ShouldContain("7");
        ex.Message.ShouldContain(tree.NodeCount.ToString());
    }

    [Test]
    public void ACorrectlySizedArrayIsAccepted()
    {
        var tree = ThreeNodeTree();

        Should.NotThrow(() => tree.Tick(tree.NewState(), new FakeClock()));
    }
}
