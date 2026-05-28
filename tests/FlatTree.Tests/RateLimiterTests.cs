namespace FlatTree.Tests;

public sealed class RateLimiterTests
{
    [Test]
    public void WhenChildReturnsSuccess_ReturnSuccessAndCacheValue()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode child = new MockNode { ReturnStatus = TickResult.Success };
        Harness h = new Harness(
            n,
            n.RateLimiter("RateLimiter", TimeSpan.FromMilliseconds(1000), child)
        );

        h.Tick().ShouldBe(TickResult.Success);
        child.UpdateCallCount.ShouldBe(1);
        child.TerminateCallCount.ShouldBe(1);

        h.Tick().ShouldBe(TickResult.Success);
        child.UpdateCallCount.ShouldBe(1);
        child.TerminateCallCount.ShouldBe(1);
    }

    [Test]
    public void WhenChildReturnsFailure_ReturnFailureAndCacheValue()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode child = new MockNode { ReturnStatus = TickResult.Failure };
        Harness h = new Harness(
            n,
            n.RateLimiter("RateLimiter", TimeSpan.FromMilliseconds(1000), child)
        );

        h.Tick().ShouldBe(TickResult.Failure);
        child.UpdateCallCount.ShouldBe(1);
        child.TerminateCallCount.ShouldBe(1);

        h.Tick().ShouldBe(TickResult.Failure);
        child.UpdateCallCount.ShouldBe(1);
        child.TerminateCallCount.ShouldBe(1);
    }

    [Test]
    public void WhenChildReturnsRunning_ReturnRunningButDoNotCacheValue()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode child = new MockNode { ReturnStatus = TickResult.Running };
        Harness h = new Harness(
            n,
            n.RateLimiter("RateLimiter", TimeSpan.FromMilliseconds(1000), child)
        );

        h.Tick().ShouldBe(TickResult.Running);
        child.UpdateCallCount.ShouldBe(1);
        child.TerminateCallCount.ShouldBe(0);

        h.Tick().ShouldBe(TickResult.Running);
        child.UpdateCallCount.ShouldBe(2);
        child.TerminateCallCount.ShouldBe(0);
    }

    [Test]
    public void WhenCacheExpires_ChildMustBeReevaluated()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode child = new MockNode { ReturnStatus = TickResult.Success };
        FakeClock clock = new FakeClock();
        Harness h = new Harness(
            n,
            n.RateLimiter("RateLimiter", TimeSpan.FromMilliseconds(1000), child),
            clock
        );

        h.Tick().ShouldBe(TickResult.Success);
        child.UpdateCallCount.ShouldBe(1);
        child.TerminateCallCount.ShouldBe(1);

        clock.Advance(2000);
        child.ReturnStatus = TickResult.Failure;

        h.Tick().ShouldBe(TickResult.Failure);
        child.UpdateCallCount.ShouldBe(2);
        child.TerminateCallCount.ShouldBe(2);
    }
}
