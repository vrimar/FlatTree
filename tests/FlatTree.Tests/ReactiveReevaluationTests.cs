namespace FlatTree.Tests;

/// <summary>
/// A priority composite re-ticks from child 0 every tick, so a leaf that re-arms itself on success
/// (<c>Wait</c>, or a <c>Do</c> with scratch) oscillates as a non-final child: it reports Success on
/// one tick, then restarts and reports Running on the next, resetting everything behind it. Work that
/// completes inside that one Success tick still runs; work that needs several ticks never finishes.
/// These tests pin both halves — the shape that starves multi-tick work, and the <c>Sequence</c>
/// wrapper that fixes it.
/// </summary>
public sealed class ReactiveReevaluationTests
{
    private static TickResult Succeed(in FakeClock c) => TickResult.Success;

    private static bool True(in FakeClock c) => true;

    [Test]
    public void AWaitDirectlyUnderAReactiveParent_StarvesMultiTickWorkBehindIt()
    {
        var n = Bt.For<FakeClock>();
        var wait = n.Wait("wind-up", TimeSpan.FromMilliseconds(200));
        var channel = new MockNode { ReturnStatus = TickResult.Running };
        var clock = new FakeClock();
        var h = new Harness(
            n,
            n.PrioritySequence("attack", n.Condition("has-target", True), wait, channel),
            clock
        );

        for (int i = 0; i < 21; i++)
        {
            h.Tick().ShouldBe(TickResult.Running);
            clock.Advance(100);
        }

        // The Wait re-arms whenever it succeeds, so the next tick reports Running again and resets the
        // branch behind it. Every tick the channelled action receives is therefore a fresh start — it
        // never gets two in a row, so multi-tick work there can never finish.
        channel.UpdateCallCount.ShouldBeGreaterThan(1);
        channel.InitializeCallCount.ShouldBe(channel.UpdateCallCount);
    }

    [Test]
    public void WrappingTheTailInASequence_LetsTheTimedBranchProgress()
    {
        var n = Bt.For<FakeClock>();
        var strike = new MockNode { ReturnStatus = TickResult.Success };
        var clock = new FakeClock();
        var h = new Harness(
            n,
            n.PrioritySequence(
                "attack",
                n.Condition("has-target", True),
                n.Sequence("swing", n.Wait("wind-up", TimeSpan.FromMilliseconds(200)), strike)
            ),
            clock
        );

        h.Tick().ShouldBe(TickResult.Running);
        clock.Advance(100);
        h.Tick().ShouldBe(TickResult.Running);
        strike.UpdateCallCount.ShouldBe(0);

        // Sequence resumes from its cursor, so the satisfied Wait is not re-ticked.
        clock.Advance(100);
        h.Tick().ShouldBe(TickResult.Success);
        strike.UpdateCallCount.ShouldBe(1);
    }

    [Test]
    public void AGuardAheadOfTheSequenceStaysReactive()
    {
        var n = Bt.For<FakeClock>();
        var strike = new MockNode { ReturnStatus = TickResult.Running };
        var clock = new FakeClock();
        var h = new Harness(
            n,
            n.PrioritySequence(
                "attack",
                n.Condition("has-target", static (in FakeClock c) => c.NowMs < 300),
                n.Sequence("swing", n.Wait("wind-up", TimeSpan.FromMilliseconds(100)), strike)
            ),
            clock
        );

        h.Tick().ShouldBe(TickResult.Running);
        clock.Advance(100);
        h.Tick().ShouldBe(TickResult.Running);
        strike.UpdateCallCount.ShouldBe(1);

        // Guard drops: the running branch is torn down even though it was mid-flight.
        clock.Advance(300);
        h.Tick().ShouldBe(TickResult.Failure);
        strike.ResetCount.ShouldBe(1);
    }

    [Test]
    public void AScratchLeafDirectlyUnderAReactiveParent_AlsoOscillates()
    {
        var n = Bt.For<FakeClock>();
        var node = n.Do(
            "multi-tick",
            static (in FakeClock c, ref int cursor, ref long stamp) =>
            {
                cursor++;
                return cursor >= 2 ? TickResult.Success : TickResult.Running;
            }
        );
        var after = new MockNode { ReturnStatus = TickResult.Running };
        var h = new Harness(n, n.PrioritySequence("root", node, after));

        for (int i = 0; i < 12; i++)
        {
            h.Tick();
        }

        // Same shape as the Wait case: the scratch clears on success, so the action restarts and the
        // branch behind it never gets two consecutive ticks.
        after.UpdateCallCount.ShouldBeGreaterThan(1);
        after.InitializeCallCount.ShouldBe(after.UpdateCallCount);
    }

    [Test]
    public void APlainSequenceDoesNotReevaluateSatisfiedGuards()
    {
        var n = Bt.For<FakeClock>();
        var guard = new MockNode { ReturnStatus = TickResult.Success };
        var body = new MockNode { ReturnStatus = TickResult.Running };
        var h = new Harness(n, n.Sequence("root", guard, body));

        h.Tick().ShouldBe(TickResult.Running);
        h.Tick().ShouldBe(TickResult.Running);
        h.Tick().ShouldBe(TickResult.Running);

        guard.UpdateCallCount.ShouldBe(1);
        body.UpdateCallCount.ShouldBe(3);
    }
}
