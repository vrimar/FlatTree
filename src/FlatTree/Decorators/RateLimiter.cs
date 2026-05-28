namespace FlatTree;

/// <summary>
/// Caches the child's last non-Running result for <paramref name="interval"/> of logical time,
/// only re-ticking the child once the interval has elapsed (or on the very first tick). Running
/// is never cached — while the child runs it is re-ticked every tick. <c>Cursor</c> low byte
/// holds the last child status; bit 8 is the "has a cached timestamp" flag; <c>Stamp</c> is
/// the last non-Running timestamp (read only when the flag is set).
/// </summary>
public sealed class RateLimiter<TContext> : DecoratorNode<TContext>
    where TContext : IClock
{
    private const int StatusMask = 0xFF;
    private const int HasTimestampFlag = 0x100;

    private readonly long _intervalMs;

    internal RateLimiter(string name, BtNode<TContext> child, TimeSpan interval)
        : base(name, child)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
            interval,
            TimeSpan.Zero,
            nameof(interval)
        );
        _intervalMs = (long)interval.TotalMilliseconds;
    }

    /// <summary>The minimum interval between child evaluations.</summary>
    public TimeSpan Interval => TimeSpan.FromMilliseconds(_intervalMs);

    protected override TickResult Update(Span<NodeState> s, in TContext ctx)
    {
        ref var st = ref s[Id];
        var now = ctx.NowMs;

        var hasTimestamp = (st.Cursor & HasTimestampFlag) != 0;

        if (!hasTimestamp || (now - st.Stamp) >= _intervalMs)
        {
            var childStatus = Child.Tick(s, in ctx);

            if (childStatus != TickResult.Running)
            {
                st.Cursor = (int)childStatus | HasTimestampFlag;
                st.Stamp = now;
            }
            else
            {
                // Running is never cached: record the status for return, keep the existing
                // timestamp flag untouched.
                st.Cursor = (int)childStatus | (st.Cursor & HasTimestampFlag);
            }

            return childStatus;
        }

        return (TickResult)(byte)(st.Cursor & StatusMask);
    }
}
