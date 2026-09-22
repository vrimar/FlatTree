namespace FlatTree;

/// <summary>
/// Base for <see cref="Catch{TContext}"/> and <see cref="OnComplete{TContext}"/>: hands the child's
/// outcome to a handler that says what the decorator reports in its place. Running, and an outcome
/// the node does not handle, pass through untouched. The handler must return Success or Failure.
/// </summary>
public abstract class OutcomeDecorator<TContext> : DecoratorNode<TContext>
    where TContext : IClock
{
    private readonly bool _failureOnly;

    private protected OutcomeDecorator(string name, BtNode<TContext> child, bool failureOnly)
        : base(name, child)
    {
        _failureOnly = failureOnly;
    }

    private protected abstract TickResult Handle(in TContext ctx, TickResult outcome);

    protected sealed override TickResult Update(Span<NodeState> s, in TContext ctx)
    {
        var childStatus = Child.Tick(s, in ctx);

        var handled = _failureOnly
            ? childStatus == TickResult.Failure
            : childStatus != TickResult.Running;

        if (!handled)
        {
            return childStatus;
        }

        return VerdictGuard.RequireComplete(
            Handle(in ctx, childStatus),
            _failureOnly ? "failure" : "completion",
            Name
        );
    }
}
