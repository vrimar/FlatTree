namespace FlatTree;

/// <summary>
/// Default <see cref="IRandomProvider"/> backed by <see cref="System.Random.Shared"/>.
/// Allocation-free per call. Used when no provider is injected into <c>Bt.For</c>.
/// </summary>
public sealed class DefaultRandomProvider : IRandomProvider
{
    public static readonly DefaultRandomProvider Instance = new DefaultRandomProvider();

    public double NextDouble() => Random.Shared.NextDouble();

    public int Next(int maxExclusive)
    {
        // Random.Shared.Next(0) returns 0, which is outside the documented [0, maxExclusive).
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxExclusive);
        return Random.Shared.Next(maxExclusive);
    }
}
