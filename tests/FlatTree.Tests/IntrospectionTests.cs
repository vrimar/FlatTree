namespace FlatTree.Tests;

public sealed class IntrospectionTests
{
    [Test]
    public void Nodes_AreIndexedById_AndCountMatchesNodeCount()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        Do<FakeClock> a0 = n.Do("a0", static _ => TickResult.Success);
        Do<FakeClock> a1 = n.Do("a1", static _ => TickResult.Success);
        Sequence<FakeClock> seq = n.Sequence("seq", a0, a1);
        Do<FakeClock> b0 = n.Do("b0", static _ => TickResult.Success);
        Selector<FakeClock> root = n.Selector("root", seq, b0);

        BehaviourTree<FakeClock> tree = n.Build(root);

        tree.Nodes.Count.ShouldBe(tree.NodeCount);
        tree.Nodes[root.Id].ShouldBeSameAs(root);
        tree.Nodes[seq.Id].ShouldBeSameAs(seq);
        tree.Nodes[a0.Id].ShouldBeSameAs(a0);
        tree.Nodes[a1.Id].ShouldBeSameAs(a1);
        tree.Nodes[b0.Id].ShouldBeSameAs(b0);
    }

    [Test]
    public void StatusOf_ReturnsTheNodesStatusForTheAgent()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        Wait<FakeClock> wait = n.Wait("w", TimeSpan.FromMilliseconds(1000));
        Do<FakeClock> done = n.Do("d", static _ => TickResult.Success);
        Selector<FakeClock> root = n.Selector("root", wait, done);
        BehaviourTree<FakeClock> tree = n.Build(root);

        NodeState[] state = tree.NewState();
        FakeClock clock = new FakeClock();

        tree.StatusOf(state, root).ShouldBe(NodeStatus.Fresh);
        tree.StatusOf(state, wait).ShouldBe(NodeStatus.Fresh);

        // The Wait holds the Selector, so the whole branch is Running and `done` is untouched.
        tree.Tick(state, clock).ShouldBe(TickResult.Running);
        tree.StatusOf(state, root).ShouldBe(NodeStatus.Running);
        tree.StatusOf(state, wait).ShouldBe(NodeStatus.Running);
        tree.StatusOf(state, done).ShouldBe(NodeStatus.Fresh);

        // StatusOf agrees with the raw slot by definition.
        tree.StatusOf(state, wait).ShouldBe(state[wait.Id].Status);
    }
}
