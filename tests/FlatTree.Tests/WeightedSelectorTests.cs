namespace FlatTree.Tests;

public sealed class WeightedSelectorTests
{
    private static MockNode[] MakeChildren(int count, TickResult status) =>
        Enumerable.Range(0, count).Select(_ => new MockNode { ReturnStatus = status }).ToArray();

    private static (int Weight, BtNode<FakeClock> Child)[] Weigh(
        int[] weights,
        BtNode<FakeClock>[] children
    ) => children.Select((child, i) => (weights[i], child)).ToArray();

    private sealed class OrderRecordingLeaf : LeafNode<FakeClock>
    {
        private readonly int _index;
        private readonly List<int> _order;

        public OrderRecordingLeaf(int index, List<int> order)
            : base($"leaf{index}")
        {
            _index = index;
            _order = order;
        }

        protected override TickResult Update(Span<NodeState> s, in FakeClock ctx)
        {
            _order.Add(_index);
            return TickResult.Failure;
        }
    }

    private static List<int> VisitOrder(IRandomProvider rng, params int[] weights)
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>(rng);
        var order = new List<int>();
        var recorders = Enumerable
            .Range(0, weights.Length)
            .Select(BtNode<FakeClock> (i) => new OrderRecordingLeaf(i, order))
            .ToArray();
        Harness h = new Harness(
            n,
            n.WeightedSelector("WeightedSelector", Weigh(weights, recorders))
        );

        h.Tick().ShouldBe(TickResult.Failure);

