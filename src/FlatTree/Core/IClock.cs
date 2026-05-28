namespace FlatTree;

/// <summary>
/// Source of logical time for time-based nodes (Wait, Cooldown, RateLimiter, TimeLimit).
/// Implemented by the agent context. Must be monotonic and <c>&gt;= 0</c>. No wall-clock:
/// tests drive a fake clock; a server drives logical time. This is what makes a tree
/// deterministically testable.
/// </summary>
public interface IClock
{
    /// <summary>Current logical time in milliseconds. Monotonic, <c>&gt;= 0</c>.</summary>
    long NowMs { get; }
}
