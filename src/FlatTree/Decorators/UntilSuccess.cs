namespace FlatTree;

/// <summary>
/// Repeats the child until it succeeds. Child Success → Success; child Failure → reset the
/// child and return Running (so it re-runs from fresh); child Running → Running (the child's
/// progress is preserved, NOT reset).
/// </summary>
public sealed class UntilSuccess<TContext> : DecoratorNode<TContext>
    where TContext : IClock
{
    internal UntilSuccess(string name, BtNode<TContext> child)
        : base(name, child) { }

    protected override TickResult Update(Span<NodeState> s, in TContext ctx)
    {
        var childStatus = Child.Tick(s, in ctx);

        if (childStatus == TickResult.Success)
        {
            return TickResult.Success;
        }

        if (childStatus == TickResult.Failure)
        {
            Child.Reset(s);
        }

        return TickResult.Running;
    }
}
