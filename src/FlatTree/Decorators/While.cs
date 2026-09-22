namespace FlatTree;

/// <summary>
/// Runs the body while a condition holds, checked only between iterations. Success once it is
/// false; Failure if the body fails, or if the condition still holds after
/// <see cref="MaxIterationsPerTick"/> iterations within one tick. <c>Cursor</c> counts completed
/// iterations.
/// </summary>
/// <remarks>
/// The budget bounds one tick, not the loop: instant iterations would otherwise spin forever, while
/// iterations that span ticks run for as long as the condition holds.
/// </remarks>
public sealed class While<TContext> : LoopDecorator<TContext>
    where TContext : IClock
{
    private readonly LeafPredicate<TContext> _condition;

    internal While(
        string name,
        LeafPredicate<TContext> condition,
        BtNode<TContext> body,
        int maxIterationsPerTick
    )
        : base(name, body, maxIterationsPerTick)
    {
        ArgumentNullException.ThrowIfNull(condition);
        _condition = condition;
    }

    /// <summary>The most iterations, a resumed one included, that one tick may run.</summary>
    public int MaxIterationsPerTick => _maxIterationsPerTick;

    private protected override bool HasNext(
        ReadOnlySpan<NodeState> s,
        in TContext ctx,
        int snapshot
    ) => _condition(in ctx);
}