        return order;
    }

    [Test]
    public void WhenAllChildrenFail_ReturnFailureAndVisitEachExactlyOnce()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode[] children = MakeChildren(6, TickResult.Failure);
        Harness h = new Harness(
            n,
            n.WeightedSelector("WeightedSelector", Weigh([1, 2, 3, 4, 5, 6], children))
        );

        h.Tick().ShouldBe(TickResult.Failure);

        children.ShouldAllBe(c => c.UpdateCallCount == 1);
    }

    [Test]
    public void WhenTheDrawnChildFails_FallsThroughToTheNextDraw()
    {
        var fellThrough = 0;

        for (var seed = 1; seed <= 40; seed++)
        {
            BtFactory<FakeClock> n = Bt.For<FakeClock>(new SeededRandomProvider(seed));
            MockNode failing = new MockNode { ReturnStatus = TickResult.Failure };
            MockNode succeeding = new MockNode { ReturnStatus = TickResult.Success };
            Harness h = new Harness(n, n.WeightedSelector("w", (3, failing), (1, succeeding)));

            h.Tick().ShouldBe(TickResult.Success);

            succeeding.UpdateCallCount.ShouldBe(1);
            failing.UpdateCallCount.ShouldBeLessThanOrEqualTo(1);
            fellThrough += failing.UpdateCallCount;
        }

        fellThrough.ShouldBeGreaterThan(0);
    }

    [Test]
    public void WhileTheDrawnChildRuns_ItIsResumedEveryTickUntilItCompletes()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode[] children = MakeChildren(6, TickResult.Running);
        WeightedSelector<FakeClock> sut = n.WeightedSelector(
            "WeightedSelector",
            Weigh([1, 2, 3, 4, 5, 6], children)
        );
        Harness h = new Harness(n, sut);

        h.Tick().ShouldBe(TickResult.Running);
        h.Tick().ShouldBe(TickResult.Running);
        h.Tick().ShouldBe(TickResult.Running);

        MockNode running = children.Single(c => c.UpdateCallCount == 3);
        children.Count(c => c.UpdateCallCount == 0).ShouldBe(5);

        running.ReturnStatus = TickResult.Failure;

        h.Tick().ShouldBe(TickResult.Running);

        running.UpdateCallCount.ShouldBe(4);
        children.Count(c => c.UpdateCallCount == 1).ShouldBe(1);
        h.CursorOf(sut).ShouldBe(1);
    }

    [Test]
    public void IsDeterministicForTheSameSeed()
    {
        List<int> first = VisitOrder(new SeededRandomProvider(777), 5, 3, 1, 1, 4, 2);
        List<int> second = VisitOrder(new SeededRandomProvider(777), 5, 3, 1, 1, 4, 2);

        second.ShouldBe(first);

        var distinctOrders = Enumerable
            .Range(1, 40)
            .Select(seed =>
                string.Join(',', VisitOrder(new SeededRandomProvider(seed), 5, 3, 1, 1, 4, 2))
            )
            .Distinct()
            .Count();
        distinctOrders.ShouldBeGreaterThan(1);
    }

    // Replays and server-side reproductions rely on a seed giving this exact order everywhere.
    [Test]
    public void TheOrderDrawnFromASeedIsPinned()
    {
        VisitOrder(new SeededRandomProvider(7), 5, 3, 1, 1).ShouldBe([0, 1, 3, 2]);
        VisitOrder(new SeededRandomProvider(4), 5, 3, 1, 1).ShouldBe([1, 0, 3, 2]);
    }

    [Test]
    public void AThreeToOneWeighting_IsTriedFirstAboutThreeQuartersOfTheTime()
    {
        const int Activations = 4000;
        BtFactory<FakeClock> n = Bt.For<FakeClock>(new SeededRandomProvider(2026));
        MockNode heavy = new MockNode { ReturnStatus = TickResult.Success };
        MockNode light = new MockNode { ReturnStatus = TickResult.Success };
        Harness h = new Harness(n, n.WeightedSelector("w", (3, heavy), (1, light)));

        for (var i = 0; i < Activations; i++)
        {
            h.Tick().ShouldBe(TickResult.Success);
        }

        (heavy.UpdateCallCount + light.UpdateCallCount).ShouldBe(Activations);
        ((double)heavy.UpdateCallCount / Activations).ShouldBe(0.75, 0.03);
    }

    [Test]
    public void FirstTick_DrawsNonZeroSeed()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode[] children = MakeChildren(3, TickResult.Running);
        WeightedSelector<FakeClock> sut = n.WeightedSelector(
            "WeightedSelector",
            Weigh([1, 2, 3], children)
        );
        Harness h = new Harness(n, sut);

        h.StampOf(sut).ShouldBe(0L);
        h.Tick().ShouldBe(TickResult.Running);
        h.StampOf(sut).ShouldNotBe(0L);
    }

    [Test]
    public void OnReset_ClearsTheSeedSoTheNextActivationDrawsANewOne()
    {
        ScriptedRandomProvider rng = new ScriptedRandomProvider();
        rng.EnqueueNext(1, 2, 3, 4);
        BtFactory<FakeClock> n = Bt.For<FakeClock>(rng);
        MockNode[] children = MakeChildren(3, TickResult.Running);
        WeightedSelector<FakeClock> sut = n.WeightedSelector(
            "WeightedSelector",
            Weigh([1, 2, 3], children)
        );
        Harness h = new Harness(n, sut);

        h.Tick();
        long firstSeed = h.StampOf(sut);
        firstSeed.ShouldNotBe(0L);

        foreach (MockNode child in children)
        {
            child.ReturnStatus = TickResult.Failure;
        }

        h.Tick().ShouldBe(TickResult.Failure);
        h.StampOf(sut).ShouldBe(0L);
        h.CursorOf(sut).ShouldBe(0);

        foreach (MockNode child in children)
        {
            child.ReturnStatus = TickResult.Running;
        }

        h.Tick();
        long secondSeed = h.StampOf(sut);

        secondSeed.ShouldNotBe(0L);
        secondSeed.ShouldNotBe(firstSeed);
    }

    [Test]
    public void APreemptingReset_ClearsTheSeedAndTheCursor()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode[] children = MakeChildren(3, TickResult.Running);
        WeightedSelector<FakeClock> sut = n.WeightedSelector(
            "WeightedSelector",
            Weigh([1, 2, 3], children)
        );
        Harness h = new Harness(n, sut);

        h.Tick().ShouldBe(TickResult.Running);
        h.StampOf(sut).ShouldNotBe(0L);

        h.ResetTree();

        h.StampOf(sut).ShouldBe(0L);
        h.CursorOf(sut).ShouldBe(0);
        children.ShouldAllBe(c => h.StatusOf(c) == NodeStatus.Fresh);
    }

    [Test]
    public void DoResetIsIdempotent_SoCompletingDrawsExactlyOneSeed()
    {
        CountingRandomProvider rng = new CountingRandomProvider();
        BtFactory<FakeClock> n = Bt.For<FakeClock>(rng);
        MockNode[] children = MakeChildren(3, TickResult.Failure);
        WeightedSelector<FakeClock> sut = n.WeightedSelector(
            "WeightedSelector",
            Weigh([1, 2, 3], children)
        );

        // Under a composite parent, so the parent's terminal reset cascade also reaches sut.
        Harness h = new Harness(n, n.Sequence("root", sut));

        h.Tick().ShouldBe(TickResult.Failure);

        rng.NextCallCount.ShouldBe(2);
    }

    [Test]
    public void Weights_AreReportedInDeclarationOrder()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode[] children = MakeChildren(3, TickResult.Success);

        WeightedSelector<FakeClock> sut = n.WeightedSelector(Weigh([3, 1, 7], children));

        sut.Weights.ToArray().ShouldBe([3, 1, 7]);
        sut.Children.ToArray().ShouldBe(children);
        sut.Name.ShouldBe("WeightedSelector");
    }

    [Test]
    public void ANonPositiveWeight_Throws()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();

        Should
            .Throw<ArgumentOutOfRangeException>(() =>
                n.WeightedSelector("w", (1, new MockNode()), (0, new MockNode()))
            )
            .ParamName.ShouldBe("children");
        Should
            .Throw<ArgumentOutOfRangeException>(() =>
                n.WeightedSelector("w", (-3, new MockNode()), (1, new MockNode()))
            )
            .ParamName.ShouldBe("children");
    }

    [Test]
    public void WeightsSummingPastIntMaxValue_Throw()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();

        Should
            .Throw<ArgumentException>(() =>
                n.WeightedSelector("w", (int.MaxValue, new MockNode()), (1, new MockNode()))
            )
            .ParamName.ShouldBe("children");
    }

    [Test]
    public void WeightsSummingToExactlyIntMaxValue_AreAccepted()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode[] children = MakeChildren(2, TickResult.Failure);

        Harness h = new Harness(n, n.WeightedSelector("w", Weigh([int.MaxValue - 1, 1], children)));

        h.Tick().ShouldBe(TickResult.Failure);
        children.ShouldAllBe(c => c.UpdateCallCount == 1);
    }

    [Test]
    public void ANullChildOrChildArray_Throws()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();

        Should.Throw<ArgumentNullException>(() => n.WeightedSelector("w", null!));
        Should.Throw<ArgumentException>(() =>
            n.WeightedSelector("w", (1, new MockNode()), (1, null!))
        );
    }
}
