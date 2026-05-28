namespace FlatTree;

/// <summary>
/// Randomness source injected once into a tree at build time and handed to every
/// <c>Random</c>, <c>RandomSelector</c>, and <c>RandomSequence</c> node. The shared tree
/// carries no per-agent RNG; per-agent shuffle seeds live in <see cref="NodeState.Stamp"/>.
/// </summary>
public interface IRandomProvider
{
    /// <summary>A random double in <c>[0, 1)</c>.</summary>
    double NextDouble();

    /// <summary>A random non-negative integer in <c>[0, maxExclusive)</c>.</summary>
    int Next(int maxExclusive);
}
