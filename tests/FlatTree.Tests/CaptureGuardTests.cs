namespace FlatTree.Tests;

/// <summary>
/// The DEBUG-only guard that rejects capturing leaf delegates (a node is shared by every agent,
/// so a closure would leak one agent's state to all others).
/// </summary>
public sealed class CaptureGuardTests
{
#if DEBUG
    [Test]
    public void CapturingAction_IsRejected()
    {
        var n = Bt.For<FakeClock>();
        var captured = 0;

        Should.Throw<ArgumentException>(() =>
            n.Do(
                "bad",
                c =>
                {
                    captured++;
                    return TickResult.Success;
                }
            )
        );
    }

    [Test]
    public void CapturingPredicate_IsRejected()
    {
        var n = Bt.For<FakeClock>();
        var threshold = 5L;

        Should.Throw<ArgumentException>(() => n.Condition("bad", c => c.NowMs > threshold));
    }

    [Test]
    public void CapturingStatefulAction_IsRejected()
    {
        var n = Bt.For<FakeClock>();
        var captured = 0;

        Should.Throw<ArgumentException>(() =>
            n.Do(
                "bad",
                (in FakeClock c, ref int cursor, ref long stamp) =>
                {
                    captured++;
                    return TickResult.Success;
                }
            )
        );
    }
#endif

    [Test]
    public void NonCapturingDelegates_AreAccepted()
    {
        var n = Bt.For<FakeClock>();

        Should.NotThrow(() => n.Do("ok", static _ => TickResult.Success));
        Should.NotThrow(() => n.Condition("ok", static c => c.NowMs >= 0));
        Should.NotThrow(() => n.Do("method-group", AlwaysSucceed));
        Should.NotThrow(() =>
            n.Do(
                "stateful-ok",
                static (in FakeClock c, ref int cursor, ref long stamp) => TickResult.Success
            )
        );
    }

    private static TickResult AlwaysSucceed(FakeClock c) => TickResult.Success;
}
