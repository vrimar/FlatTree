namespace FlatTree.Tests;

public sealed class InverterTests
{
    [Test]
    [Arguments(TickResult.Success, TickResult.Failure)]
    [Arguments(TickResult.Failure, TickResult.Success)]
    [Arguments(TickResult.Running, TickResult.Running)]
    public void InvertsTerminalStatusAndPassesRunningThrough(
        TickResult childStatus,
        TickResult expected
    )
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode child = new MockNode { ReturnStatus = childStatus };
        Harness h = new Harness(n, n.Inverter("Inverter", child));

        h.Tick().ShouldBe(expected);
    }
}
