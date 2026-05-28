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
    /// RandomSequence node; when omitted, <see cref="DefaultRandomProvider"/> is used. Pass a
    /// <see cref="SeededRandomProvider"/> for reproducible randomness.
    /// </summary>
    public static BtFactory<TContext> For<TContext>(IRandomProvider? randomProvider = null)
        where TContext : IClock
    {
        return new BtFactory<TContext>(randomProvider ?? DefaultRandomProvider.Instance);
    }
}
