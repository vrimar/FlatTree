namespace FlatTree;

/// <summary>
/// Passes the child status through unchanged, but explicitly resets the child whenever it
/// completes (its entire purpose) so the child re-runs from fresh next time.
/// </summary>
public sealed class AutoReset<TContext> : DecoratorNode<TContext>
    where TContext : IClock
{
    internal AutoReset(string name, BtNode<TContext> child)
        : base(name, child) { }

    protected override TickResult Update(Span<NodeState> s, in TContext ctx)
    {
        return Child.Tick(s, in ctx);
    }

    protected override void OnTerminate(Span<NodeState> s, TickResult status, in TContext ctx)
    {
        Child.Reset(s, in ctx);
    }
}
