namespace FlatTree;

/// <summary>
/// A deterministic, seedable <see cref="IRandomProvider"/> backed by SplitMix64. Inject it via
/// <c>Bt.For&lt;Ctx&gt;(new SeededRandomProvider(seed))</c> to make a tree's randomness
/// (RandomSelector/RandomSequence orderings and Chance rolls) fully reproducible for replay and
/// testing. Allocation-free per call. Not thread-safe (a tree is single-threaded by design).
/// </summary>
/// <remarks>
/// One provider is shared by every tree and agent built from the same factory, and it is one
/// advancing stream: what any agent draws depends on how many draws came before it globally.
/// Reproducing a run therefore means replaying every draw in the same order — a single agent
/// replayed alone will diverge. For per-tree or per-agent replay, give each its own factory and
/// provider.
/// </remarks>
public sealed class SeededRandomProvider : IRandomProvider
{
    private DeterministicRng _rng;

    public SeededRandomProvider(long seed)
    {
        _rng = new DeterministicRng(seed);
    }

    public double NextDouble() => _rng.NextDouble();

    public int Next(int maxExclusive)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxExclusive);
        return _rng.Next(maxExclusive);
    }
}
