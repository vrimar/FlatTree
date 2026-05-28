namespace FlatTree.Tests.Doubles;

/// <summary>
/// Test convenience: builds a single shared tree rooted at <paramref name="root"/>, allocates
/// one agent state array, and ticks against a <see cref="FakeClock"/>. Wraps the
/// construct-a-SUT-then-<c>Tick</c>/<c>Reset</c> pattern.
/// </summary>
public sealed class Harness
{
    public BehaviourTree<FakeClock> Tree { get; }

    public NodeState[] State { get; }

    public FakeClock Clock { get; }

    public Harness(BtFactory<FakeClock> factory, BtNode<FakeClock> root, FakeClock? clock = null)
    {
        Tree = factory.Build(root);
        State = Tree.NewState();
        Clock = clock ?? new FakeClock();
    }

    public TickResult Tick() => Tree.Tick(State, Clock);

    public void ResetTree() => Tree.Reset(State);

    public NodeStatus StatusOf(BtNode<FakeClock> node) => State[node.Id].Status;

    public int CursorOf(BtNode<FakeClock> node) => State[node.Id].Cursor;

    public long StampOf(BtNode<FakeClock> node) => State[node.Id].Stamp;
}
