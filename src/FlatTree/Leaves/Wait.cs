namespace FlatTree;

/// <summary>
/// Leaf that returns Running until <c>duration</c> of logical time has elapsed
/// since it started, then Success. Auto re-arms after success (so it can be used again).
/// <c>Cursor</c> bit 0 is the started flag; <c>Stamp</c> is the start timestamp (read only when
/// started, per the sentinel discipline).
/// </summary>
public sealed class Wait<TContext> : LeafNode<TContext>
    where TContext : IClock
{
    private const int StartedFlag = 1;

    private readonly long _durationMs;

    internal Wait(string name, TimeSpan duration)
        : base(name)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.Zero, nameof(duration));
        _durationMs = (long)duration.TotalMilliseconds;
    }

    /// <summary>The wait duration.</summary>
    public TimeSpan Duration => TimeSpan.FromMilliseconds(_durationMs);

    protected override TickResult Update(Span<NodeState> s, in TContext ctx)
    {
        ref var st = ref s[Id];
        var now = ctx.NowMs;

        if ((st.Cursor & StartedFlag) == 0)
        {
            st.Cursor |= StartedFlag;
            st.Stamp = now;
        }

        return (now - st.Stamp) >= _durationMs ? TickResult.Success : TickResult.Running;
    }

    protected override void OnTerminate(Span<NodeState> s, TickResult status, in TContext ctx)
    {
        s[Id].Cursor &= ~StartedFlag;
    }

    protected override void DoReset(Span<NodeState> s, in TContext ctx)
    {
        s[Id].Cursor &= ~StartedFlag;
    }
}
