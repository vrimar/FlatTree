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
    private readonly Func<TContext, bool> _predicate;

    internal Condition(string name, Func<TContext, bool> predicate)
        : base(name)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        _predicate = predicate;
    }

    protected override TickResult Update(Span<NodeState> s, in TContext ctx)
    {
        return _predicate(ctx) ? TickResult.Success : TickResult.Failure;
    }
}
