namespace FlatTree.Tests;

public sealed class NodeTagTests
{
    private const int Checkpoint = 1;
    private const int Cleanup = 2;

    [Test]
    public void NodesWithReturnsEveryNodeCarryingTheTagInIdOrder()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        BehaviourTree<FakeClock> tree = n.Build(
            n.Sequence(
                "seq",
                n.Tagged(n.Do("a", Ok), Checkpoint),
                n.Do("b", Ok),
                n.Tagged(n.Do("c", Ok), Cleanup),
                n.Tagged(n.Do("d", Ok), Checkpoint)
            )
        );

        tree.NodesWith(Checkpoint).ToArray().Select(node => node.Name).ShouldBe(["a", "d"]);
        tree.NodesWith(Cleanup).ToArray().Select(node => node.Name).ShouldBe(["c"]);
    }

    [Test]
    public void AnUnusedTagIsEmpty()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        BehaviourTree<FakeClock> tree = n.Build(n.Tagged(n.Do("a", Ok), Checkpoint));

        tree.NodesWith(Cleanup).IsEmpty.ShouldBeTrue();
        tree.NodesWith(0).IsEmpty.ShouldBeTrue();
    }

    [Test]
    public void ATreeWithNoTagsAnswersEmpty()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        BehaviourTree<FakeClock> tree = n.Build(n.Do("a", Ok));

        tree.NodesWith(Checkpoint).IsEmpty.ShouldBeTrue();
    }

    [Test]
    public void TaggingAfterBuildIsRejected()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        Do<FakeClock> node = n.Do("a", Ok);
        n.Build(node);

        Should.Throw<InvalidOperationException>(() => n.Tagged(node, Checkpoint));
    }

    [Test]
    public void TagZeroIsRejected()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();

        Should.Throw<ArgumentOutOfRangeException>(() => n.Tagged(n.Do("a", Ok), 0));
    }

    private static TickResult Ok(in FakeClock ctx) => TickResult.Success;
}
