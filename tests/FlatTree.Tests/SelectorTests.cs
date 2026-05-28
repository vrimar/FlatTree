namespace FlatTree.Tests;

public sealed class SelectorTests
{
    [Test]
    public void WhenAllChildrenReturnFailure_ReturnFailure()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode[] children = Enumerable
            .Range(0, 10)
            .Select(_ => new MockNode { ReturnStatus = TickResult.Failure })
            .ToArray();
        Harness h = new Harness(n, n.Selector("Selector", children));

        TickResult status = h.Tick();

        status.ShouldBe(TickResult.Failure);
        children.ShouldAllBe(c => c.InitializeCallCount == 1);
        children.ShouldAllBe(c => c.UpdateCallCount == 1);
        children.ShouldAllBe(c => c.TerminateCallCount == 1);
    }

    [Test]
    [Arguments(TickResult.Success)]
    [Arguments(TickResult.Running)]
    public void WhenAChildReturnsSuccessOrRunning_ReturnTheSameAndDoNotCallNextChild(
        TickResult status
    )
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode[] children = Enumerable
            .Range(0, 10)
            .Select(_ => new MockNode { ReturnStatus = TickResult.Failure })
            .ToArray();
        children[4].ReturnStatus = status;
        Harness h = new Harness(n, n.Selector("Selector", children));

        TickResult result = h.Tick();

        result.ShouldBe(status);

        for (int i = 0; i < 4; i++)
        {
            children[i].InitializeCallCount.ShouldBe(1);
            children[i].UpdateCallCount.ShouldBe(1);
        }

        for (int i = 5; i < children.Length; i++)
        {
            children[i].InitializeCallCount.ShouldBe(0);
            children[i].UpdateCallCount.ShouldBe(0);
            children[i].TerminateCallCount.ShouldBe(0);
        }
    }

    [Test]
    public void WhenAChildTakesMultipleTicksToComplete_ResumeSelectorFromRunningChild()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode[] children = Enumerable
            .Range(0, 10)
            .Select(_ => new MockNode { ReturnStatus = TickResult.Failure })
            .ToArray();
        children[4].ReturnStatus = TickResult.Running;
        Harness h = new Harness(n, n.Selector("Selector", children));

        h.Tick();
        children[4].ReturnStatus = TickResult.Success;
        h.Tick();

        for (int i = 0; i < 4; i++)
        {
            children[i].InitializeCallCount.ShouldBe(1);
            children[i].UpdateCallCount.ShouldBe(1);
            children[i].TerminateCallCount.ShouldBe(1);
        }

        children[4].InitializeCallCount.ShouldBe(1);
        children[4].UpdateCallCount.ShouldBe(2);
        children[4].TerminateCallCount.ShouldBe(1);

        for (int i = 5; i < children.Length; i++)
        {
            children[i].InitializeCallCount.ShouldBe(0);
            children[i].UpdateCallCount.ShouldBe(0);
            children[i].TerminateCallCount.ShouldBe(0);
        }
    }
}
