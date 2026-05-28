namespace FlatTree.Tests;

/// <summary>F11: SimpleParallel accepts N children (params), not just two.</summary>
public sealed class SimpleParallelManyChildrenTests
{
    private static MockNode[] Children(int count, TickResult status) =>
        Enumerable.Range(0, count).Select(_ => new MockNode { ReturnStatus = status }).ToArray();

    [Test]
    public void Both_WithThreeChildren_AllSucceed_ReturnsSuccess()
    {
        var n = Bt.For<FakeClock>();
        var children = Children(3, TickResult.Success);
        var h = new Harness(
            n,
            n.SimpleParallel("p", SimpleParallelPolicy.BothMustSucceed, children)
        );

        h.Tick().ShouldBe(TickResult.Success);
        children.ShouldAllBe(c => c.UpdateCallCount == 1);
    }

    [Test]
    public void Both_WithThreeChildren_OneFails_ReturnsFailure()
    {
        var n = Bt.For<FakeClock>();
        var children = Children(3, TickResult.Success);
        children[2].ReturnStatus = TickResult.Failure;
        var h = new Harness(
            n,
            n.SimpleParallel("p", SimpleParallelPolicy.BothMustSucceed, children)
        );

        h.Tick().ShouldBe(TickResult.Failure);
    }

    [Test]
    public void OnlyOne_WithFiveChildren_AllFail_ReturnsFailure()
    {
        var n = Bt.For<FakeClock>();
        var children = Children(5, TickResult.Failure);
        var h = new Harness(
            n,
            n.SimpleParallel("p", SimpleParallelPolicy.OnlyOneMustSucceed, children)
        );

        h.Tick().ShouldBe(TickResult.Failure);
    }

    [Test]
    public void OnlyOne_WithFiveChildren_OneSucceeds_ReturnsSuccess()
    {
        var n = Bt.For<FakeClock>();
        var children = Children(5, TickResult.Running);
        children[3].ReturnStatus = TickResult.Success;
        var h = new Harness(
            n,
            n.SimpleParallel("p", SimpleParallelPolicy.OnlyOneMustSucceed, children)
        );

        h.Tick().ShouldBe(TickResult.Success);
    }

    [Test]
    public void Both_RunningChildrenAreReTicked_CompletedOnesAreNot()
    {
        var n = Bt.For<FakeClock>();
        var children = Children(3, TickResult.Running);
        children[0].ReturnStatus = TickResult.Success;
        var h = new Harness(
            n,
            n.SimpleParallel("p", SimpleParallelPolicy.BothMustSucceed, children)
        );

        h.Tick().ShouldBe(TickResult.Running);
        h.Tick().ShouldBe(TickResult.Running);

        // The completed child is ticked once; the still-running ones twice.
        children[0].UpdateCallCount.ShouldBe(1);
        children[1].UpdateCallCount.ShouldBe(2);
        children[2].UpdateCallCount.ShouldBe(2);
    }

    [Test]
    public void Constructor_AllowsUpToMaxChildren()
    {
        var n = Bt.For<FakeClock>();
        var children = Children(SimpleParallel<FakeClock>.MaxChildren, TickResult.Success);

        Should.NotThrow(() =>
            n.SimpleParallel("p", SimpleParallelPolicy.BothMustSucceed, children)
        );
    }

    [Test]
    public void Constructor_RejectsMoreThanMaxChildren()
    {
        var n = Bt.For<FakeClock>();
        var children = Children(SimpleParallel<FakeClock>.MaxChildren + 1, TickResult.Success);

        Should.Throw<ArgumentException>(() =>
            n.SimpleParallel("p", SimpleParallelPolicy.BothMustSucceed, children)
        );
    }
}
