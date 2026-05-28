namespace FlatTree.Tests;

public sealed class WaitTests
{
    [Test]
    public void WhenStarted_ReturnRunning()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        Harness h = new Harness(n, n.Wait("Wait", TimeSpan.FromMilliseconds(1000)));

        h.Tick().ShouldBe(TickResult.Running);
    }

    [Test]
    public void WhenWaitTimeExpires_ReturnSuccessAndReArm()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        FakeClock clock = new FakeClock();
        Harness h = new Harness(n, n.Wait("Wait", TimeSpan.FromMilliseconds(1000)), clock);

        h.Tick();
        clock.Advance(2000);
        h.Tick().ShouldBe(TickResult.Success);

        // Auto re-arms after success.
        h.Tick().ShouldBe(TickResult.Running);
    }

    [Test]
    public void WhenResetIsCalled_RestartTimer()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        FakeClock clock = new FakeClock();
        Harness h = new Harness(n, n.Wait("Wait", TimeSpan.FromMilliseconds(1000)), clock);

        clock.Advance(500);
        h.Tick().ShouldBe(TickResult.Running);

        h.ResetTree();
        clock.Advance(600);

        // Timer restarted at 1100ms, so only 0ms elapsed since start => still running.
        h.Tick().ShouldBe(TickResult.Running);
    }

    [Test]
    public void WhenFirstTickIsAtTimeZero_StartsCorrectly()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        FakeClock clock = new FakeClock();
        Harness h = new Harness(n, n.Wait("Wait", TimeSpan.FromMilliseconds(1000)), clock);

        // NowMs == 0 at first tick: the started flag (not Stamp == 0) discriminates "started".
        h.Tick().ShouldBe(TickResult.Running);
        clock.Advance(1000);
        h.Tick().ShouldBe(TickResult.Success);
    }
}
