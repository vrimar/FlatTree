namespace FlatTree;

/// <summary>
/// The result of ticking a node. Unlike <see cref="NodeStatus"/>, this has no
/// <c>Fresh</c> member: a ticked node always reports <see cref="Running"/>,
/// <see cref="Success"/>, or <see cref="Failure"/>. The values are deliberately aligned with
/// <see cref="NodeStatus"/> so a stored status is a direct, free cast.
/// </summary>
public enum TickResult : byte
{
    Running = 1,
    Success = 2,
    Failure = 3,
}
