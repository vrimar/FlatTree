namespace FlatTree;

/// <summary>
/// Ticks children left-to-right, resuming from the last running child (<c>Cursor</c>);
/// returns the first non-<see cref="NodeStatus.Failure"/> status, or
/// <see cref="NodeStatus.Failure"/> if all children fail. Earlier children are NOT
/// re-ticked while a later child is running.
/// </summary>
public sealed class Selector<TContext> : CompositeNode<TContext>
    where TContext : IClock
{
    internal Selector(string name, BtNode<TContext>[] children)
        : base(name, children) { }

    protected override TickResult Update(Span<NodeState> s, in TContext ctx) =>
        TickSequential(s, in ctx, TickResult.Failure);

    protected override void DoReset(Span<NodeState> s)
    {
        s[Id].Cursor = 0;
        base.DoReset(s);
    }
}
