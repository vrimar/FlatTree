namespace FlatTree;

/// <summary>
/// Ticks the child until a logical-time limit elapses, then returns Failure (without ticking
/// the child) and resets the child + timer. <c>Cursor</c> bit 0 is the started flag;
/// <c>Stamp</c> is the initial timestamp (read only when started, per the sentinel discipline).
/// </summary>
public sealed class TimeLimit<TContext> : DecoratorNode<TContext>
    where TContext : IClock
{
    private readonly long _limitMs;
    private readonly ClockSelector<TContext> _clock;

    internal TimeLimit(
        string name,
        BtNode<TContext> child,
        TimeSpan limit,
        ClockSelector<TContext>? clock
    )
        : base(name, child)
    {
        _limitMs = DurationGuard.ToMilliseconds(limit, nameof(limit));
        _clock = ClockGuard.Resolve(clock);
    }

    /// <summary>The time limit.</summary>
    public TimeSpan Limit => TimeSpan.FromMilliseconds(_limitMs);

    protected override TickResult Update(Span<NodeState> s, in TContext ctx)
    {
        ref var st = ref s[Id];
        var now = _clock(in ctx);

        st.TryBegin(now);

        if (st.Since(now) >= _limitMs)
        {
            return TickResult.Failure;
        }

        return Child.Tick(s, in ctx);
    }

    protected override void OnTerminate(Span<NodeState> s, TickResult status, in TContext ctx)
    {
        s[Id].ClearBegun();
        Child.Reset(s, in ctx);
    }

    protected override void DoReset(Span<NodeState> s, in TContext ctx)
    {
        s[Id].ClearScratch();
        base.DoReset(s, in ctx);
    }

    protected internal override bool PreemptsRunningChildren => true;
}
