namespace FlatTree.Tests;

public sealed class UntilFailedTests
{
    [Test]
    public void WhenChildReturnsSuccess_RepeatChildAndReturnRunning()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode child = new MockNode { ReturnStatus = TickResult.Success };
        Harness h = new Harness(n, n.UntilFailed("UntilFailed", child));

        for (int i = 0; i < 10; i++)
        {
            h.Tick().ShouldBe(TickResult.Running);
            child.TerminateCallCount.ShouldBe(i + 1);
        }
    }

    [Test]
    public void WhenChildReturnsFailure_ReturnSuccess()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode child = new MockNode { ReturnStatus = TickResult.Failure };
        Harness h = new Harness(n, n.UntilFailed("UntilFailed", child));

        h.Tick().ShouldBe(TickResult.Success);
    }

    [Test]
    public void WhenChildReturnsRunning_ReturnRunningWithoutResettingChild()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode child = new MockNode { ReturnStatus = TickResult.Running };
        Harness h = new Harness(n, n.UntilFailed("UntilFailed", child));

        for (int i = 0; i < 10; i++)
        {
            h.Tick().ShouldBe(TickResult.Running);
            child.TerminateCallCount.ShouldBe(0);
            child.ResetCount.ShouldBe(0);
        }
    }
}
