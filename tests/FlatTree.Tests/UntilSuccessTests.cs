namespace FlatTree.Tests;

public sealed class UntilSuccessTests
{
    [Test]
    public void WhenChildReturnsFailure_RepeatChildAndReturnRunning()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode child = new MockNode { ReturnStatus = TickResult.Failure };
        Harness h = new Harness(n, n.UntilSuccess("UntilSuccess", child));

        for (int i = 0; i < 10; i++)
        {
            h.Tick().ShouldBe(TickResult.Running);
            child.TerminateCallCount.ShouldBe(i + 1);
        }
    }

    [Test]
    public void WhenChildReturnsSuccess_ReturnSuccess()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode child = new MockNode { ReturnStatus = TickResult.Success };
        Harness h = new Harness(n, n.UntilSuccess("UntilSuccess", child));

        h.Tick().ShouldBe(TickResult.Success);
    }

    [Test]
    public void WhenChildReturnsRunning_ReturnRunningWithoutResettingChild()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode child = new MockNode { ReturnStatus = TickResult.Running };
        Harness h = new Harness(n, n.UntilSuccess("UntilSuccess", child));

        for (int i = 0; i < 10; i++)
        {
            h.Tick().ShouldBe(TickResult.Running);
            child.TerminateCallCount.ShouldBe(0);
            child.ResetCount.ShouldBe(0);
        }
    }
}
