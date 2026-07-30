namespace FlatTree;

/// <summary>
/// Mirror of <see cref="RandomSelector{TContext}"/> over sequence semantics: children are
/// visited in a per-agent shuffled order and the sequence returns the first
/// non-<see cref="TickResult.Success"/> status, or Success if all succeed.
/// </summary>
public sealed class RandomSequence<TContext> : CompositeNode<TContext>
    where TContext : IClock
{
    private readonly IRandomProvider _randomProvider;

    internal RandomSequence(
        string name,
        BtNode<TContext>[] children,
        IRandomProvider randomProvider
    )
        : base(name, children)
    {
        _randomProvider = randomProvider;
    }

    protected override TickResult Update(Span<NodeState> s, in TContext ctx) =>
        TickShuffled(s, in ctx, TickResult.Success, _randomProvider);

    protected override void DoReset(Span<NodeState> s, in TContext ctx)
    {
        ref var st = ref s[Id];
        st.Cursor = 0;
        st.Stamp = DeterministicRng.DrawNonZeroSeed(_randomProvider);
        base.DoReset(s, in ctx);
    }
}
