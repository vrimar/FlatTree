namespace FlatTree.Tests;

public sealed class RandomSelectorTests
{
    private static MockNode[] MakeChildren(int count, TickResult status) =>
        Enumerable.Range(0, count).Select(_ => new MockNode { ReturnStatus = status }).ToArray();

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

    [Test]
    public void WhenAllChildrenFail_ReturnFailureAndVisitEachExactlyOnce()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode[] children = MakeChildren(6, TickResult.Failure);
        Harness h = new Harness(n, n.RandomSelector("RandomSelector", children));

        h.Tick().ShouldBe(TickResult.Failure);

        children.ShouldAllBe(c => c.UpdateCallCount == 1);
    }

    // Every other test here passes under an identity permutation, so nothing else would notice if
    // the shuffle stopped shuffling and every agent picked the same branch first forever.
    [Test]
    public void AcrossSeeds_TheVisitOrderIsActuallyPermuted()
    {
        var firstVisited = new HashSet<int>();
        var sawNonIdentityOrder = false;

        for (var seed = 1; seed <= 40; seed++)
        {
            BtFactory<FakeClock> n = Bt.For<FakeClock>(new SeededRandomProvider(seed));
            MockNode[] children = MakeChildren(6, TickResult.Failure);
            var order = new List<int>();
            var recorders = Enumerable
                .Range(0, 6)
                .Select(BtNode<FakeClock> (i) => new OrderRecordingLeaf(i, order))
                .ToArray();
            Harness h = new Harness(n, n.RandomSelector("RandomSelector", recorders));

            h.Tick().ShouldBe(TickResult.Failure);

            order.Count.ShouldBe(6);
            firstVisited.Add(order[0]);
            sawNonIdentityOrder |= !order.SequenceEqual(Enumerable.Range(0, 6));
        }

        sawNonIdentityOrder.ShouldBeTrue();
        firstVisited.Count.ShouldBeGreaterThan(1);
    }

    [Test]
    public void FirstTick_DrawsNonZeroSeed()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();

        // Running children keep the activation open, so the drawn seed is observable; a completing
        // tick clears it again for the next activation.
        MockNode[] children = MakeChildren(6, TickResult.Running);
        RandomSelector<FakeClock> sut = n.RandomSelector("RandomSelector", children);
        Harness h = new Harness(n, sut);

        h.StampOf(sut).ShouldBe(0L);
        h.Tick().ShouldBe(TickResult.Running);
        h.StampOf(sut).ShouldNotBe(0L);
    }

    [Test]
    public void WithinASession_ReVisitsTheSameChildAtTheSameCursor()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode[] children = MakeChildren(6, TickResult.Running);
        Harness h = new Harness(n, n.RandomSelector("RandomSelector", children));

        h.Tick().ShouldBe(TickResult.Running);
        h.Tick().ShouldBe(TickResult.Running);
        h.Tick().ShouldBe(TickResult.Running);

        // The first visited child returns Running, so only it is ticked — and the same one
        // every tick because the seed (and therefore the permutation) is stable.
        children.Count(c => c.UpdateCallCount == 3).ShouldBe(1);
        children.Count(c => c.UpdateCallCount == 0).ShouldBe(5);
    }

    [Test]
    public void OnReset_ClearsTheSeedSoTheNextActivationDrawsANewOne()
    {
        ScriptedRandomProvider rng = new ScriptedRandomProvider();
        rng.EnqueueNext(1, 2, 3, 4);
        BtFactory<FakeClock> n = Bt.For<FakeClock>(rng);
        MockNode[] children = MakeChildren(6, TickResult.Running);
        RandomSelector<FakeClock> sut = n.RandomSelector("RandomSelector", children);
        Harness h = new Harness(n, sut);

        h.Tick();
        long firstSeed = h.StampOf(sut);
        firstSeed.ShouldNotBe(0L);

        // Force a terminal result so the node resets.
        foreach (MockNode child in children)
        {
            child.ReturnStatus = TickResult.Failure;
        }

        h.Tick().ShouldBe(TickResult.Failure);
        h.StampOf(sut).ShouldBe(0L);

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
    public void DoResetIsIdempotent_SoCompletingDrawsExactlyOneSeed()
    {
        CountingRandomProvider rng = new CountingRandomProvider();
        BtFactory<FakeClock> n = Bt.For<FakeClock>(rng);
        MockNode[] children = MakeChildren(3, TickResult.Failure);
        RandomSelector<FakeClock> sut = n.RandomSelector("RandomSelector", children);

        // Under a composite parent, so the parent's terminal reset cascade also reaches sut.
        Harness h = new Harness(n, n.Sequence("root", sut));

        h.Tick().ShouldBe(TickResult.Failure);

        // One activation => one seed => two Next() calls, however many times DoReset runs.
        rng.NextCallCount.ShouldBe(2);
    }

    [Test]
    public void IsDeterministicForTheSameSeed()
    {
        ScriptedRandomProvider rng = new ScriptedRandomProvider();
        rng.SetDefaultNext(12345);
        BtFactory<FakeClock> n = Bt.For<FakeClock>(rng);

        MockNode[] childrenA = MakeChildren(6, TickResult.Running);
        MockNode[] childrenB = MakeChildren(6, TickResult.Running);
        Harness a = new Harness(n, n.RandomSelector("A", childrenA));
        Harness b = new Harness(n, n.RandomSelector("B", childrenB));

        a.Tick();
        b.Tick();

        // Same constant seed source => same permutation => same first child visited.
        int indexA = Array.FindIndex(childrenA, c => c.UpdateCallCount == 1);
        int indexB = Array.FindIndex(childrenB, c => c.UpdateCallCount == 1);
        indexA.ShouldBe(indexB);
    }
}
