namespace FlatTree.Tests;

public sealed class CooldownTests
{
    private static bool OnCooldown(Harness h, BtNode<FakeClock> cooldown) =>
        (h.CursorOf(cooldown) & 1) != 0;

    [Test]
    public void WhenNotOnCooldownAndChildReturnsSuccess_ReturnSuccessAndGoOnCooldown()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode child = new MockNode { ReturnStatus = TickResult.Success };
        Cooldown<FakeClock> sut = n.Cooldown("Cooldown", TimeSpan.FromMilliseconds(1000), child);
        Harness h = new Harness(n, sut);

        h.Tick().ShouldBe(TickResult.Success);
        child.TerminateCallCount.ShouldBe(1);
        OnCooldown(h, sut).ShouldBeTrue();
    }

    [Test]
    public void WhenNotOnCooldownAndChildReturnsFailure_ReturnFailureAndDoNotGoOnCooldown()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode child = new MockNode { ReturnStatus = TickResult.Failure };
        Cooldown<FakeClock> sut = n.Cooldown("Cooldown", TimeSpan.FromMilliseconds(1000), child);
        Harness h = new Harness(n, sut);

        h.Tick().ShouldBe(TickResult.Failure);
        child.TerminateCallCount.ShouldBe(1);
        OnCooldown(h, sut).ShouldBeFalse();
    }

    [Test]
    public void WhenNotOnCooldownAndChildReturnsRunning_ReturnRunningAndDoNotGoOnCooldown()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode child = new MockNode { ReturnStatus = TickResult.Running };
        Cooldown<FakeClock> sut = n.Cooldown("Cooldown", TimeSpan.FromMilliseconds(1000), child);
        Harness h = new Harness(n, sut);

        h.Tick().ShouldBe(TickResult.Running);
        child.TerminateCallCount.ShouldBe(0);
        OnCooldown(h, sut).ShouldBeFalse();
    }

    [Test]
    public void WhenOnCooldown_ReturnFailure()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode child = new MockNode { ReturnStatus = TickResult.Success };
        Cooldown<FakeClock> sut = n.Cooldown("Cooldown", TimeSpan.FromMilliseconds(1000), child);
        Harness h = new Harness(n, sut);

        h.Tick();
        h.Tick().ShouldBe(TickResult.Failure);

        child.TerminateCallCount.ShouldBe(1);
        OnCooldown(h, sut).ShouldBeTrue();
    }

    [Test]
    public void WhenCooldownExpires_GoBackToRegularBehaviour()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode child = new MockNode { ReturnStatus = TickResult.Success };
        Cooldown<FakeClock> sut = n.Cooldown("Cooldown", TimeSpan.FromMilliseconds(1000), child);
        FakeClock clock = new FakeClock();
        Harness h = new Harness(n, sut, clock);

        h.Tick();
        clock.Advance(2000);
        h.Tick().ShouldBe(TickResult.Success);

        child.TerminateCallCount.ShouldBe(2);
        OnCooldown(h, sut).ShouldBeTrue();
    }
}
