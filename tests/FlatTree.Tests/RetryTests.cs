namespace FlatTree.Tests;

public sealed class RetryTests
{
    [Test]
    public void WhenChildFailsBelowTheAttemptCap_ResetItAndReturnRunning()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode child = new MockNode { ReturnStatus = TickResult.Failure };
        Retry<FakeClock> sut = n.Retry("Retry", 4, child);
        Harness h = new Harness(n, sut);

        for (int i = 0; i < 3; i++)
        {
            h.Tick().ShouldBe(TickResult.Running);
            h.CursorOf(sut).ShouldBe(i + 1);
        }

        child.InitializeCallCount.ShouldBe(3);
    }

    [Test]
    public void WhenTheLastAttemptFails_ReturnFailureAndResetCounter()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode child = new MockNode { ReturnStatus = TickResult.Failure };
        Retry<FakeClock> sut = n.Retry("Retry", 3, child);
        Harness h = new Harness(n, sut);

        h.Tick().ShouldBe(TickResult.Running);
        h.Tick().ShouldBe(TickResult.Running);
        h.Tick().ShouldBe(TickResult.Failure);

        h.CursorOf(sut).ShouldBe(0);
    }

    [Test]
    public void WhenChildSucceeds_ReturnSuccessAndResetCounter()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode child = new MockNode { ReturnStatus = TickResult.Failure };
        Retry<FakeClock> sut = n.Retry("Retry", 5, child);
        Harness h = new Harness(n, sut);

        h.Tick().ShouldBe(TickResult.Running);
        child.ReturnStatus = TickResult.Success;
        h.Tick().ShouldBe(TickResult.Success);

        h.CursorOf(sut).ShouldBe(0);
    }

    [Test]
    public void WhenChildRuns_PassRunningThroughWithoutSpendingAnAttempt()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode child = new MockNode { ReturnStatus = TickResult.Running };
        Retry<FakeClock> sut = n.Retry("Retry", 3, child);
        Harness h = new Harness(n, sut);

        h.Tick().ShouldBe(TickResult.Running);
        h.Tick().ShouldBe(TickResult.Running);

        h.CursorOf(sut).ShouldBe(0);
        child.TerminateCallCount.ShouldBe(0);
    }

    [Test]
    public void OneAttemptIsPlainPassThrough()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode child = new MockNode { ReturnStatus = TickResult.Failure };
        Retry<FakeClock> sut = n.Retry(1, child);
        Harness h = new Harness(n, sut);

        h.Tick().ShouldBe(TickResult.Failure);
    }

    [Test]
    public void WhenResettingMidRetry_ClearTheCounter()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode child = new MockNode { ReturnStatus = TickResult.Failure };
        Retry<FakeClock> sut = n.Retry("Retry", 10, child);
        Harness h = new Harness(n, sut);

        h.Tick();
        h.Tick();
        h.ResetTree();

        h.CursorOf(sut).ShouldBe(0);
    }

    [Test]
    public void AttemptsBelowOneIsRejected()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();

        Should.Throw<ArgumentOutOfRangeException>(() =>
            n.Retry(0, new MockNode { ReturnStatus = TickResult.Success })
        );
    }
}
