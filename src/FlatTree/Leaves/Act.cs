namespace FlatTree;

/// <summary>Leaf that runs an effect and returns Success.</summary>
/// <remarks>
/// Not a <c>Do</c> overload: a <c>bool</c>-returning lambda would bind to it and always succeed.
/// </remarks>
public sealed class Act<TContext> : LeafNode<TContext>
    where TContext : IClock
{
    private readonly LeafEffect<TContext> _effect;

    internal Act(string name, LeafEffect<TContext> effect)
        : base(name)
    {
        ArgumentNullException.ThrowIfNull(effect);
        _effect = effect;
    }

    protected override TickResult Update(Span<NodeState> s, in TContext ctx)
    {
        _effect(in ctx);
        return TickResult.Success;
    }
}
