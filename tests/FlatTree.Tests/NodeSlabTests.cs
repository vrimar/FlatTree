namespace FlatTree.Tests;

public sealed class NodeSlabTests
{
    [Test]
    public void EachSlotGetsItsOwnSliceOfNodeCountEntries()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        BehaviourTree<FakeClock> tree = n.Build(SampleTree(n));
        NodeSlab<int> slab = tree.NewSlab<int>(4);

        slab.Slice(0).Length.ShouldBe(tree.NodeCount);
        slab.Capacity.ShouldBe(4);

        slab.Slice(1)[0] = 7;

        slab.Slice(0)[0].ShouldBe(0);
        slab.Slice(1)[0].ShouldBe(7);
    }

    [Test]
    public void ASlabSlotLinesUpWithThePoolSlotThatIndexesIt()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        BehaviourTree<FakeClock> tree = n.Build(SampleTree(n));
        BehaviourTreePool<FakeClock> pool = new BehaviourTreePool<FakeClock>(tree, 2);
        NodeSlab<string> slab = tree.NewSlab<string>(2);
        FakeClock clock = new FakeClock();

        int first = pool.Rent();
        int second = pool.Rent();

        slab.Slice(first)[0] = "a";
        slab.Slice(second)[0] = "b";

        pool.Tick(first, clock);
        slab.Slice(first)[0].ShouldBe("a");
        slab.Slice(second)[0].ShouldBe("b");

        pool.Return(first);
        slab.Clear(first);
        slab.Slice(first)[0].ShouldBeNull();
    }

    [Test]
    public void ASlotOutsideCapacityIsRejected()
    {
        NodeSlab<int> slab = new NodeSlab<int>(3, 2);

        Should.Throw<ArgumentOutOfRangeException>(() => slab.Slice(2));
        Should.Throw<ArgumentOutOfRangeException>(() => slab.Slice(-1));
    }

    [Test]
    public void ASidecarIsOneEntryPerNode()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        BehaviourTree<FakeClock> tree = n.Build(SampleTree(n));

        tree.NewSidecar<int>().Length.ShouldBe(tree.NodeCount);
    }

    private static BtNode<FakeClock> SampleTree(BtFactory<FakeClock> n) =>
        n.Sequence(
            "seq",
            n.Do("a", static (in FakeClock _) => TickResult.Success),
            n.Do("b", static (in FakeClock _) => TickResult.Success)
        );
}
