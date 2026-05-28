namespace FlatTree.Tests;

public sealed class AlwaysFailTests
{
    [Test]
    [Arguments(TickResult.Success, TickResult.Failure)]
    [Arguments(TickResult.Failure, TickResult.Failure)]
    [Arguments(TickResult.Running, TickResult.Running)]
    public void MapsTerminalToFailureAndPassesRunningThrough(
        TickResult childStatus,
        TickResult expected
    )
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode child = new MockNode { ReturnStatus = childStatus };
        Harness h = new Harness(n, n.AlwaysFail("AlwaysFail", child));

        h.Tick().ShouldBe(expected);
    }
}
