namespace FlatTree.Tests;

public sealed class AutoResetTests
{
    [Test]
    [Arguments(TickResult.Failure)]
    [Arguments(TickResult.Success)]
    public void WhenChildTerminates_ResetChild(TickResult status)
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode child = new MockNode { ReturnStatus = status };
        Harness h = new Harness(n, n.AutoReset("AutoReset", child));

        h.Tick();

        child.ResetCount.ShouldBe(1);
    }

    [Test]
    public void WhenChildIsRunning_ContinueRunning()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode child = new MockNode { ReturnStatus = TickResult.Running };
        Harness h = new Harness(n, n.AutoReset("AutoReset", child));

        h.Tick();

        child.ResetCount.ShouldBe(0);
    }

    [Test]
    [Arguments(TickResult.Success)]
    [Arguments(TickResult.Running)]
    [Arguments(TickResult.Failure)]
    public void OnTick_ShouldReturnChildStatus(TickResult status)
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode child = new MockNode { ReturnStatus = status };
        Harness h = new Harness(n, n.AutoReset("AutoReset", child));

        h.Tick().ShouldBe(status);
    }
}
