namespace FlatTree;

/// <summary>
/// Entry point for authoring a tree. <c>Bt.For&lt;Ctx&gt;()</c> returns a
/// <see cref="BtFactory{TContext}"/> instance whose nested factory methods build the
/// immutable, shared node graph.
/// </summary>
public static class Bt
{
    /// <summary>
    /// Creates a factory for context type <typeparamref name="TContext"/>. The optional
    /// <paramref name="randomProvider"/> is injected once into every Chance/RandomSelector/
    /// RandomSequence node and every jittered Wait; when omitted,
    /// <see cref="DefaultRandomProvider"/> is used. Pass a <see cref="SeededRandomProvider"/> for
    /// reproducible randomness, or the <see cref="RandomSource{TContext}"/> overload for a stream
    /// per agent.
    /// </summary>
    public static BtFactory<TContext> For<TContext>(IRandomProvider? randomProvider = null)
        where TContext : IClock
    {
        var provider = randomProvider ?? DefaultRandomProvider.Instance;
        return new BtFactory<TContext>((in TContext _) => provider);
    }

    /// <summary>
    /// Creates a factory that draws randomness from each agent's own context rather than from one
    /// tree-wide provider — the only way a fleet sharing a tree gets per-agent streams that replay.
    /// <paramref name="randomSource"/> must capture nothing.
    /// </summary>
    public static BtFactory<TContext> For<TContext>(RandomSource<TContext> randomSource)
        where TContext : IClock
    {
        BtGuard.RequireNoCapture(randomSource, nameof(randomSource));
        return new BtFactory<TContext>(randomSource);
    }
}
