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
    private readonly Func<TContext, TickResult> _action;

    internal Do(string name, Func<TContext, TickResult> action)
        : base(name)
    {
        ArgumentNullException.ThrowIfNull(action);
        _action = action;
    }

    protected override TickResult Update(Span<NodeState> s, in TContext ctx)
    {
        return _action(ctx);
    }
}
