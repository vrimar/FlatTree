namespace FlatTree.Tests;

public sealed class AlwaysSucceedTests
{
    [Test]
    [Arguments(TickResult.Success, TickResult.Success)]
    [Arguments(TickResult.Failure, TickResult.Success)]
    [Arguments(TickResult.Running, TickResult.Running)]
    public void MapsTerminalToSuccessAndPassesRunningThrough(
        TickResult childStatus,
        TickResult expected
    )
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode child = new MockNode { ReturnStatus = childStatus };
        Harness h = new Harness(n, n.AlwaysSucceed("AlwaysSucceed", child));

        h.Tick().ShouldBe(expected);
    }
}
