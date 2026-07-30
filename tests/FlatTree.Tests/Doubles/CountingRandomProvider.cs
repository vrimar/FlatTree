namespace FlatTree.Tests.Doubles;

/// <summary>
/// An <see cref="IRandomProvider"/> that counts how often it is drawn from, for asserting that a
/// node consumes the provider stream exactly as often as it should.
/// </summary>
public sealed class CountingRandomProvider : IRandomProvider
{
    private int _next;

    public int NextCallCount { get; private set; }

    public int NextDoubleCallCount { get; private set; }

    public double NextDouble()
    {
        NextDoubleCallCount++;
        return 0.5;
    }

    public int Next(int maxExclusive)
    {
        NextCallCount++;
        return ++_next % maxExclusive;
    }
}
