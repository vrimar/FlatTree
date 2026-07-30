namespace FlatTree;

/// <summary>
/// After the child succeeds, blocks (returns Failure without ticking the child) for
/// <c>duration</c> of logical time. The cooldown starts ONLY on child success.
/// <c>Cursor</c> bit 0 is the on-cooldown flag; <c>Stamp</c> is the cooldown-start timestamp
/// (read only when the flag is set, per the sentinel discipline).
/// </summary>
/// <remarks>
/// <see cref="BtNode{TContext}.Reset"/> deliberately does NOT clear the cooldown: the timer records
/// what the agent did, not where it is in the tree, so preempting the branch must not refund it. For
/// an agent with no history, use <see cref="BehaviourTree{TContext}.NewState"/> or a recycled pool slot.
/// </remarks>
public sealed class Cooldown<TContext> : DecoratorNode<TContext>
    where TContext : IClock
{
    private const int OnCooldownFlag = 1;

    private readonly long _durationMs;

    internal Cooldown(string name, BtNode<TContext> child, TimeSpan duration)
        : base(name, child)
    {
        _durationMs = DurationGuard.ToMilliseconds(duration, nameof(duration));
    }

    /// <summary>The cooldown duration.</summary>
    public TimeSpan Duration => TimeSpan.FromMilliseconds(_durationMs);

    protected override TickResult Update(Span<NodeState> s, in TContext ctx)
    {
        ref var st = ref s[Id];
        var now = ctx.NowMs;

        if ((st.Cursor & OnCooldownFlag) != 0)
        {
            if ((now - st.Stamp) < _durationMs)
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
            st.Stamp = now;
        }

        return childStatus;
    }
}
