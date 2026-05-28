namespace FlatTree.Tests;

public sealed class PrioritySequenceTests
{
    [Test]
    public void WhenAllChildrenReturnSuccess_ReturnSuccess()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode[] children = Enumerable
            .Range(0, 10)
            .Select(_ => new MockNode { ReturnStatus = TickResult.Success })
            .ToArray();
        Harness h = new Harness(n, n.PrioritySequence("PrioritySequence", children));

        TickResult status = h.Tick();

        status.ShouldBe(TickResult.Success);
        children.ShouldAllBe(c => c.InitializeCallCount == 1);
        children.ShouldAllBe(c => c.UpdateCallCount == 1);
        children.ShouldAllBe(c => c.TerminateCallCount == 1);
    }

    [Test]
    [Arguments(TickResult.Failure)]
    [Arguments(TickResult.Running)]
    public void WhenAChildDoesNotReturnSuccess_ReturnTheSameAndDoNotCallNextChild(TickResult status)
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode[] children = Enumerable
            .Range(0, 10)
            .Select(_ => new MockNode { ReturnStatus = TickResult.Success })
            .ToArray();
        children[4].ReturnStatus = status;
        Harness h = new Harness(n, n.PrioritySequence("PrioritySequence", children));

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
    public void WhenTicked_ReevaluateAllPreviousChildren()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode[] children = Enumerable
            .Range(0, 10)
            .Select(_ => new MockNode { ReturnStatus = TickResult.Success })
            .ToArray();
        children[4].ReturnStatus = TickResult.Running;
        Harness h = new Harness(n, n.PrioritySequence("PrioritySequence", children));

        h.Tick();
        children[4].ReturnStatus = TickResult.Success;
        h.Tick();

        for (int i = 0; i < 4; i++)
        {
            children[i].InitializeCallCount.ShouldBe(1);
            children[i].UpdateCallCount.ShouldBe(2);
            children[i].TerminateCallCount.ShouldBe(2);
        }

        children[4].InitializeCallCount.ShouldBe(1);
        children[4].UpdateCallCount.ShouldBe(2);
        children[4].TerminateCallCount.ShouldBe(1);

        for (int i = 5; i < children.Length; i++)
        {
            children[i].InitializeCallCount.ShouldBe(1);
            children[i].UpdateCallCount.ShouldBe(1);
            children[i].TerminateCallCount.ShouldBe(1);
        }
    }

    [Test]
    public void WhenAReevaluatedChildReturnsFailure_ReturnFailureAndResetChildren()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode[] children = Enumerable
            .Range(0, 10)
            .Select(_ => new MockNode { ReturnStatus = TickResult.Success })
            .ToArray();
        children[4].ReturnStatus = TickResult.Running;
        Harness h = new Harness(n, n.PrioritySequence("PrioritySequence", children));

        h.Tick();
        children[0].ReturnStatus = TickResult.Failure;
        h.Tick();

        children[0].InitializeCallCount.ShouldBe(1);
        children[0].UpdateCallCount.ShouldBe(2);
        children[0].TerminateCallCount.ShouldBe(2);
        children[0].ResetCount.ShouldBe(1);

        for (int i = 1; i < 4; i++)
        {
            children[i].InitializeCallCount.ShouldBe(1);
            children[i].UpdateCallCount.ShouldBe(1);
            children[i].TerminateCallCount.ShouldBe(1);
            children[i].ResetCount.ShouldBe(1);
        }

        children[4].InitializeCallCount.ShouldBe(1);
        children[4].UpdateCallCount.ShouldBe(1);
        children[4].TerminateCallCount.ShouldBe(0);
        children[4].ResetCount.ShouldBe(1);

        for (int i = 5; i < children.Length; i++)
        {
            children[i].InitializeCallCount.ShouldBe(0);
            children[i].UpdateCallCount.ShouldBe(0);
            children[i].TerminateCallCount.ShouldBe(0);
            children[i].ResetCount.ShouldBe(0);
        }
    }
}
