namespace FlatTree.Tests.Doubles;

/// <summary>
/// Manual logical clock for tests. Used directly as the tree's context type. Mutable:
/// <see cref="Advance"/> moves logical time forward, and nodes observe the updated value on the
/// next tick.
/// </summary>
public sealed class FakeClock : IClock
{
    public long NowMs { get; private set; }

    public void Advance(long ms) => NowMs += ms;
}
