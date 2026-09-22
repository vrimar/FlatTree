namespace FlatTree;

/// <summary>
/// Leaf that evaluates a predicate: <c>true</c> → Success, <c>false</c> → Failure.
/// </summary>
/// <remarks>
/// The predicate must capture nothing — use a <c>static</c> lambda or a method group — and read
/// all per-agent state from the context. This is a correctness rule, not just an allocation one:
/// one node instance is shared by every agent of an archetype, so a predicate that closes over a
/// single agent's state would make every agent read that one agent's state. (Capturing is
/// harmless only when the tree serves exactly one agent, e.g. in a unit test.)
/// </remarks>
public sealed class Condition<TContext> : LeafNode<TContext>
    where TContext : IClock
{
    private readonly LeafPredicate<TContext> _predicate;

    internal Condition(string name, LeafPredicate<TContext> predicate)
        : base(name)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        _predicate = predicate;
    }

    protected override TickResult Update(Span<NodeState> s, in TContext ctx)
    {
        return _predicate(in ctx) ? TickResult.Success : TickResult.Failure;
    }
}

/// <summary>
/// A <see cref="Condition{TContext}"/> whose predicate also receives per-site state authored on the
/// node. Every agent shares it, so treat it as read-only.
/// </summary>
public sealed class Condition<TContext, TState> : LeafNode<TContext>
    where TContext : IClock
{
    private readonly TState _state;
    private readonly LeafPredicate<TContext, TState> _predicate;

    internal Condition(string name, TState state, LeafPredicate<TContext, TState> predicate)
        : base(name)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        _state = state;
        _predicate = predicate;
    }

    /// <summary>The state handed to the predicate.</summary>
    public TState State => _state;

    protected override TickResult Update(Span<NodeState> s, in TContext ctx)
    {
        return _predicate(in ctx, in _state) ? TickResult.Success : TickResult.Failure;
    }
}
