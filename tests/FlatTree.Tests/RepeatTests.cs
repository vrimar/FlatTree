namespace FlatTree.Tests;

public sealed class RepeatTests
{
    [Test]
    public void WhileRepeatCountNotReached_ReturnRunning()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode child = new MockNode { ReturnStatus = TickResult.Success };
        Repeat<FakeClock> sut = n.Repeat("Repeat", 10, child);
        Harness h = new Harness(n, sut);

        for (int i = 0; i < 9; i++)
        {
            h.Tick().ShouldBe(TickResult.Running);
            h.CursorOf(sut).ShouldBe(i + 1);
            child.TerminateCallCount.ShouldBe(i + 1);
        }
    }

    [Test]
    public void WhenRepeatCountIsReached_ReturnSuccessAndResetCounter()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode child = new MockNode { ReturnStatus = TickResult.Success };
        Repeat<FakeClock> sut = n.Repeat("Repeat", 10, child);
        Harness h = new Harness(n, sut);

        var status = TickResult.Running;
        for (int i = 0; i < 10; i++)
        {
            status = h.Tick();
        }

        status.ShouldBe(TickResult.Success);
        h.CursorOf(sut).ShouldBe(0);

        h.Tick().ShouldBe(TickResult.Running);
        h.CursorOf(sut).ShouldBe(1);
    }

    [Test]
    public void WhenChildReturnsFailure_ReturnFailureAndResetCounter()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode child = new MockNode { ReturnStatus = TickResult.Success };
        Repeat<FakeClock> sut = n.Repeat("Repeat", 10, child);
        Harness h = new Harness(n, sut);

        h.Tick();
        child.ReturnStatus = TickResult.Failure;
        h.Tick().ShouldBe(TickResult.Failure);

        h.CursorOf(sut).ShouldBe(0);
    }

    [Test]
    public void WhenChildReturnsRunning_ReturnRunning()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode child = new MockNode { ReturnStatus = TickResult.Running };
        Repeat<FakeClock> sut = n.Repeat("Repeat", 10, child);
        Harness h = new Harness(n, sut);

        for (int i = 0; i < 10; i++)
        {
            h.Tick().ShouldBe(TickResult.Running);
            child.TerminateCallCount.ShouldBe(0);
            h.CursorOf(sut).ShouldBe(0);
        }
    }

    [Test]
    public void WhenResettingWhileRunning_ReInitializeCounter()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode child = new MockNode { ReturnStatus = TickResult.Running };
        Repeat<FakeClock> sut = n.Repeat("Repeat", 15, child);
        Harness h = new Harness(n, sut);

        h.Tick();
        h.Tick();
        h.Tick();

        h.ResetTree();

        h.CursorOf(sut).ShouldBe(0);
    }

    [Test]
    public void ChildIsFreshBetweenIterations()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode child = new MockNode { ReturnStatus = TickResult.Success };
        Repeat<FakeClock> sut = n.Repeat("Repeat", 5, child);
        Harness h = new Harness(n, sut);

        // Each of the first 4 successes resets the child (so it re-initializes each time).
        for (int i = 0; i < 4; i++)
        {
            h.Tick().ShouldBe(TickResult.Running);
        }

        child.InitializeCallCount.ShouldBe(4);
        h.Tick().ShouldBe(TickResult.Success);
        child.UpdateCallCount.ShouldBe(5);
    }
}
