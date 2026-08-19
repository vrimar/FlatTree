namespace FlatTree;

/// <summary>
/// Per-agent, per-node state of an arbitrary type, held as one contiguous
/// <c>T[capacity * nodeCount]</c> and sliced by agent slot — the sidecar to
/// <see cref="BehaviourTreePool{TContext}"/> for whatever a custom node needs beyond the
/// <c>Cursor</c>/<c>Stamp</c> scratch in <see cref="NodeState"/>.
/// </summary>
/// <remarks>
/// The pool stays the authority on slot allocation: rent from it, index this with the slot it
/// returned, and <see cref="Clear"/> before reuse when the slab holds references.
/// </remarks>
public sealed class NodeSlab<T>
{
    private readonly T[] _buffer;
    private readonly int _nodeCount;

    /// <summary>The number of agents the slab holds.</summary>
    public int Capacity { get; }

    public NodeSlab(int nodeCount, int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(nodeCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

        _nodeCount = nodeCount;
        Capacity = capacity;
        _buffer = new T[capacity * nodeCount];
    }

    /// <summary>One agent's slice, indexed by <see cref="BtNode{TContext}.Id"/>.</summary>
    public Span<T> Slice(int slot)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(slot);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(slot, Capacity);

        return _buffer.AsSpan(slot * _nodeCount, _nodeCount);
    }

    /// <summary>Zeroes one agent's slice.</summary>
    public void Clear(int slot) => Slice(slot).Clear();
}
