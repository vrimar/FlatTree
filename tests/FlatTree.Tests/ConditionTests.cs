namespace FlatTree.Tests;

public sealed class ConditionTests
{
    [Test]
    public void WhenPredicateReturnsTrue_ReturnSuccess()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        Harness h = new Harness(n, n.Condition("Condition", static (in FakeClock _) => true));

        h.Tick().ShouldBe(TickResult.Success);
    }

    [Test]
    public void WhenPredicateReturnsFalse_ReturnFailure()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        Harness h = new Harness(n, n.Condition("Condition", static (in FakeClock _) => false));

        h.Tick().ShouldBe(TickResult.Failure);
    }

    [Test]
    public void ThePredicateReceivesTheAuthoredState()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        FakeClock clock = new FakeClock();
        Condition<FakeClock, long> sut = n.Condition("after", 500L, IsPast);
        Harness h = new Harness(n, sut, clock);

        h.Tick().ShouldBe(TickResult.Failure);
        clock.Advance(500);
        h.Tick().ShouldBe(TickResult.Success);
        sut.State.ShouldBe(500L);
    }

    [Test]
    public void OneMethodServesManySitesThroughTheirState()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        FakeClock clock = new FakeClock();
        Harness h = new Harness(
            n,
            n.Sequence(n.Condition(0L, IsPast), n.Condition(1L, IsPast)),
            clock
        );

        h.Tick().ShouldBe(TickResult.Failure);
        clock.Advance(1);
        h.Tick().ShouldBe(TickResult.Success);
    }

    private static bool IsPast(in FakeClock c, in long at) => c.NowMs >= at;
}
