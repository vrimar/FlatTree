namespace FlatTree;

/// <summary>
/// A selector whose children are visited in a per-agent shuffled order: returns the first
/// non-<see cref="TickResult.Failure"/> status, or Failure if all fail. <c>Stamp</c> holds the
/// shuffle seed and <c>Cursor</c> is the visitation index into the reproduced permutation. Each
/// <c>Update</c> regenerates the SAME permutation from the seed into a <c>stackalloc</c> span
/// (no heap), so a running child resumes at the same position. <c>DoReset</c> draws a fresh seed
/// (new order next session). Lazy first shuffle: <c>Stamp == 0</c> means "unseeded" — a non-zero
/// seed is drawn and stored on first tick.
/// </summary>
public sealed class RandomSelector<TContext> : CompositeNode<TContext>
    where TContext : IClock
{
    private readonly IRandomProvider _randomProvider;

    internal RandomSelector(
        string name,
        BtNode<TContext>[] children,
        IRandomProvider randomProvider
    )
        : base(name, children)
    {
        _randomProvider = randomProvider;
    }

    protected override TickResult Update(Span<NodeState> s, in TContext ctx) =>
        TickShuffled(s, in ctx, TickResult.Failure, _randomProvider);

    protected override void DoReset(Span<NodeState> s, in TContext ctx)
    {
        ref var st = ref s[Id];
        st.Cursor = 0;
        st.Stamp = DeterministicRng.DrawNonZeroSeed(_randomProvider);
        base.DoReset(s, in ctx);
    }
}
