namespace FlatTree;

/// <summary>Maps any terminal child status to Success; passes Running through.</summary>
public sealed class AlwaysSucceed<TContext> : DecoratorNode<TContext>
    where TContext : IClock
{
    internal AlwaysSucceed(string name, BtNode<TContext> child)
        : base(name, child) { }

    protected override TickResult Update(Span<NodeState> s, in TContext ctx)
    {
        var childStatus = Child.Tick(s, in ctx);

        return childStatus == TickResult.Running ? TickResult.Running : TickResult.Success;
    }
}
