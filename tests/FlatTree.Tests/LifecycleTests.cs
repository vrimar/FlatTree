namespace FlatTree.Tests;

public sealed class LifecycleTests
{
    [Test]
    public void Reset_OnAFreshTree_DoesNotCascade()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode[] children = Enumerable
            .Range(0, 5)
            .Select(_ => new MockNode { ReturnStatus = TickResult.Running })
            .ToArray();
        Harness h = new Harness(n, n.Selector("Selector", children));

        h.ResetTree();

        children.ShouldAllBe(c => c.ResetCount == 0);
    }

    [Test]
    public void Reset_AfterTick_CascadesToCompositeChildren()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode a = new MockNode { ReturnStatus = TickResult.Failure };
        MockNode b = new MockNode { ReturnStatus = TickResult.Running };
        Selector<FakeClock> sut = n.Selector("Selector", a, b);
        Harness h = new Harness(n, sut);

        h.Tick();
        h.ResetTree();

        a.ResetCount.ShouldBe(1);
        b.ResetCount.ShouldBe(1);
        h.StatusOf(a).ShouldBe(NodeStatus.Fresh);
        h.StatusOf(b).ShouldBe(NodeStatus.Fresh);
        h.StatusOf(sut).ShouldBe(NodeStatus.Fresh);
    }

    [Test]
    public void Reset_AfterTick_CascadesToDecoratorChild()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode child = new MockNode { ReturnStatus = TickResult.Running };
        Harness h = new Harness(n, n.Inverter("Inverter", child));

        h.Tick();
        h.ResetTree();

        child.ResetCount.ShouldBe(1);
    }

    [Test]
    public void Tick_IsTheSingleWriterOfStatus()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode child = new MockNode { ReturnStatus = TickResult.Running };
        Selector<FakeClock> sut = n.Selector("Selector", child);
        Harness h = new Harness(n, sut);

        var returned = h.Tick();

        h.StatusOf(sut).ShouldBe((NodeStatus)returned);
        h.StatusOf(child).ShouldBe(NodeStatus.Running);
    }
}
