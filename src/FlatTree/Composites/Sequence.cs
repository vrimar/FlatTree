namespace FlatTree;

/// <summary>
/// Mirror of <see cref="Selector{TContext}"/>: ticks children left-to-right, resuming from
/// the last running child (<c>Cursor</c>); returns the first
/// non-<see cref="NodeStatus.Success"/> status, or <see cref="NodeStatus.Success"/> if all
/// children succeed.
/// </summary>
public sealed class Sequence<TContext> : CompositeNode<TContext>
    where TContext : IClock
{
    internal Sequence(string name, BtNode<TContext>[] children)
        : base(name, children) { }

    protected override TickResult Update(Span<NodeState> s, in TContext ctx) =>
        TickSequential(s, in ctx, TickResult.Success);

    protected override void DoReset(Span<NodeState> s, in TContext ctx)
    {
        s[Id].Cursor = 0;
        base.DoReset(s, in ctx);
    }
}
