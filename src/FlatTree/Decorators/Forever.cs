namespace FlatTree;

/// <summary>
/// Loops the child forever: on child completion it is reset and Running is returned, so it
/// restarts next tick. Without an <c>exitWhen</c> predicate it never terminates and ends only when
/// an ancestor resets it. One child iteration per tick.
/// </summary>
/// <remarks>
/// With <c>exitWhen</c> it ends with Success at the first completion the predicate holds at — a
/// boundary the child chose, not a teardown mid-iteration. The child's status is discarded either way.
/// </remarks>
public sealed class Forever<TContext> : DecoratorNode<TContext>
    where TContext : IClock
{
    private readonly LeafPredicate<TContext>? _exitWhen;

    internal Forever(string name, BtNode<TContext> child, LeafPredicate<TContext>? exitWhen = null)
        : base(name, child)
    {
        _exitWhen = exitWhen;
    }

    protected override TickResult Update(Span<NodeState> s, in TContext ctx)
    {
        if (Child.Tick(s, in ctx) == TickResult.Running)
        {
            return TickResult.Running;
        }

        Child.Reset(s, in ctx);

        return _exitWhen is not null && _exitWhen(in ctx) ? TickResult.Success : TickResult.Running;
    }
}
