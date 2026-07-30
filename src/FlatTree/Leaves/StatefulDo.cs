namespace FlatTree;

/// <summary>
/// Leaf that invokes a <see cref="StatefulAction{TContext}"/>, giving the action <c>ref</c>
/// access to this node's per-agent <c>Cursor</c>/<c>Stamp</c> scratch so it can track progress
/// across multiple ticks (returning <see cref="TickResult.Running"/> until done). The simpler
/// <see cref="Do{TContext}"/> is preferred for instantaneous actions that need no scratch.
/// A reset clears the scratch, so an aborted action restarts rather than resuming mid-flight.
/// </summary>
/// <remarks>
/// The delegate must capture nothing — use a <c>static</c> lambda or a method group — and read
/// all per-agent state from the context or the supplied scratch refs. One node instance is
/// shared by every agent, so a delegate closing over a single agent's state would leak it.
/// </remarks>
public sealed class StatefulDo<TContext> : LeafNode<TContext>
    where TContext : IClock
{
    private readonly StatefulAction<TContext> _action;

    internal StatefulDo(string name, StatefulAction<TContext> action)
        : base(name)
    {
        ArgumentNullException.ThrowIfNull(action);
        _action = action;
    }

    protected override TickResult Update(Span<NodeState> s, in TContext ctx)
    {
        ref var st = ref s[Id];
        return _action(in ctx, ref st.Cursor, ref st.Stamp);
    }

    protected override void DoReset(Span<NodeState> s, in TContext ctx)
    {
        ref var st = ref s[Id];
        st.Cursor = 0;
        st.Stamp = 0;
    }
}
