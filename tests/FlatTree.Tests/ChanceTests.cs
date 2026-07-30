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

    [Test]
    public void ARunningChildLatchesTheGate_SoTheRollIsNotRepeated()
    {
        CountingRandomProvider rng = new CountingRandomProvider();
        BtFactory<FakeClock> n = Bt.For<FakeClock>(rng);
        MockNode child = new MockNode { ReturnStatus = TickResult.Running };
        Harness h = new Harness(n, n.Chance("Chance", 0.6, child));

        h.Tick().ShouldBe(TickResult.Running);
        h.Tick().ShouldBe(TickResult.Running);
        h.Tick().ShouldBe(TickResult.Running);

        child.UpdateCallCount.ShouldBe(3);
        rng.NextDoubleCallCount.ShouldBe(1);
    }

    [Test]
    public void ALatchedChildIsNeverAbandonedByALosingRoll()
    {
        ScriptedRandomProvider rng = new ScriptedRandomProvider();
        rng.SetNextDouble(0.0);
        BtFactory<FakeClock> n = Bt.For<FakeClock>(rng);
        MockNode child = new MockNode { ReturnStatus = TickResult.Running };
        Harness h = new Harness(n, n.Chance("Chance", 0.5, child));

        h.Tick().ShouldBe(TickResult.Running);

        // A roll that would now lose must not close the gate on work already in flight.
        rng.SetNextDouble(0.99);

        h.Tick().ShouldBe(TickResult.Running);
        child.UpdateCallCount.ShouldBe(2);

        child.ReturnStatus = TickResult.Success;
        h.Tick().ShouldBe(TickResult.Success);
    }

    [Test]
    public void CompletingReleasesTheLatch_SoTheNextActivationRollsAgain()
    {
        ScriptedRandomProvider rng = new ScriptedRandomProvider();
        rng.SetNextDouble(0.0);
        BtFactory<FakeClock> n = Bt.For<FakeClock>(rng);
        MockNode child = new MockNode { ReturnStatus = TickResult.Running };
        Chance<FakeClock> sut = n.Chance("Chance", 0.5, child);
        Harness h = new Harness(n, sut);

        h.Tick().ShouldBe(TickResult.Running);
        child.ReturnStatus = TickResult.Success;
        h.Tick().ShouldBe(TickResult.Success);

        h.CursorOf(sut).ShouldBe(0);

        rng.SetNextDouble(0.99);
        h.Tick().ShouldBe(TickResult.Failure);
    }

    [Test]
    public void AbortedWhileLatched_ReleasesTheChild()
    {
        ScriptedRandomProvider rng = new ScriptedRandomProvider();
        rng.SetNextDouble(0.0);
        var n = Bt.For<RecordingClock>(rng);
        var clock = new RecordingClock();
        var tree = n.Build(
            n.PrioritySelector(
                "root",
                n.Condition("guard", static (in RecordingClock c) => c.NowMs >= 1000),
                n.Chance("Chance", 0.5, new CleanupLeaf("gated-work"))
            )
        );
        var state = tree.NewState();

        tree.Tick(state, clock).ShouldBe(TickResult.Running);
        clock.Released.ShouldBeEmpty();

        clock.Advance(1000);
        tree.Tick(state, clock).ShouldBe(TickResult.Success);

        clock.Released.ShouldBe(new[] { "gated-work:reset" });
    }
}
