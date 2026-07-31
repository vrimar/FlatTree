namespace FlatTree;

/// <summary>
/// Caches the child's last non-Running result for <c>interval</c> of logical time,
/// only re-ticking the child once the interval has elapsed (or on the very first tick). Running
/// is never cached — while the child runs it is re-ticked every tick. <c>Cursor</c> bits 0-1 hold
/// the cached verdict and bit 2 marks that one is held; <c>Stamp</c> is the timestamp it was cached
/// at (read only when the flag is set).
/// </summary>
/// <remarks>
/// <see cref="BtNode{TContext}.Reset"/> keeps both the interval timer and the cached verdict: they
/// record what the agent did, not where it is in the tree, so preempting the branch must not refund
/// them. This is what makes the node useful beneath a composite, which resets its children whenever
/// it completes. For an agent with no history, use
/// <see cref="BehaviourTree{TContext}.NewState"/> or a recycled pool slot.
/// </remarks>
public sealed class RateLimiter<TContext> : DecoratorNode<TContext>
    where TContext : IClock
{
    private const int StatusMask = 0x3;
    private const int HasVerdictFlag = 0x4;

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

        if ((st.Cursor & HasVerdictFlag) != 0 && (now - st.Stamp) < _intervalMs)
        {
            return (TickResult)(st.Cursor & StatusMask);
        }

        var childStatus = Child.Tick(s, in ctx);

        if (childStatus != TickResult.Running)
        {
            st.Cursor = (int)childStatus | HasVerdictFlag;
            st.Stamp = now;
        }

        return childStatus;
    }
}
