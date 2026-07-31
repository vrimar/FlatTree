namespace FlatTree.Tests;

/// <summary>
/// The build-time guard that rejects capturing leaf delegates (a node is shared by every agent,
/// so a closure would leak one agent's state to all others).
/// </summary>
public sealed class CaptureGuardTests
{
    [Test]
    public void CapturingAction_IsRejected()
    {
        var n = Bt.For<FakeClock>();
        var captured = 0;

        Should.Throw<ArgumentException>(() =>
            n.Do(
                "bad",
                (in FakeClock c) =>
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

        Should.Throw<ArgumentException>(() => n.Condition("bad", (in FakeClock c) => c.NowMs > threshold));
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

    private class StatefulBase
    {
        private int _calls;

        protected void Bump() => _calls++;
    }

    private sealed class Service : StatefulBase
    {
        public TickResult Act(in FakeClock c)
        {
            Bump();
            return TickResult.Success;
        }
    }

    // A multicast delegate reports only its last entry's Target, so the capturing half would
    // otherwise be invisible to the guard.
    [Test]
    public void MulticastDelegate_IsRejectedEvenWhenTheLastEntryIsStatic()
    {
        var n = Bt.For<FakeClock>();
        var captured = 0;

        LeafAction<FakeClock> capturing = (in FakeClock c) =>
        {
            captured++;
            return TickResult.Success;
        };
        LeafAction<FakeClock> pure = static (in FakeClock _) => TickResult.Success;

        Should.Throw<ArgumentException>(() => n.Do("bad", capturing + pure));
    }

    // GetFields does not return private fields declared on base types.
    [Test]
    public void MethodGroupOnAnObjectWithInheritedPrivateState_IsRejected()
    {
        var n = Bt.For<FakeClock>();

        Should.Throw<ArgumentException>(() => n.Do("bad", new Service().Act));
    }

    [Test]
    public void NonCapturingDelegates_AreAccepted()
    {
        var n = Bt.For<FakeClock>();

        Should.NotThrow(() => n.Do("ok", static (in FakeClock _) => TickResult.Success));
        Should.NotThrow(() => n.Condition("ok", static (in FakeClock c) => c.NowMs >= 0));
        Should.NotThrow(() => n.Do("method-group", AlwaysSucceed));
        Should.NotThrow(() =>
            n.Do(
                "stateful-ok",
                static (in FakeClock c, ref int cursor, ref long stamp) => TickResult.Success
            )
        );
    }

    private static TickResult AlwaysSucceed(in FakeClock c) => TickResult.Success;
}
