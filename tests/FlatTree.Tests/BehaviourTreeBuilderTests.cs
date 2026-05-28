namespace FlatTree.Tests;

public sealed class BehaviourTreeBuilderTests
{
    [Test]
    public void Build_AssignsDfsPreOrderIdsAndNodeCount()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();

        Do<FakeClock> a0 = n.Do("a0", static _ => TickResult.Success);
        Do<FakeClock> a1 = n.Do("a1", static _ => TickResult.Success);
        Sequence<FakeClock> seq = n.Sequence("seq", a0, a1);
        Do<FakeClock> b0 = n.Do("b0", static _ => TickResult.Success);
        Selector<FakeClock> root = n.Selector("root", seq, b0);

        BehaviourTree<FakeClock> tree = n.Build(root);

        root.Id.ShouldBe(0);
        seq.Id.ShouldBe(1);
        a0.Id.ShouldBe(2);
        a1.Id.ShouldBe(3);
        b0.Id.ShouldBe(4);
        tree.NodeCount.ShouldBe(5);
        tree.NewState().Length.ShouldBe(5);
    }

    [Test]
    public void Build_ARealTreeWithEveryNodeType_TicksWithoutThrowing()
    {
        BehaviourTree<FakeClock> tree = SampleTrees.EveryNodeType(out int expectedNodeCount);
        tree.NodeCount.ShouldBe(expectedNodeCount);

        NodeState[] state = tree.NewState();
        FakeClock clock = new FakeClock();

        for (int i = 0; i < 50; i++)
        {
            var status = tree.Tick(state, clock);
            status.ShouldBeOneOf(TickResult.Running, TickResult.Success, TickResult.Failure);
            clock.Advance(100);
        }
    }

    [Test]
    public void Build_WhenANodeInstanceIsReused_Throws()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        Do<FakeClock> shared = n.Do("shared", static _ => TickResult.Success);
        Selector<FakeClock> root = n.Selector("root", shared, shared);

        Should.Throw<InvalidOperationException>(() => n.Build(root));
    }

    [Test]
    public void NewState_StartsAllSlotsFresh()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        BehaviourTree<FakeClock> tree = n.Build(n.Do("a", static _ => TickResult.Success));

        NodeState[] state = tree.NewState();

        state.ShouldAllBe(slot => slot.Status == NodeStatus.Fresh);
    }
}
