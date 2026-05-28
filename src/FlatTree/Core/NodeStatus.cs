namespace FlatTree;

/// <summary>
/// The lifecycle/result status of a node for a given agent.
/// <see cref="Fresh"/> (== zero) is a freshly reset / never-ticked node. A node's
/// <c>Update</c> must never return <see cref="Fresh"/>.
/// </summary>
public enum NodeStatus : byte
{
    Fresh = 0,
    Running = 1,
    Success = 2,
    Failure = 3,
}
