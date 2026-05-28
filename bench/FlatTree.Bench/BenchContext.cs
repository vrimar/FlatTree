namespace FlatTree.Bench;

/// <summary>
/// A representative agent context: a <c>readonly struct</c> implementing <see cref="IClock"/>.
/// Constrained interface calls on a struct context (<c>ctx.NowMs</c>) are non-virtual with no
/// boxing, and passing it by <c>in</c> avoids copies — both essential to the 0 B/tick goal.
/// </summary>
public readonly struct BenchContext : IClock
{
    private readonly long _nowMs;

    public BenchContext(long nowMs)
    {
        _nowMs = nowMs;
    }

    public long NowMs => _nowMs;
}
