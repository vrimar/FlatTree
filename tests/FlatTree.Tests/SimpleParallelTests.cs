namespace FlatTree.Tests;

public sealed class SimpleParallelTests
{
    [Test]
    [Arguments(SimpleParallelPolicy.OnlyOneMustSucceed)]
    [Arguments(SimpleParallelPolicy.BothMustSucceed)]
    public void WhenFirstTicked_ChildrenShouldAllBeStarted(SimpleParallelPolicy policy)
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode first = new MockNode { ReturnStatus = TickResult.Running };
        MockNode second = new MockNode { ReturnStatus = TickResult.Running };
        Harness h = new Harness(n, n.SimpleParallel("SimpleParallel", policy, first, second));

        h.Tick();

        first.InitializeCallCount.ShouldBe(1);
        second.InitializeCallCount.ShouldBe(1);
        first.UpdateCallCount.ShouldBe(1);
        second.UpdateCallCount.ShouldBe(1);
    }

    [Test]
    [Arguments(SimpleParallelPolicy.OnlyOneMustSucceed)]
    [Arguments(SimpleParallelPolicy.BothMustSucceed)]
    public void WhenTicked_RunningChildrenShouldAllBeTicked(SimpleParallelPolicy policy)
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode first = new MockNode { ReturnStatus = TickResult.Running };
        MockNode second = new MockNode { ReturnStatus = TickResult.Running };
        Harness h = new Harness(n, n.SimpleParallel("SimpleParallel", policy, first, second));

        h.Tick();
        h.Tick();

        first.UpdateCallCount.ShouldBe(2);
        second.UpdateCallCount.ShouldBe(2);
    }

    [Test]
    [Arguments(SimpleParallelPolicy.OnlyOneMustSucceed)]
    [Arguments(SimpleParallelPolicy.BothMustSucceed)]
    public void WhenTickedAfterCompletion_ChildrenShouldAllBeTicked(SimpleParallelPolicy policy)
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode first = new MockNode { ReturnStatus = TickResult.Success };
        MockNode second = new MockNode { ReturnStatus = TickResult.Success };
        Harness h = new Harness(n, n.SimpleParallel("SimpleParallel", policy, first, second));

        h.Tick();
        h.Tick();

        first.UpdateCallCount.ShouldBe(2);
        second.UpdateCallCount.ShouldBe(2);
    }

    // --- OnlyOneMustSucceed ---

    [Test]
    public void OnlyOne_WhenOneFailsAndOtherIsRunning_ReturnRunning()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode first = new MockNode { ReturnStatus = TickResult.Running };
        MockNode second = new MockNode { ReturnStatus = TickResult.Failure };
        Harness h = new Harness(
            n,
            n.SimpleParallel(
                "SimpleParallel",
                SimpleParallelPolicy.OnlyOneMustSucceed,
                first,
                second
            )
        );

        h.Tick().ShouldBe(TickResult.Running);
    }

    [Test]
    public void OnlyOne_WhenOneSucceeds_ReturnSuccessAndResetBoth()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode first = new MockNode { ReturnStatus = TickResult.Success };
        MockNode second = new MockNode { ReturnStatus = TickResult.Running };
        Harness h = new Harness(
            n,
            n.SimpleParallel(
                "SimpleParallel",
                SimpleParallelPolicy.OnlyOneMustSucceed,
                first,
                second
            )
        );

        h.Tick().ShouldBe(TickResult.Success);
        first.ResetCount.ShouldBe(1);
        second.ResetCount.ShouldBe(1);
    }

    [Test]
    public void OnlyOne_WhenBothFail_ReturnFailure()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode first = new MockNode { ReturnStatus = TickResult.Failure };
        MockNode second = new MockNode { ReturnStatus = TickResult.Failure };
        Harness h = new Harness(
            n,
            n.SimpleParallel(
                "SimpleParallel",
                SimpleParallelPolicy.OnlyOneMustSucceed,
                first,
                second
            )
        );

        h.Tick().ShouldBe(TickResult.Failure);
    }

    // --- BothMustSucceed ---

    [Test]
    public void Both_WhenSomeChildrenAreStillRunning_CompletedChildrenShouldNotBeTicked()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode first = new MockNode { ReturnStatus = TickResult.Success };
        MockNode second = new MockNode { ReturnStatus = TickResult.Running };
        Harness h = new Harness(
            n,
            n.SimpleParallel("SimpleParallel", SimpleParallelPolicy.BothMustSucceed, first, second)
        );

        h.Tick();
        h.Tick();

        first.UpdateCallCount.ShouldBe(1);
        second.UpdateCallCount.ShouldBe(2);
    }

    [Test]
    public void Both_WhenOneSucceedsAndOtherIsRunning_ReturnRunning()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode first = new MockNode { ReturnStatus = TickResult.Success };
        MockNode second = new MockNode { ReturnStatus = TickResult.Running };
        Harness h = new Harness(
            n,
            n.SimpleParallel("SimpleParallel", SimpleParallelPolicy.BothMustSucceed, first, second)
        );

        h.Tick().ShouldBe(TickResult.Running);
    }

    [Test]
    public void Both_WhenOneFails_ReturnFailureAndResetBoth()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode first = new MockNode { ReturnStatus = TickResult.Failure };
        MockNode second = new MockNode { ReturnStatus = TickResult.Success };
        Harness h = new Harness(
            n,
            n.SimpleParallel("SimpleParallel", SimpleParallelPolicy.BothMustSucceed, first, second)
        );

        h.Tick().ShouldBe(TickResult.Failure);
        first.ResetCount.ShouldBe(1);
        second.ResetCount.ShouldBe(1);
    }

    [Test]
    public void Both_WhenBothSucceed_ReturnSuccess()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode first = new MockNode { ReturnStatus = TickResult.Success };
        MockNode second = new MockNode { ReturnStatus = TickResult.Success };
        Harness h = new Harness(
            n,
            n.SimpleParallel("SimpleParallel", SimpleParallelPolicy.BothMustSucceed, first, second)
        );

        h.Tick().ShouldBe(TickResult.Success);
    }
}
