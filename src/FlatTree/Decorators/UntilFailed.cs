namespace FlatTree;

/// <summary>
/// Mirror of <see cref="UntilSuccess{TContext}"/>: repeats the child until it fails. Child
/// Failure → Success; child Success → reset the child and return Running; child Running →
/// Running (progress preserved).
/// </summary>
public sealed class UntilFailed<TContext> : DecoratorNode<TContext>
    where TContext : IClock
{
    internal UntilFailed(string name, BtNode<TContext> child)
        : base(name, child) { }

    protected override TickResult Update(Span<NodeState> s, in TContext ctx)
    {
        var childStatus = Child.Tick(s, in ctx);

        if (childStatus == TickResult.Failure)
        {
            return TickResult.Success;
        }

        if (childStatus == TickResult.Success)
        {
            Child.Reset(s, in ctx);
        }

        return TickResult.Running;
    }
}
