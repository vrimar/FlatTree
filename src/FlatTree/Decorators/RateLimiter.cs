namespace FlatTree;

/// <summary>
/// Caches the child's last non-Running result for <c>interval</c> of logical time,
/// only re-ticking the child once the interval has elapsed (or on the very first tick). Running
/// is never cached — while the child runs it is re-ticked every tick. <c>Cursor</c> bits 0-1 hold
/// the cached verdict, bit 2 marks that a verdict is held and bit 3 that a timestamp is held;
/// <c>Stamp</c> is the last non-Running timestamp (read only when the flag is set).
/// </summary>
/// <remarks>
/// <see cref="BtNode{TContext}.Reset"/> keeps the interval timer but drops the cached verdict: the
/// timer records what the agent did, while a node reporting <see cref="NodeStatus.Fresh"/> must not
/// replay a stale status. Reset mid-interval, it reports Failure — gated, with nothing to replay.
/// </remarks>
public sealed class RateLimiter<TContext> : DecoratorNode<TContext>
    where TContext : IClock
{
    private const int StatusMask = 0x3;
    private const int HasVerdictFlag = 0x4;
    private const int HasTimestampFlag = 0x8;

    private readonly long _intervalMs;

    internal RateLimiter(string name, BtNode<TContext> child, TimeSpan interval)
        : base(name, child)
    {
        _intervalMs = DurationGuard.ToMilliseconds(interval, nameof(interval));
    }

    /// <summary>The minimum interval between child evaluations.</summary>
    public TimeSpan Interval => TimeSpan.FromMilliseconds(_intervalMs);

    protected override TickResult Update(Span<NodeState> s, in TContext ctx)
    {
        ref var st = ref s[Id];
        var now = ctx.NowMs;

        if ((st.Cursor & HasTimestampFlag) != 0 && (now - st.Stamp) < _intervalMs)
        {
            // Ticking the child when the verdict is gone would bypass the interval.
            return (st.Cursor & HasVerdictFlag) != 0
                ? (TickResult)(st.Cursor & StatusMask)
                : TickResult.Failure;
        }

        var childStatus = Child.Tick(s, in ctx);

        if (childStatus != TickResult.Running)
        {
            st.Cursor = (int)childStatus | HasVerdictFlag | HasTimestampFlag;
            st.Stamp = now;
        }

        return childStatus;
    }

    protected override void DoReset(Span<NodeState> s, in TContext ctx)
    {
        s[Id].Cursor &= ~(StatusMask | HasVerdictFlag);
        base.DoReset(s, in ctx);
    }
}
