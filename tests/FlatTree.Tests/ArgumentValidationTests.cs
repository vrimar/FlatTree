namespace FlatTree.Tests;

/// <summary>
/// The construction-time throw paths. Building a tree is a startup concern, so these guards are the
/// cheapest place to catch a malformed tree — but they were entirely untested.
/// </summary>
public sealed class ArgumentValidationTests
{
    private static TickResult Succeed(in FakeClock c) => TickResult.Success;

    private static bool True(in FakeClock c) => true;

    [Test]
    public void Composite_WithNoChildren_Throws()
    {
        var n = Bt.For<FakeClock>();

        Should.Throw<ArgumentException>(() => n.Selector("s", Array.Empty<BtNode<FakeClock>>()));
        Should.Throw<ArgumentException>(() => n.Sequence("s", Array.Empty<BtNode<FakeClock>>()));
        Should.Throw<ArgumentException>(() =>
            n.PrioritySelector("s", Array.Empty<BtNode<FakeClock>>())
        );
        Should.Throw<ArgumentException>(() =>
            n.PrioritySequence("s", Array.Empty<BtNode<FakeClock>>())
        );
        Should.Throw<ArgumentException>(() =>
            n.RandomSelector("s", Array.Empty<BtNode<FakeClock>>())
        );
        Should.Throw<ArgumentException>(() =>
            n.RandomSequence("s", Array.Empty<BtNode<FakeClock>>())
        );
    }

    [Test]
    public void Composite_WithANullChild_Throws()
    {
        var n = Bt.For<FakeClock>();
        BtNode<FakeClock>[] children = [n.Do("d", Succeed), null!];

        Should.Throw<ArgumentException>(() => n.Selector("s", children));
    }

    [Test]
    public void Decorator_WithANullChild_Throws()
    {
        var n = Bt.For<FakeClock>();

        Should.Throw<ArgumentNullException>(() => n.Inverter("i", null!));
        Should.Throw<ArgumentNullException>(() => n.AutoReset("a", null!));
        Should.Throw<ArgumentNullException>(() => n.Forever("f", null!));
        Should.Throw<ArgumentNullException>(() =>
            n.Cooldown("c", TimeSpan.FromMilliseconds(10), null!)
        );
    }

    [Test]
    public void Leaf_WithANullDelegate_Throws()
    {
        var n = Bt.For<FakeClock>();

        Should.Throw<ArgumentNullException>(() => n.Do("d", (LeafAction<FakeClock>)null!));
        Should.Throw<ArgumentNullException>(() => n.Do("d", (StatefulAction<FakeClock>)null!));
        Should.Throw<ArgumentNullException>(() => n.Condition("c", null!));
    }

    [Test]
    public void Repeat_WithANonPositiveCount_Throws()
    {
        var n = Bt.For<FakeClock>();

        Should.Throw<ArgumentOutOfRangeException>(() => n.Repeat("r", 0, n.Do("d", Succeed)));
        Should.Throw<ArgumentOutOfRangeException>(() => n.Repeat("r", -1, n.Do("d", Succeed)));
    }

    [Test]
    public void Build_WithANullRoot_Throws()
    {
        var n = Bt.For<FakeClock>();

        Should.Throw<ArgumentNullException>(() => n.Build(null!));
    }

    [Test]
    public void Uninterruptible_WithANullNode_Throws()
    {
        var n = Bt.For<FakeClock>();

        Should.Throw<ArgumentNullException>(() => n.Uninterruptible<BtNode<FakeClock>>(null!));
    }

    [Test]
    public void Pool_WithANullTree_Throws()
    {
        Should.Throw<ArgumentNullException>(() => new BehaviourTreePool<FakeClock>(null!, 4));
    }

    [Test]
    public void SimpleParallel_BeyondMaxChildren_Throws()
    {
        var n = Bt.For<FakeClock>();
        BtNode<FakeClock>[] children = Enumerable
            .Range(0, SimpleParallel<FakeClock>.MaxChildren + 1)
            .Select(BtNode<FakeClock> (_) => n.Do("d", Succeed))
            .ToArray();

        Should.Throw<ArgumentException>(() =>
            n.SimpleParallel("p", SimpleParallelPolicy.BothMustSucceed, children)
        );
    }

    // TickShuffled holds its stackalloc permutation live across the recursive child ticks, so an
    // unbounded child count is a latent stack overflow rather than a manageable exception.
    [Test]
    public void ShuffledComposite_BeyondMaxChildren_Throws()
    {
        var n = Bt.For<FakeClock>();
        BtNode<FakeClock>[] tooMany = Enumerable
            .Range(0, CompositeNode<FakeClock>.MaxShuffledChildren + 1)
            .Select(BtNode<FakeClock> (_) => n.Condition("c", True))
            .ToArray();

        Should.Throw<ArgumentException>(() => n.RandomSelector("rs", tooMany));

        var m = Bt.For<FakeClock>();
        BtNode<FakeClock>[] tooManyAgain = Enumerable
            .Range(0, CompositeNode<FakeClock>.MaxShuffledChildren + 1)
            .Select(BtNode<FakeClock> (_) => m.Condition("c", True))
            .ToArray();

        Should.Throw<ArgumentException>(() => m.RandomSequence("rq", tooManyAgain));
    }

    [Test]
    public void ShuffledComposite_AtMaxChildren_IsAccepted()
    {
        var n = Bt.For<FakeClock>();
        BtNode<FakeClock>[] children = Enumerable
            .Range(0, CompositeNode<FakeClock>.MaxShuffledChildren)
            .Select(BtNode<FakeClock> (_) => n.Condition("c", True))
            .ToArray();

        var h = new Harness(n, n.RandomSelector("rs", children));

        h.Tick().ShouldBe(TickResult.Success);
    }

    // IRandomProvider documents the result as being in [0, maxExclusive), which is empty for 0, so
    // both shipped providers must reject it rather than one returning 0.
    [Test]
    public void BothRandomProviders_RejectANonPositiveBound()
    {
        var seeded = new SeededRandomProvider(1);

        Should.Throw<ArgumentOutOfRangeException>(() => seeded.Next(0));
        Should.Throw<ArgumentOutOfRangeException>(() => seeded.Next(-1));

        Should.Throw<ArgumentOutOfRangeException>(() => DefaultRandomProvider.Instance.Next(0));
        Should.Throw<ArgumentOutOfRangeException>(() => DefaultRandomProvider.Instance.Next(-1));
    }
}
