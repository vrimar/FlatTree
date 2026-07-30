namespace FlatTree.Tests;

public sealed class RandomSequenceTests
{
    private static MockNode[] MakeChildren(int count, TickResult status) =>
        Enumerable.Range(0, count).Select(_ => new MockNode { ReturnStatus = status }).ToArray();

    [Test]
    public void WhenAllChildrenSucceed_ReturnSuccessAndVisitEachExactlyOnce()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode[] children = MakeChildren(6, TickResult.Success);
        Harness h = new Harness(n, n.RandomSequence("RandomSequence", children));

        h.Tick().ShouldBe(TickResult.Success);

        children.ShouldAllBe(c => c.UpdateCallCount == 1);
    }

    [Test]
    public void WhenAChildFails_ReturnFailureAndStopVisiting()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode[] children = MakeChildren(6, TickResult.Failure);
        Harness h = new Harness(n, n.RandomSequence("RandomSequence", children));

        h.Tick().ShouldBe(TickResult.Failure);

        // First visited child fails, so the sequence stops: exactly one child is ticked.
        children.Count(c => c.UpdateCallCount == 1).ShouldBe(1);
        children.Count(c => c.UpdateCallCount == 0).ShouldBe(5);
    }

    [Test]
    public void FirstTick_DrawsNonZeroSeed()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();

        // Running children keep the activation open, so the drawn seed is observable; a completing
        // tick clears it again for the next activation.
        MockNode[] children = MakeChildren(6, TickResult.Running);
        RandomSequence<FakeClock> sut = n.RandomSequence("RandomSequence", children);
        Harness h = new Harness(n, sut);

        h.StampOf(sut).ShouldBe(0L);
        h.Tick().ShouldBe(TickResult.Running);
        h.StampOf(sut).ShouldNotBe(0L);
    }

    [Test]
    public void CompletingClearsTheSeedSoTheNextActivationReshuffles()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode[] children = MakeChildren(6, TickResult.Success);
        RandomSequence<FakeClock> sut = n.RandomSequence("RandomSequence", children);
        Harness h = new Harness(n, sut);

        h.Tick().ShouldBe(TickResult.Success);

        h.StampOf(sut).ShouldBe(0L);
        h.CursorOf(sut).ShouldBe(0);
    }
}
