namespace FlatTree;

/// <summary>
/// Leaf that invokes an action delegate and returns its status.
/// </summary>
/// <remarks>
/// The delegate must capture nothing — use a <c>static</c> lambda or a method group — and read
/// all per-agent state from the context. This is a correctness rule, not just an allocation one:
/// one node instance is shared by every agent of an archetype, so a delegate that closes over a
/// single agent's state would make every agent read that one agent's state. (Capturing is
/// harmless only when the tree serves exactly one agent, e.g. in a unit test.)
/// </remarks>
public sealed class Do<TContext> : LeafNode<TContext>
    where TContext : IClock
{
    private readonly LeafAction<TContext> _action;

    internal Do(string name, LeafAction<TContext> action)
        : base(name)
    {
        ArgumentNullException.ThrowIfNull(action);
        _action = action;
    }

    protected override TickResult Update(Span<NodeState> s, in TContext ctx)
    {
        return _action(in ctx);
    }
}

/// <summary>
/// A <see cref="Do{TContext}"/> whose action also receives per-site state authored on the node.
/// Every agent shares it, so treat it as read-only.
/// </summary>
public sealed class Do<TContext, TState> : LeafNode<TContext>
    where TContext : IClock
{
    private readonly TState _state;
    private readonly LeafAction<TContext, TState> _action;

    internal Do(string name, TState state, LeafAction<TContext, TState> action)
        : base(name)
    {
        ArgumentNullException.ThrowIfNull(action);
        _state = state;
        _action = action;
    }

    /// <summary>The state handed to the action.</summary>
    public TState State => _state;

    protected override TickResult Update(Span<NodeState> s, in TContext ctx)
    {
        return _action(in ctx, in _state);
    }
}
