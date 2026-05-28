namespace FlatTree.Tests;

/// <summary>
/// <see cref="BehaviourTreePool{TContext}"/>: contiguous per-agent state, O(1) rent/return,
/// recycled-slot clearing, capacity limit, and cross-slot isolation on the shared tree.
/// </summary>
public sealed class BehaviourTreePoolTests
{
    [Test]
    public void Rent_Tick_Return_TracksCountAndCapacity()
    {
        var n = Bt.For<FakeClock>();
        var tree = n.Build(n.Do("d", static _ => TickResult.Success));
        var pool = new BehaviourTreePool<FakeClock>(tree, 4);

        pool.Capacity.ShouldBe(4);
        pool.Count.ShouldBe(0);

        var slot = pool.Rent();
        pool.Count.ShouldBe(1);
        pool.Tick(slot, new FakeClock()).ShouldBe(TickResult.Success);

        pool.Return(slot);
        pool.Count.ShouldBe(0);
    }

    [Test]
    public void Rent_BeyondCapacity_Throws()
    {
        var n = Bt.For<FakeClock>();
        var tree = n.Build(n.Do("d", static _ => TickResult.Success));
        var pool = new BehaviourTreePool<FakeClock>(tree, 1);

        pool.Rent();

        Should.Throw<InvalidOperationException>(() => pool.Rent());
    }

    [Test]
    public void RecycledSlot_IsClearedToFresh()
    {
        var n = Bt.For<FakeClock>();
        var wait = n.Wait("w", TimeSpan.FromMilliseconds(1000));
        var tree = n.Build(wait);
        var pool = new BehaviourTreePool<FakeClock>(tree, 1);

        var a = pool.Rent();
        pool.Tick(a, new FakeClock()).ShouldBe(TickResult.Running);
        pool.StatusOf(a, wait).ShouldBe(NodeStatus.Running);
        pool.Return(a);

        var b = pool.Rent();
        b.ShouldBe(a); // same underlying slot
        pool.StatusOf(b, wait).ShouldBe(NodeStatus.Fresh);
    }

    [Test]
    public void Reset_RewindsASlot()
    {
        var n = Bt.For<FakeClock>();
        var wait = n.Wait("w", TimeSpan.FromMilliseconds(1000));
        var tree = n.Build(wait);
        var pool = new BehaviourTreePool<FakeClock>(tree, 2);

        var slot = pool.Rent();
        pool.Tick(slot, new FakeClock()).ShouldBe(TickResult.Running);
        pool.Reset(slot);

        pool.StatusOf(slot, wait).ShouldBe(NodeStatus.Fresh);
    }

    [Test]
    public void TwoSlots_TickingSameTree_StayIsolated()
    {
        var n = Bt.For<FakeClock>();
        var tree = n.Build(n.Wait("w", TimeSpan.FromMilliseconds(1000)));
        var pool = new BehaviourTreePool<FakeClock>(tree, 4);

        var a = pool.Rent();
        var b = pool.Rent();
        var clockA = new FakeClock();
        var clockB = new FakeClock();
        clockB.Advance(500);

        pool.Tick(a, clockA).ShouldBe(TickResult.Running);
        pool.Tick(b, clockB).ShouldBe(TickResult.Running);

        clockA.Advance(1000); // A: elapsed 1000 => done
        clockB.Advance(400); // B: elapsed 400 => still waiting

        pool.Tick(a, clockA).ShouldBe(TickResult.Success);
        pool.Tick(b, clockB).ShouldBe(TickResult.Running);
    }
}
