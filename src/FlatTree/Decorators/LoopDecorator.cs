namespace FlatTree;

/// <summary>
/// Base for <see cref="ForEach{TContext}"/> and <see cref="While{TContext}"/>: finishes an
/// iteration that is Running, then starts another while the loop has a next one. A body Failure
/// fails the loop; each completed iteration advances <c>Cursor</c> and resets the body, and
/// iterations that complete instantly run back-to-back within one tick. <c>Cursor</c> clears on
/// completion or reset.
/// </summary>
public abstract class LoopDecorator<TContext> : DecoratorNode<TContext>
    where TContext : IClock
{
    private protected readonly int _maxIterationsPerTick;

    private protected LoopDecorator(string name, BtNode<TContext> body, int maxIterationsPerTick)
        : base(name, body)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(
            maxIterationsPerTick,
            1,
            nameof(maxIterationsPerTick)
        );
        _maxIterationsPerTick = maxIterationsPerTick;
    }

    private protected virtual int Snapshot(in TContext ctx) => 0;

    private protected abstract bool HasNext(
        ReadOnlySpan<NodeState> s,
        in TContext ctx,
        int snapshot
    );

    private protected virtual void BeforeIteration(ReadOnlySpan<NodeState> s, in TContext ctx) { }

    protected sealed override TickResult Update(Span<NodeState> s, in TContext ctx)
    {
        var snapshot = Snapshot(in ctx);

        for (
            var ran = 0;
            s[Child.Id].Status == NodeStatus.Running || HasNext(s, in ctx, snapshot);
            ran++
        )
        {
            if (ran == _maxIterationsPerTick)
            {
                return TickResult.Failure;
            }

            BeforeIteration(s, in ctx);

            var childStatus = Child.Tick(s, in ctx);
            if (childStatus != TickResult.Success)
            {
                return childStatus;
            }

            s[Id].Cursor++;
            Child.Reset(s, in ctx);
        }

        return TickResult.Success;
    }

    protected sealed override void OnTerminate(
        Span<NodeState> s,
        TickResult status,
        in TContext ctx
    ) => s[Id].Cursor = 0;

    protected sealed override void DoReset(Span<NodeState> s, in TContext ctx)
    {
        s[Id].Cursor = 0;
        base.DoReset(s, in ctx);
    }
}
