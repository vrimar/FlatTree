namespace FlatTree;

/// <summary>
/// After the child succeeds, blocks (returns Failure without ticking the child) for
/// <paramref name="duration"/> of logical time. The cooldown starts ONLY on child success.
/// <c>Cursor</c> bit 0 is the on-cooldown flag; <c>Stamp</c> is the cooldown-start timestamp
/// (read only when the flag is set, per the sentinel discipline).
/// </summary>
public sealed class Cooldown<TContext> : DecoratorNode<TContext>
    where TContext : IClock
{
    private const int OnCooldownFlag = 1;

    private readonly long _durationMs;

    internal Cooldown(string name, BtNode<TContext> child, TimeSpan duration)
        : base(name, child)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
            duration,
            TimeSpan.Zero,
            nameof(duration)
        );
        _durationMs = (long)duration.TotalMilliseconds;
    }

    /// <summary>The cooldown duration.</summary>
    public TimeSpan Duration => TimeSpan.FromMilliseconds(_durationMs);

    protected override TickResult Update(Span<NodeState> s, in TContext ctx)
    {
        ref var st = ref s[Id];

        if ((st.Cursor & OnCooldownFlag) != 0)
        {
            var elapsed = ctx.NowMs - st.Stamp;

            if (elapsed < _durationMs)
            {
                return TickResult.Failure;
            }

            // Cooldown expired: exit and fall through to regular behaviour.
            st.Cursor &= ~OnCooldownFlag;
            st.Stamp = 0;
        }

        var childStatus = Child.Tick(s, in ctx);

        if (childStatus == TickResult.Success)
        {
            st.Cursor |= OnCooldownFlag;
            st.Stamp = ctx.NowMs;
        }

        return childStatus;
    }
}
