namespace FlatTree;

/// <summary>
/// Base for a leaf that starts work once and then polls until it lands: <see cref="Begin"/> runs on
/// the activating tick, <see cref="Poll"/> on that tick and every tick after, and
/// <see cref="OnTimeout"/> once the optional deadline passes. The base owns the started flag
/// (<c>Cursor</c> bit 0) and the start timestamp (<c>Stamp</c>), clears them when the node
/// completes, and calls <see cref="Cancel"/> when a started node is reset — the only notice a
/// preempted node gets that its outstanding work is being abandoned.
/// </summary>
/// <remarks>
/// <c>Cursor</c> bits 1-31 belong to the subclass, through <see cref="Scratch"/> /
/// <see cref="SetScratch"/>. A field on the node is shared by every agent.
/// </remarks>
public abstract class PollingLeaf<TContext> : LeafNode<TContext>
    where TContext : IClock
{
    private const int ScratchShift = 1;

    private readonly long _timeoutMs;
    private readonly ClockSelector<TContext> _clock;

    /// <summary>
    /// Creates the leaf. <paramref name="timeout"/> of <see cref="TimeSpan.Zero"/> polls forever.
    /// </summary>
    protected PollingLeaf(string name, TimeSpan timeout, ClockSelector<TContext>? clock = null)
        : base(name)
    {
        _timeoutMs =
            timeout == TimeSpan.Zero ? 0 : DurationGuard.ToMilliseconds(timeout, nameof(timeout));
        _clock = ClockGuard.Resolve(clock);
    }

    /// <summary>The deadline, or <see cref="TimeSpan.Zero"/> when the leaf polls forever.</summary>
    public TimeSpan Timeout => TimeSpan.FromMilliseconds(_timeoutMs);

    /// <summary>
    /// Starts the work, once per activation. Success proceeds to <see cref="Poll"/> on the same
    /// tick; Running yields the tick; Failure aborts without polling.
    /// </summary>
    protected abstract TickResult Begin(Span<NodeState> s, in TContext ctx);

    /// <summary>Reports whether the started work has landed. Running keeps the deadline counting.</summary>
    protected abstract TickResult Poll(Span<NodeState> s, in TContext ctx);

    /// <summary>What the leaf reports once the deadline passes with the work still outstanding.</summary>
    protected virtual TickResult OnTimeout(Span<NodeState> s, in TContext ctx) =>
        TickResult.Failure;

    /// <summary>
    /// Releases what <see cref="Begin"/> acquired, when a started node is reset rather than allowed
    /// to finish. Must be idempotent.
    /// </summary>
    protected virtual void Cancel(Span<NodeState> s, in TContext ctx) { }

    /// <summary>
    /// The subclass's per-agent scratch, <c>Cursor</c> bits 1-31. Unsigned shift: bit 31 is a value
    /// bit here, not a sign bit, so an arithmetic shift would read the top of the range back negative.
    /// </summary>
    protected int Scratch(ReadOnlySpan<NodeState> s) => (int)((uint)s[Id].Cursor >> ScratchShift);

    /// <summary>
    /// Writes the subclass's per-agent scratch, preserving the started flag.
    /// <paramref name="value"/> must be non-negative: 31 bits hold a value, not a sign.
    /// </summary>
    protected void SetScratch(Span<NodeState> s, int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);

        ref var st = ref s[Id];
        st.Cursor = (int)(((uint)value << ScratchShift) | (uint)(st.Cursor & NodeScratch.BegunFlag));
    }

    protected sealed override TickResult Update(Span<NodeState> s, in TContext ctx)
    {
        var now = _clock(in ctx);

        if (s[Id].TryBegin(now))
        {
            var started = Begin(s, in ctx);
            if (started != TickResult.Success)
            {
                return started;
            }
        }

        var polled = Poll(s, in ctx);
        if (polled != TickResult.Running)
        {
            return polled;
        }

        return _timeoutMs > 0 && s[Id].Since(now) >= _timeoutMs
            ? OnTimeout(s, in ctx)
            : TickResult.Running;
    }

    protected override void OnTerminate(Span<NodeState> s, TickResult status, in TContext ctx) =>
        s[Id].ClearScratch();

    protected override void DoReset(Span<NodeState> s, in TContext ctx)
    {
        if (s[Id].HasBegun())
        {
            Cancel(s, in ctx);
        }

        s[Id].ClearScratch();
    }
}
