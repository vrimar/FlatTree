namespace FlatTree.Tests;

/// <summary>
/// A <c>StatefulDo</c> keeps multi-tick progress in its own <c>Cursor</c>/<c>Stamp</c> scratch.
/// A reset must clear it, otherwise an aborted action resumes mid-flight the next time its branch
/// is selected — while reporting itself Fresh.
/// </summary>
public sealed class StatefulDoResetTests
{
    private static TickResult ThreeStep(in FakeClock c, ref int cursor, ref long stamp)
    {
        cursor++;
        stamp = c.NowMs;
        return cursor >= 3 ? TickResult.Success : TickResult.Running;
    }

    [Test]
    public void ScratchPersistsAcrossTicksUntilTheActionCompletes()
    {
        var n = Bt.For<FakeClock>();
        var node = n.Do("work", ThreeStep);
        var harness = new Harness(n, node);

        harness.Tick().ShouldBe(TickResult.Running);
        harness.CursorOf(node).ShouldBe(1);

        harness.Tick().ShouldBe(TickResult.Running);
        harness.CursorOf(node).ShouldBe(2);

        harness.Tick().ShouldBe(TickResult.Success);
    }

    [Test]
    public void ResetClearsTheScratch_SoTheActionRestartsRatherThanResuming()
    {
        var n = Bt.For<FakeClock>();
        var node = n.Do("work", ThreeStep);
        var harness = new Harness(n, node);

        harness.Tick().ShouldBe(TickResult.Running);
        harness.Tick().ShouldBe(TickResult.Running);
        harness.CursorOf(node).ShouldBe(2);

        harness.ResetTree();

        harness.CursorOf(node).ShouldBe(0);
        harness.StampOf(node).ShouldBe(0);
        harness.StatusOf(node).ShouldBe(NodeStatus.Fresh);

        harness.Tick().ShouldBe(TickResult.Running);
        harness.CursorOf(node).ShouldBe(1);
    }

    [Test]
    public void AbortByAPriorityParent_ClearsTheScratch()
    {
        var n = Bt.For<FakeClock>();
        var node = n.Do("work", ThreeStep);
        var clock = new FakeClock();
        var harness = new Harness(
            n,
            n.PrioritySelector(
                "root",
                n.Condition("guard", static (in FakeClock c) => c.NowMs >= 1000),
                node
            ),
            clock
        );

        harness.Tick().ShouldBe(TickResult.Running);
        harness.CursorOf(node).ShouldBe(1);

        clock.Advance(1000);
        harness.Tick().ShouldBe(TickResult.Success);

        harness.CursorOf(node).ShouldBe(0);
        harness.StampOf(node).ShouldBe(0);
    }

    [Test]
    public void CompletingClearsTheScratch_SoASecondRunUnderCooldownRestarts()
    {
        var n = Bt.For<FakeClock>();
        var node = n.Do("work", ThreeStep);
        var clock = new FakeClock();
        var harness = new Harness(
            n,
            n.Cooldown("cd", TimeSpan.FromMilliseconds(500), node),
            clock
        );

        harness.Tick().ShouldBe(TickResult.Running);
        harness.Tick().ShouldBe(TickResult.Running);
        harness.Tick().ShouldBe(TickResult.Success);
        harness.CursorOf(node).ShouldBe(0);

        // Cooldown does not reset its child, so a leaf that kept its scratch would report Success
        // on the first tick of the next run instead of taking three ticks again.
        clock.Advance(500);

        harness.Tick().ShouldBe(TickResult.Running);
        harness.Tick().ShouldBe(TickResult.Running);
        harness.Tick().ShouldBe(TickResult.Success);
    }

    [Test]
    public void CompletingClearsTheScratch_SoUntilSuccessRestartsEachAttempt()
    {
        var n = Bt.For<FakeClock>();
        var node = n.Do(
            "flaky",
            static (in FakeClock c, ref int cursor, ref long stamp) =>
            {
                cursor++;
                return cursor >= 2 ? TickResult.Failure : TickResult.Running;
            }
        );
        var harness = new Harness(n, n.UntilFailed("until", node));

        harness.Tick().ShouldBe(TickResult.Running);
        harness.CursorOf(node).ShouldBe(1);

        harness.Tick().ShouldBe(TickResult.Success);
        harness.CursorOf(node).ShouldBe(0);
    }
}
