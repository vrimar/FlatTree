namespace FlatTree;

/// <summary>
/// Allocation-free query helpers for <see cref="TickResult"/>, so call sites read intent
/// (<c>result.IsSuccess()</c>) instead of comparing against enum members.
/// </summary>
public static class TickResultExtensions
{
    /// <summary>The node is still executing (<see cref="TickResult.Running"/>).</summary>
    public static bool IsRunning(this TickResult result) => result == TickResult.Running;

    /// <summary>The node completed successfully (<see cref="TickResult.Success"/>).</summary>
    public static bool IsSuccess(this TickResult result) => result == TickResult.Success;

    /// <summary>The node completed with failure (<see cref="TickResult.Failure"/>).</summary>
    public static bool IsFailure(this TickResult result) => result == TickResult.Failure;

    /// <summary>The node finished this tick (Success or Failure — anything but Running).</summary>
    public static bool IsComplete(this TickResult result) => result != TickResult.Running;
}

/// <summary>
/// Allocation-free query helpers for the persistent <see cref="NodeStatus"/> stored per agent.
/// </summary>
public static class NodeStatusExtensions
{
    /// <summary>The node has never ticked (or was reset) this session.</summary>
    public static bool IsFresh(this NodeStatus status) => status == NodeStatus.Fresh;

    /// <summary>The node is currently executing.</summary>
    public static bool IsRunning(this NodeStatus status) => status == NodeStatus.Running;

    /// <summary>The node last completed successfully.</summary>
    public static bool IsSuccess(this NodeStatus status) => status == NodeStatus.Success;

    /// <summary>The node last completed with failure.</summary>
    public static bool IsFailure(this NodeStatus status) => status == NodeStatus.Failure;

    /// <summary>The node has a terminal status (Success or Failure).</summary>
    public static bool IsComplete(this NodeStatus status) =>
        status is NodeStatus.Success or NodeStatus.Failure;
}
