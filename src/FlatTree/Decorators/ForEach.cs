namespace FlatTree;

/// <summary>
/// Runs the body once per item, publishing the index through the optional
/// <see cref="IterationHook{TContext}"/> before each tick of it. Success once every item is done;
/// a body Failure fails the loop, and Running yields with the cursor parked on the current item so
/// a multi-tick body resumes where it left off. <c>Cursor</c> is the item index.
/// </summary>
/// <remarks>
/// The count is re-read every tick, so a collection that shrinks mid-loop ends early — an
/// iteration already in flight is ticked to completion first, and no new one starts after it.
/// Iterations that complete instantly run back-to-back within one tick.
/// </remarks>
public sealed class ForEach<TContext> : LoopDecorator<TContext>
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
        : base(name, child, int.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(count);
        _count = count;
        _onIteration = onIteration;
    }

    private protected override int Snapshot(in TContext ctx) => _count(in ctx);

    private protected override bool HasNext(
        ReadOnlySpan<NodeState> s,
        in TContext ctx,
        int snapshot
    ) => s[Id].Cursor < snapshot;

    private protected override void BeforeIteration(ReadOnlySpan<NodeState> s, in TContext ctx) =>
        _onIteration?.Invoke(in ctx, s[Id].Cursor);
}
