namespace FlatTree;

/// <summary>
/// Leaf that returns Running until its duration of logical time has elapsed since it started, then
/// Success. Auto re-arms after success (so it can be used again). <c>Cursor</c> bit 0 is the started
/// flag; <c>Stamp</c> is the deadline (read only when started, per the sentinel discipline).
/// </summary>
/// <remarks>
/// A <c>min</c>/<c>max</c> range draws a fresh duration per activation, which keeps a fleet on
/// identical schedules from landing its work on the same tick.
/// <para>
/// Because it re-arms, this leaf should not be a non-final child of a
/// <see cref="PrioritySelector{TContext}"/>/<see cref="PrioritySequence{TContext}"/>: those
/// re-evaluate from index 0 every tick, so it alternates Success/Running and resets the branch behind
/// it on every restart — multi-tick work there never finishes. Put the tail under a
/// <see cref="Sequence{TContext}"/>, which resumes from its cursor.
/// </para>
/// </remarks>
public sealed class Wait<TContext> : LeafNode<TContext>
    where TContext : IClock
{
    private readonly long _minMs;
    private readonly long _maxMs;
    private readonly DurationOf<TContext>? _durationOf;
    private readonly ClockSelector<TContext> _clock;
    private readonly RandomSource<TContext> _randomSource;

    internal Wait(
        string name,
        TimeSpan min,
        TimeSpan max,
        ClockSelector<TContext>? clock,
        RandomSource<TContext> randomSource
    )
        : base(name)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(max, min, nameof(max));

        // Zero is a legitimate "succeed immediately"; a sub-millisecond value truncates to it.
        _minMs = min == TimeSpan.Zero ? 0 : DurationGuard.ToMilliseconds(min, nameof(min));
        _maxMs = max == TimeSpan.Zero ? 0 : DurationGuard.ToMilliseconds(max, nameof(max));
        _clock = ClockGuard.Resolve(clock);
        _randomSource = randomSource;
    }

    internal Wait(
        string name,
        DurationOf<TContext> duration,
        ClockSelector<TContext>? clock,
        RandomSource<TContext> randomSource
    )
        : base(name)
    {
        BtGuard.RequireNoCapture(duration, nameof(duration));
        _durationOf = duration;
        _clock = ClockGuard.Resolve(clock);
        _randomSource = randomSource;
    }

    /// <summary>
    /// The wait duration, or the low end of the range it draws from. Zero when the duration comes
    /// from a <see cref="DurationOf{TContext}"/>, which only an agent's context can answer.
    /// </summary>
    public TimeSpan Duration => TimeSpan.FromMilliseconds(_minMs);

    /// <summary>The high end of the drawn range; equal to <see cref="Duration"/> when fixed.</summary>
    public TimeSpan MaxDuration => TimeSpan.FromMilliseconds(_maxMs);

    protected override TickResult Update(Span<NodeState> s, in TContext ctx)
    {
        ref var st = ref s[Id];
        var now = _clock(in ctx);

        // Stamp is the deadline, not the start: a drawn duration has nowhere else to live.
        if (!st.HasBegun())
        {
            st.Cursor |= NodeScratch.BegunFlag;
            st.Stamp = now + Draw(in ctx);
        }

        return now >= st.Stamp ? TickResult.Success : TickResult.Running;
    }

    protected override void OnTerminate(Span<NodeState> s, TickResult status, in TContext ctx) =>
        s[Id].ClearBegun();

    protected override void DoReset(Span<NodeState> s, in TContext ctx) => s[Id].ClearScratch();

    private long Draw(in TContext ctx)
    {
        if (_durationOf is { } accessor)
        {
            var drawn = accessor(in ctx);
            ArgumentOutOfRangeException.ThrowIfLessThan(drawn, TimeSpan.Zero, "duration");
            return (long)drawn.TotalMilliseconds;
        }

        // NextDouble is [0, 1), so the drawn range is [min, max).
        var span = _maxMs - _minMs;
        return span == 0 ? _minMs : _minMs + (long)(_randomSource(in ctx).NextDouble() * span);
    }
}
