namespace FlatTree;

internal static class DurationGuard
{
    private static readonly TimeSpan Minimum = TimeSpan.FromMilliseconds(1);

    // Sub-millisecond durations truncate to 0, which reads as "already elapsed" and silently
    // defeats the node they configure.
    internal static long ToMilliseconds(TimeSpan value, string paramName)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, Minimum, paramName);
        return (long)value.TotalMilliseconds;
    }
}
