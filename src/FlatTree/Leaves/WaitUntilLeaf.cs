namespace FlatTree;

/// <summary>
/// Base for the <see cref="WaitUntil{TContext}"/> leaves: succeeds once a predicate holds, checked
/// from the activating tick. A lapsed deadline reports what the timeout handler returns, Failure
/// without one.
/// </summary>
public abstract class WaitUntilLeaf<TContext> : PollingLeaf<TContext>
    where TContext : IClock
{
    private protected WaitUntilLeaf(
        string name,
        TimeSpan timeout,
        bool handlesTimeout,
        ClockSelector<TContext>? clock
    )
        : base(name, timeout, clock)
    {
        if (handlesTimeout && timeout == TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeout),
                timeout,
                "A zero timeout waits forever, so the timeout handler would never run."
            );
        }
    }

    private protected abstract bool Holds(in TContext ctx);

    private protected abstract TickResult? Expire(in TContext ctx);

    protected sealed override TickResult Begin(Span<NodeState> s, in TContext ctx) =>
        TickResult.Success;

    protected sealed override TickResult Poll(Span<NodeState> s, in TContext ctx) =>
        Holds(in ctx) ? TickResult.Success : TickResult.Running;

    protected sealed override TickResult OnTimeout(Span<NodeState> s, in TContext ctx) =>
        Expire(in ctx) is { } verdict
            ? VerdictGuard.RequireComplete(verdict, "timeout", Name)
            : TickResult.Failure;
}
