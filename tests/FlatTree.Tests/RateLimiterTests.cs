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

    [Test]
    public void AtTheExactInterval_TheChildIsReevaluated()
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
        clock.Advance(999);
        h.Tick();
        child.UpdateCallCount.ShouldBe(1);

        clock.Advance(1);
        h.Tick();
        child.UpdateCallCount.ShouldBe(2);
    }

    [Test]
    public void ResetKeepsTheIntervalTimerButDropsTheCachedVerdict()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode child = new MockNode { ReturnStatus = TickResult.Success };
        FakeClock clock = new FakeClock();
        RateLimiter<FakeClock> sut = n.RateLimiter(
            "RateLimiter",
            TimeSpan.FromMilliseconds(1000),
            child
        );
        Harness h = new Harness(n, sut, clock);

        h.Tick().ShouldBe(TickResult.Success);

        h.ResetTree();
        h.StatusOf(sut).ShouldBe(NodeStatus.Fresh);

        // Still gated, so the child must not be re-ticked; but the stale Success must not be
        // replayed by a node reporting itself Fresh.
        clock.Advance(500);
        h.Tick().ShouldBe(TickResult.Failure);
        child.UpdateCallCount.ShouldBe(1);

        // Once the interval elapses the child is evaluated again as normal.
        clock.Advance(500);
        h.Tick().ShouldBe(TickResult.Success);
        child.UpdateCallCount.ShouldBe(2);
    }
}
