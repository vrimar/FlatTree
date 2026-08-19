namespace FlatTree;

/// <summary>
/// A selector whose children are visited in a per-agent shuffled order: returns the first
/// non-<see cref="TickResult.Failure"/> status, or Failure if all fail. <c>Stamp</c> holds the
/// shuffle seed and <c>Cursor</c> is the visitation index into the reproduced permutation. Each
/// <c>Update</c> regenerates the SAME permutation from the seed into a <c>stackalloc</c> span
/// (no heap), so a running child resumes at the same position. <c>DoReset</c> clears the seed, so
/// the next activation draws a new one and visits in a new order. Lazy shuffle: <c>Stamp == 0</c>
/// means "unseeded" — a non-zero seed is drawn and stored on the next tick.
/// </summary>
public sealed class RandomSelector<TContext> : CompositeNode<TContext>
    where TContext : IClock
{
    private readonly RandomSource<TContext> _randomSource;

    internal RandomSelector(
        string name,
        BtNode<TContext>[] children,
        RandomSource<TContext> randomSource
    )
        : base(name, children)
    {
        RequireShuffleableChildCount(nameof(children));
        _randomSource = randomSource;
    }

    protected override TickResult Update(Span<NodeState> s, in TContext ctx) =>
        TickShuffled(s, in ctx, TickResult.Failure, _randomSource(in ctx));

    // Clearing rather than re-drawing keeps this idempotent: a composite keeps its terminal status,
    // so an ancestor's reset cascade runs DoReset a second time.
    protected override void DoReset(Span<NodeState> s, in TContext ctx)
    {
        ref var st = ref s[Id];
        st.Cursor = 0;
        st.Stamp = 0;
        base.DoReset(s, in ctx);
    }
}
