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
        var tree = n.Build(n.Do("d", static (in FakeClock _) => TickResult.Success));
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
        var tree = n.Build(n.Do("d", static (in FakeClock _) => TickResult.Success));
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
        pool.Reset(slot, new FakeClock());

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

    [Test]
    public void Constructor_RejectsNonPositiveCapacity()
    {
        var n = Bt.For<FakeClock>();
        var tree = n.Build(n.Do("d", static (in FakeClock _) => TickResult.Success));

        Should.Throw<ArgumentOutOfRangeException>(() => new BehaviourTreePool<FakeClock>(tree, 0));
        Should.Throw<ArgumentOutOfRangeException>(() => new BehaviourTreePool<FakeClock>(tree, -1));
    }

    // These guards used to be Debug.Assert, so in Release a double Return silently drove Count
    // negative and handed the same slot to two agents.
    [Test]
    public void Return_Twice_Throws()
    {
        var n = Bt.For<FakeClock>();
        var tree = n.Build(n.Do("d", static (in FakeClock _) => TickResult.Success));
        var pool = new BehaviourTreePool<FakeClock>(tree, 4);

        var slot = pool.Rent();
        pool.Return(slot);

        Should.Throw<InvalidOperationException>(() => pool.Return(slot));
        pool.Count.ShouldBe(0);
    }

    [Test]
    public void Return_WithAnOutOfRangeSlot_Throws()
    {
        var n = Bt.For<FakeClock>();
        var tree = n.Build(n.Do("d", static (in FakeClock _) => TickResult.Success));
        var pool = new BehaviourTreePool<FakeClock>(tree, 2);

        Should.Throw<ArgumentOutOfRangeException>(() => pool.Return(-1));
        Should.Throw<ArgumentOutOfRangeException>(() => pool.Return(2));
    }

    [Test]
    public void ReturnWithContext_ResetsTheSlotSoNodesReleaseWhatTheyHold()
    {
        var n = Bt.For<RecordingClock>();
        var tree = n.Build(n.Sequence("root", new CleanupLeaf("session")));
        var pool = new BehaviourTreePool<RecordingClock>(tree, 2);
        var clock = new RecordingClock();

        var slot = pool.Rent();
        pool.Tick(slot, clock).ShouldBe(TickResult.Running);
        clock.Released.ShouldBeEmpty();

        pool.Return(slot, clock);

        clock.Released.ShouldBe(new[] { "session:reset" });
        pool.Count.ShouldBe(0);
    }

    [Test]
    public void Return_WithoutContext_DoesNotRunCleanup()
    {
        var n = Bt.For<RecordingClock>();
        var tree = n.Build(n.Sequence("root", new CleanupLeaf("session")));
        var pool = new BehaviourTreePool<RecordingClock>(tree, 2);
        var clock = new RecordingClock();

        var slot = pool.Rent();
        pool.Tick(slot, clock).ShouldBe(TickResult.Running);

        pool.Return(slot);

        // Rent zeroes the slot's memory; it does not run node cleanup. Hence the ctx overload.
        clock.Released.ShouldBeEmpty();
        pool.Rent().ShouldBe(slot);
        clock.Released.ShouldBeEmpty();
    }

    // Slot ownership is enforced in Release too, not just via Debug.Assert: serving a returned slot
    // lets it be re-rented while a stale caller still holds it, giving two agents one state block.
    [Test]
    public void UsingASlotAfterReturn_Throws()
    {
        var n = Bt.For<FakeClock>();
        var leaf = n.Wait("w", TimeSpan.FromMilliseconds(1000));
        var tree = n.Build(leaf);
        var pool = new BehaviourTreePool<FakeClock>(tree, 2);
        var clock = new FakeClock();

        var slot = pool.Rent();
        pool.Tick(slot, clock).ShouldBe(TickResult.Running);
        pool.Return(slot);

        Should.Throw<InvalidOperationException>(() => pool.Tick(slot, clock));
        Should.Throw<InvalidOperationException>(() => pool.Reset(slot, clock));
        Should.Throw<InvalidOperationException>(() => pool.StatusOf(slot, leaf));
        Should.Throw<InvalidOperationException>(() => pool.Return(slot, clock));
    }

    [Test]
    public void UsingAnOutOfRangeSlot_Throws()
    {
        var n = Bt.For<FakeClock>();
        var tree = n.Build(n.Do("d", static (in FakeClock _) => TickResult.Success));
        var pool = new BehaviourTreePool<FakeClock>(tree, 2);
        var clock = new FakeClock();

        Should.Throw<ArgumentOutOfRangeException>(() => pool.Tick(-1, clock));
        Should.Throw<ArgumentOutOfRangeException>(() => pool.Tick(2, clock));
        Should.Throw<ArgumentOutOfRangeException>(() => pool.Reset(2, clock));
    }

    [Test]
    public void ARecycledSlotIsUsableAgain()
    {
        var n = Bt.For<FakeClock>();
        var tree = n.Build(n.Do("d", static (in FakeClock _) => TickResult.Success));
        var pool = new BehaviourTreePool<FakeClock>(tree, 1);
        var clock = new FakeClock();

        var first = pool.Rent();
        pool.Return(first);

        var second = pool.Rent();
        second.ShouldBe(first);
        pool.Tick(second, clock).ShouldBe(TickResult.Success);
    }

    [Test]
    public void SlotsAreRecycledAcrossManyRentReturnCycles()
    {
        var n = Bt.For<FakeClock>();
        var tree = n.Build(n.Do("d", static (in FakeClock _) => TickResult.Success));
        var pool = new BehaviourTreePool<FakeClock>(tree, 2);

        for (int i = 0; i < 10; i++)
        {
            var a = pool.Rent();
            var b = pool.Rent();
            pool.Count.ShouldBe(2);

            Should.Throw<InvalidOperationException>(() => pool.Rent());

            pool.Return(a);
            pool.Return(b);
            pool.Count.ShouldBe(0);
        }
    }
}
