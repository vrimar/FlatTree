namespace FlatTree.Tests;

public sealed class IfTests
{
    [Test]
    public void AFailingThenBranchDoesNotFallBackToElse()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode condition = new() { ReturnStatus = TickResult.Success };
        MockNode then = new() { ReturnStatus = TickResult.Failure };
        MockNode otherwise = new();
        Harness h = new(n, n.If("if", condition, then, otherwise));

        h.Tick().ShouldBe(TickResult.Failure);

        otherwise.UpdateCallCount.ShouldBe(0);
    }

    [Test]
    public void AFalseConditionWithoutElseSucceeds()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode then = new();
        Harness h = new(
            n,
            n.If("if", new MockNode { ReturnStatus = TickResult.Failure }, then)
        );

        h.Tick().ShouldBe(TickResult.Success);

        then.UpdateCallCount.ShouldBe(0);
    }

    [Test]
    public void TheConditionIsReadOnceWhileTheBranchRuns()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode condition = new() { ReturnStatus = TickResult.Success };
        MockNode then = new() { ReturnStatus = TickResult.Running };
        MockNode otherwise = new();
        Harness h = new(n, n.If("if", condition, then, otherwise));

        h.Tick();
        condition.ReturnStatus = TickResult.Failure;
        h.Tick();

        condition.UpdateCallCount.ShouldBe(1);
        then.UpdateCallCount.ShouldBe(2);
        otherwise.UpdateCallCount.ShouldBe(0);
    }
}

public sealed class ReactiveIfTests
{
    [Test]
    public void AChangedAnswerResetsTheRunningBranchAndRunsTheOther()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode condition = new() { ReturnStatus = TickResult.Success };
        MockNode then = new() { ReturnStatus = TickResult.Running };
        MockNode otherwise = new() { ReturnStatus = TickResult.Running };
        Harness h = new(n, n.ReactiveIf("if", condition, then, otherwise));

        h.Tick();
        condition.ReturnStatus = TickResult.Failure;
        h.Tick();

        then.ResetCount.ShouldBe(1);
        then.ResetStatus.ShouldBe(NodeStatus.Running);
        otherwise.UpdateCallCount.ShouldBe(1);
    }

    [Test]
    public void WithoutElseTheAnswerCanFlipBackToThen()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode condition = new() { ReturnStatus = TickResult.Failure };
        MockNode then = new() { ReturnStatus = TickResult.Running };
        Harness h = new(n, n.ReactiveIf("if", condition, then));

        h.Tick().ShouldBe(TickResult.Success);
        condition.ReturnStatus = TickResult.Success;

        h.Tick().ShouldBe(TickResult.Running);
        then.UpdateCallCount.ShouldBe(1);
    }

    [Test]
    public void BuildRejectsAnUninterruptibleBranch()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        BtNode<FakeClock> commit = n.Uninterruptible(
            n.Do("commit", static (in FakeClock _) => TickResult.Success)
        );

        Should.Throw<InvalidOperationException>(() =>
            n.Build(n.ReactiveIf("if", new MockNode(), commit))
        );
    }
}
