namespace FlatTree.Tests;

public sealed class ChanceTests
{
    [Test]
    [Arguments(0.0)]
    [Arguments(-10.0)]
    public void WhenProbabilityIsBelowOrEqualToZero_Throw(double probability)
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode child = new MockNode { ReturnStatus = TickResult.Running };

        Should.Throw<ArgumentException>(() => n.Chance("Chance", probability, child));
    }

    [Test]
    public void WhenProbabilityIsAboveOne_Throw()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode child = new MockNode { ReturnStatus = TickResult.Running };

        Should.Throw<ArgumentException>(() => n.Chance("Chance", 1.1, child));
    }

    [Test]
    [Arguments(TickResult.Success)]
    [Arguments(TickResult.Failure)]
    public void WhenRollIsBelowProbability_TickChildAndReturnItsStatus(TickResult childStatus)
    {
        ScriptedRandomProvider rng = new ScriptedRandomProvider();
        rng.SetNextDouble(0.0);
        BtFactory<FakeClock> n = Bt.For<FakeClock>(rng);
        MockNode child = new MockNode { ReturnStatus = childStatus };
        Harness h = new Harness(n, n.Chance("Chance", 0.5, child));

        h.Tick().ShouldBe(childStatus);
        child.TerminateCallCount.ShouldBe(1);
    }

    [Test]
    public void WhenRollIsAboveProbability_DoNotTickChildAndReturnFailure()
    {
        ScriptedRandomProvider rng = new ScriptedRandomProvider();
        rng.SetNextDouble(0.6);
        BtFactory<FakeClock> n = Bt.For<FakeClock>(rng);
        MockNode child = new MockNode { ReturnStatus = TickResult.Success };
        Harness h = new Harness(n, n.Chance("Chance", 0.5, child));

        h.Tick().ShouldBe(TickResult.Failure);
        child.TerminateCallCount.ShouldBe(0);
        child.UpdateCallCount.ShouldBe(0);
    }

    [Test]
    public void WhenRollEqualsProbability_GateIsClosedAndReturnsFailure()
    {
        ScriptedRandomProvider rng = new ScriptedRandomProvider();
        rng.SetNextDouble(0.4);
        BtFactory<FakeClock> n = Bt.For<FakeClock>(rng);
        MockNode child = new MockNode { ReturnStatus = TickResult.Success };
        Harness h = new Harness(n, n.Chance("Chance", 0.4, child));

        h.Tick().ShouldBe(TickResult.Failure);
        child.TerminateCallCount.ShouldBe(0);
        child.UpdateCallCount.ShouldBe(0);
    }
}
