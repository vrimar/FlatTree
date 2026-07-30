namespace FlatTree;

/// <summary>
/// Loops the child forever: on child completion it is reset and Running is returned, so it
/// restarts next tick. Never terminates; ends only when an ancestor resets it. One child
/// iteration per tick.
/// </summary>
public sealed class Forever<TContext> : DecoratorNode<TContext>
    where TContext : IClock
{
    internal Forever(string name, BtNode<TContext> child)
        : base(name, child) { }

    protected override TickResult Update(Span<NodeState> s, in TContext ctx)
    {
        if (Child.Tick(s, in ctx) != TickResult.Running)
        {
            Child.Reset(s, in ctx);
        }

        return TickResult.Running;
    }
}
