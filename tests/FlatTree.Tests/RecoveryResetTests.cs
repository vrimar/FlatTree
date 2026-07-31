namespace FlatTree.Tests;

/// <summary>
/// An exception escaping a user delegate leaves every node on the throwing path reporting Fresh
/// over a dirty subtree, because <c>Tick</c> writes the status only after <c>Update</c> returns.
/// <c>Reset</c> short-circuits on Fresh and cannot recover that; <c>ResetAll</c> is the way back.
/// </summary>
public sealed class RecoveryResetTests
{
    private sealed class Boom : Exception { }

    /// <summary>
    /// Unlike <see cref="CleanupLeaf"/> this does NOT guard its cleanup behind a flag it clears, so
    /// it detects a <c>DoReset</c> that runs twice rather than absorbing it.
    /// </summary>
    private sealed class UnguardedLeaf : LeafNode<RecordingClock>
    {
        public UnguardedLeaf(string name)
            : base(name) { }

        public int ResetCount { get; private set; }

        protected override TickResult Update(Span<NodeState> s, in RecordingClock ctx)
        {
            s[Id].Cursor = 1;
            s[Id].Stamp = 99;
            return TickResult.Running;
        }

        protected override void DoReset(Span<NodeState> s, in RecordingClock ctx) => ResetCount++;
    }

    /// <summary>
    /// Acquires through the context and throws before writing its slot, so the slot still reads as
    /// untouched — the case a slot-inspecting sweep cannot see.
    /// </summary>
    private sealed class AcquireThenThrowLeaf : LeafNode<RecordingClock>
    {
        public AcquireThenThrowLeaf(string name)
            : base(name) { }

        protected override TickResult Update(Span<NodeState> s, in RecordingClock ctx) =>
            throw new Boom();

        protected override void DoReset(Span<NodeState> s, in RecordingClock ctx) =>
            ctx.Release($"{Name}:reset");
    }

    private static TickResult Succeed(in RecordingClock c) => TickResult.Success;

    private static TickResult Throw(in RecordingClock c) => throw new Boom();

    [Test]
    public void AfterAThrow_ResetIsANoOpBecauseTheRootStillReportsFresh()
    {
        var n = Bt.For<RecordingClock>();
        var held = new CleanupLeaf("trade-window", TickResult.Success);
        var root = n.Sequence("root", held, n.Do("boom", Throw));
        var tree = n.Build(root);
        var state = tree.NewState();
        var clock = new RecordingClock();

        Should.Throw<Boom>(() => tree.Tick(state, clock));

        state[root.Id].Status.ShouldBe(NodeStatus.Fresh);
        state[root.Id].Cursor.ShouldBe(1);

        tree.Reset(state, clock);

        state[root.Id].Cursor.ShouldBe(1);
    }

    [Test]
    public void AfterAThrow_ResetAllRewindsTheCursorSoNoChildIsSkipped()
    {
        var n = Bt.For<RecordingClock>();
        var first = new CleanupLeaf("first", TickResult.Success);
        var root = n.Sequence("root", first, n.Do("boom", Throw));
        var tree = n.Build(root);
        var state = tree.NewState();
        var clock = new RecordingClock();

        Should.Throw<Boom>(() => tree.Tick(state, clock));
        tree.ResetAll(state, clock);

        state[root.Id].Cursor.ShouldBe(0);
        state[first.Id].Status.ShouldBe(NodeStatus.Fresh);
    }

    [Test]
    public void AfterAThrow_ResetAllReachesALeafHoldingAResource()
    {
        var n = Bt.For<RecordingClock>();
        var held = new CleanupLeaf("trade-window");
        var tree = n.Build(
            n.SimpleParallel(
                "root",
                SimpleParallelPolicy.BothMustSucceed,
                n.Sequence("work", held),
                n.Do("boom", Throw)
            )
        );
        var state = tree.NewState();
        var clock = new RecordingClock();

        Should.Throw<Boom>(() => tree.Tick(state, clock));
        clock.Released.ShouldBeEmpty();

        tree.ResetAll(state, clock);

        clock.Released.ShouldBe(new[] { "trade-window:reset" });
    }

    [Test]
    public void ResetAllUnwindsDeepestAndLastAcquiredFirst()
    {
        var n = Bt.For<RecordingClock>();
        var a = new CleanupLeaf("a");
        var b = new CleanupLeaf("b");
        var tree = n.Build(
            n.SimpleParallel(
                "root",
                SimpleParallelPolicy.BothMustSucceed,
                n.Sequence("left", a),
                n.Sequence("right", b)
            )
        );
        var state = tree.NewState();
        var clock = new RecordingClock();

        tree.Tick(state, clock).ShouldBe(TickResult.Running);
        tree.ResetAll(state, clock);

        clock.Released.ShouldBe(new[] { "b:reset", "a:reset" });
    }

