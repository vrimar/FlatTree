namespace FlatTree;

/// <summary>Inverts the child result: Failure → Success, Success → Failure, Running → Running.</summary>
public sealed class Inverter<TContext> : DecoratorNode<TContext>
    where TContext : IClock
{
    internal Inverter(string name, BtNode<TContext> child)
        : base(name, child) { }

    protected override TickResult Update(Span<NodeState> s, in TContext ctx)
    {
        var childStatus = Child.Tick(s, in ctx);

        if (childStatus == TickResult.Failure)
        {
            return TickResult.Success;
        }

        return childStatus == TickResult.Success ? TickResult.Failure : childStatus;
    }
}
