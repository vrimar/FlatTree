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
}