    [Test]
    public void ResetAllRunsDoResetExactlyOncePerNode()
    {
        var n = Bt.For<RecordingClock>();
        var left = new UnguardedLeaf("left");
        var right = new UnguardedLeaf("right");
        var tree = n.Build(
            n.SimpleParallel(
                "root",
                SimpleParallelPolicy.BothMustSucceed,
                n.Sequence("a", left),
                n.Sequence("b", right)
            )
        );
        var state = tree.NewState();
        var clock = new RecordingClock();

        tree.Tick(state, clock).ShouldBe(TickResult.Running);
        tree.ResetAll(state, clock);

        left.ResetCount.ShouldBe(1);
        right.ResetCount.ShouldBe(1);
    }

    [Test]
    public void ResetAllReachesANodeThatAcquiredThenThrewWithoutWritingItsSlot()
    {
        var n = Bt.For<RecordingClock>();
        var leaf = new AcquireThenThrowLeaf("handle");
        var tree = n.Build(n.Sequence("root", leaf));
        var state = tree.NewState();
        var clock = new RecordingClock();

        Should.Throw<Boom>(() => tree.Tick(state, clock));
        state[leaf.Id].ShouldBe(default(NodeState));

        tree.ResetAll(state, clock);

        clock.Released.ShouldBe(new[] { "handle:reset" });
    }

    [Test]
    public void ResetAllPreservesStateANodeDeliberatelyKeepsAcrossAReset()
    {
        var n = Bt.For<RecordingClock>();
        var gated = n.Cooldown("gate", TimeSpan.FromMilliseconds(1000), n.Do("act", Succeed));
        var tree = n.Build(gated);
        var state = tree.NewState();
        var clock = new RecordingClock();

        tree.Tick(state, clock).ShouldBe(TickResult.Success);

        tree.ResetAll(state, clock);

        clock.Advance(500);
        tree.Tick(state, clock).ShouldBe(TickResult.Failure);
    }

    // ResetAll cannot tell an untouched node from one that acquired and threw, so it notifies every
    // node; a guarded cleanup is what makes that a no-op.
    [Test]
    public void ResetAllNotifiesEveryNode_ButGuardedCleanupIsANoOpOnAPristineAgent()
    {
        var n = Bt.For<RecordingClock>();
        var guarded = new CleanupLeaf("untouched");
        var unguarded = new UnguardedLeaf("counted");
        var tree = n.Build(n.Selector("root", n.Sequence("branch", guarded), unguarded));
        var state = tree.NewState();
        var clock = new RecordingClock();

        tree.ResetAll(state, clock);

        clock.Released.ShouldBeEmpty();
        unguarded.ResetCount.ShouldBe(1);
    }

    [Test]
    public void ResetAllRejectsAStateArrayOfTheWrongLength()
    {
        var n = Bt.For<RecordingClock>();
        var tree = n.Build(n.Do("act", Succeed));
        var clock = new RecordingClock();

        Should.Throw<ArgumentException>(() => tree.ResetAll(new NodeState[99], clock));
    }

    [Test]
    public void PoolReturnAfterAThrowCleansTheSlotBeforeRecyclingIt()
    {
        var n = Bt.For<RecordingClock>();
        var held = new CleanupLeaf("trade-window");
        var tree = n.Build(
            n.SimpleParallel(
                "root",
                SimpleParallelPolicy.BothMustSucceed,
                n.Sequence("work", held),
                n.Do("boom", Throw)
            )
        );
        var pool = new BehaviourTreePool<RecordingClock>(tree, 2);
        var clock = new RecordingClock();
        var slot = pool.Rent();

        Should.Throw<Boom>(() => pool.Tick(slot, clock));

        pool.ResetAll(slot, clock);
        pool.Return(slot);

        clock.Released.ShouldBe(new[] { "trade-window:reset" });
        pool.Count.ShouldBe(0);
    }

    // Return's ordinary path must not invent a teardown for a branch the agent never entered.
    [Test]
    public void PoolReturnDoesNotNotifyABranchThatNeverTicked()
    {
        var n = Bt.For<RecordingClock>();
        var untouched = new AcquireThenThrowLeaf("untouched");
        var tree = n.Build(
            n.Selector("root", n.Sequence("taken", n.Do("ok", Succeed)), n.Sequence("skipped", untouched))
        );
        var pool = new BehaviourTreePool<RecordingClock>(tree, 1);
        var clock = new RecordingClock();
        var slot = pool.Rent();

        pool.Tick(slot, clock).ShouldBe(TickResult.Success);
        pool.Return(slot, clock);

        clock.Released.ShouldBeEmpty();
    }
}
