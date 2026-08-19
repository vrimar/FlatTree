namespace FlatTree;

/// <summary>
/// Runs the body once per item, publishing the index through the optional
/// <see cref="IterationHook{TContext}"/> before each tick of it. Success once every item is done;
/// a body Failure fails the loop, and Running yields with the cursor parked on the current item so
/// a multi-tick body resumes where it left off. <c>Cursor</c> is the item index.
/// </summary>
/// <remarks>
/// The count is re-read every tick, so a collection that shrinks mid-loop ends early. Iterations
/// that complete instantly run back-to-back within one tick.
/// </remarks>
public sealed class ForEach<TContext> : DecoratorNode<TContext>
    where TContext : IClock
{
    private readonly CountOf<TContext> _count;
    private readonly IterationHook<TContext>? _onIteration;

    internal ForEach(
        string name,
        BtNode<TContext> child,
        CountOf<TContext> count,
        IterationHook<TContext>? onIteration
    )
        : base(name, child)
    {
        ArgumentNullException.ThrowIfNull(count);
        _count = count;
        _onIteration = onIteration;
    }

    protected override TickResult Update(Span<NodeState> s, in TContext ctx)
    {
        var total = _count(in ctx);

        while (s[Id].Cursor < total)
        {
            _onIteration?.Invoke(in ctx, s[Id].Cursor);

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

    protected override void OnTerminate(Span<NodeState> s, TickResult status, in TContext ctx) =>
        s[Id].Cursor = 0;

    protected override void DoReset(Span<NodeState> s, in TContext ctx)
    {
        s[Id].Cursor = 0;
        base.DoReset(s, in ctx);
    }
}
