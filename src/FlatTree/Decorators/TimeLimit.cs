namespace FlatTree;

/// <summary>
/// Ticks the child until a logical-time limit elapses, then returns Failure (without ticking
/// the child) and resets the child + timer. <c>Cursor</c> bit 0 is the started flag;
/// <c>Stamp</c> is the initial timestamp (read only when started, per the sentinel discipline).
/// </summary>
public sealed class TimeLimit<TContext> : DecoratorNode<TContext>
    where TContext : IClock
{
    private const int StartedFlag = 1;

    private readonly long _limitMs;

    internal TimeLimit(string name, BtNode<TContext> child, TimeSpan limit)
        : base(name, child)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(limit, TimeSpan.Zero, nameof(limit));
        _limitMs = (long)limit.TotalMilliseconds;
    }

    /// <summary>The time limit.</summary>
    public TimeSpan Limit => TimeSpan.FromMilliseconds(_limitMs);

    protected override TickResult Update(Span<NodeState> s, in TContext ctx)
    {
        ref var st = ref s[Id];
        var now = ctx.NowMs;

        if ((st.Cursor & StartedFlag) == 0)
        {
            st.Cursor |= StartedFlag;
            st.Stamp = now;
        }

        if ((now - st.Stamp) >= _limitMs)
        {
            return TickResult.Failure;
        }

        return Child.Tick(s, in ctx);
    }

    protected override void OnTerminate(Span<NodeState> s, TickResult status, in TContext ctx)
    {
        s[Id].Cursor &= ~StartedFlag;
        Child.Reset(s, in ctx);
    }

    protected override void DoReset(Span<NodeState> s, in TContext ctx)
    {
        s[Id].Cursor &= ~StartedFlag;
        base.DoReset(s, in ctx);
    }
}
