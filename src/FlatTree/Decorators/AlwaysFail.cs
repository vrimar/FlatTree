namespace FlatTree;

/// <summary>Maps any terminal child status to Failure; passes Running through.</summary>
public sealed class AlwaysFail<TContext> : DecoratorNode<TContext>
    where TContext : IClock
{
    internal AlwaysFail(string name, BtNode<TContext> child)
        : base(name, child) { }

    protected override TickResult Update(Span<NodeState> s, in TContext ctx)
    {
        var childStatus = Child.Tick(s, in ctx);

        return childStatus == TickResult.Running ? TickResult.Running : TickResult.Failure;
    }
}
