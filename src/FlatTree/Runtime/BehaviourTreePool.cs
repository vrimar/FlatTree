using System.Runtime.CompilerServices;

namespace FlatTree;

/// <summary>
/// Owns per-agent state for up to <see cref="Capacity"/> agents of one archetype in a single
/// contiguous <c>NodeState[capacity * NodeCount]</c> buffer (cache-friendly; no array-of-arrays).
/// Slots are rented/returned in O(1) via a free-list and ticked through the tree's
/// <see cref="BehaviourTree{TContext}.Tick(Span{NodeState}, in TContext)"/> overload, so there
/// is no per-tick allocation. Single-threaded, like the rest of the library.
/// </summary>
public sealed class BehaviourTreePool<TContext>
    where TContext : IClock
{
    private readonly BehaviourTree<TContext> _tree;
    private readonly NodeState[] _buffer;
    private readonly int _nodeCount;
    private readonly Stack<int> _free;
    private readonly bool[] _rented;
    private int _highWater;

    /// <summary>The maximum number of agents the pool can hold simultaneously.</summary>
    public int Capacity { get; }

    /// <summary>The number of agents currently rented.</summary>
    public int Count { get; private set; }

    /// <summary>
    /// Pre-allocates state for <paramref name="capacity"/> agents of <paramref name="tree"/>.
    /// Size to the archetype's peak agent count — the pool does not grow.
    /// </summary>
    public BehaviourTreePool(BehaviourTree<TContext> tree, int capacity)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

        _tree = tree;
        _nodeCount = tree.NodeCount;
        Capacity = capacity;
        _buffer = new NodeState[capacity * _nodeCount];
        _free = new Stack<int>(capacity);
        _rented = new bool[capacity];
    }

    /// <summary>
    /// Reserves a state slot and returns its index. A recycled slot is cleared to Fresh first.
    /// Throws when the pool is already at <see cref="Capacity"/>.
    /// </summary>
    public int Rent()
    {
        int slot;
        var recycled = false;

        if (_free.Count > 0)
        {
            slot = _free.Pop();
            recycled = true;
        }
        else if (_highWater < Capacity)
        {
            slot = _highWater++;
        }
        else
        {
            throw new InvalidOperationException(
                $"Pool is at capacity ({Capacity}); Return a slot before renting another."
            );
        }

        _rented[slot] = true;

        if (recycled)
        {
            Slice(slot).Clear();
        }

        Count++;
        return slot;
    }

    /// <summary>
    /// Releases a rented slot back to the pool for reuse. The slot's state is zeroed on the next
    /// <see cref="Rent"/>, which does NOT run node cleanup — call
    /// <see cref="Return(int, in TContext)"/> instead when a node in the slot may still hold a
    /// resource.
    /// </summary>
    public void Return(int slot)
    {
        RequireRented(slot);

        _rented[slot] = false;
        _free.Push(slot);
        Count--;
    }

    /// <summary>
    /// Resets the agent in <paramref name="slot"/> — giving every non-fresh node a chance to release
    /// what it acquired — then releases the slot.
    /// </summary>
    public void Return(int slot, in TContext ctx)
    {
        _tree.Reset(Slice(slot), in ctx);
        Return(slot);
    }

    /// <summary>Ticks the agent in <paramref name="slot"/>. Zero allocation.</summary>
    public TickResult Tick(int slot, in TContext ctx) => _tree.Tick(Slice(slot), in ctx);

    /// <summary>Resets the agent in <paramref name="slot"/> back to fresh.</summary>
    public void Reset(int slot, in TContext ctx) => _tree.Reset(Slice(slot), in ctx);

    /// <summary>The status of <paramref name="node"/> for the agent in <paramref name="slot"/>.</summary>
    public NodeStatus StatusOf(int slot, BtNode<TContext> node) =>
        _tree.StatusOf((ReadOnlySpan<NodeState>)Slice(slot), node);

    private void RequireRented(int slot)
    {
        // Bound against _rented.Length (== Capacity) so the JIT can drop the element bounds check.
        var rented = _rented;

        if ((uint)slot >= (uint)rented.Length || !rented[slot])
        {
            ThrowInvalidSlot(slot);
        }
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowInvalidSlot(int slot)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(slot);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(slot, Capacity);

        throw new InvalidOperationException(
            $"Slot {slot} is not currently rented (double Return, or use after Return). "
                + "Serving it again would give two agents the same state."
        );
    }

    private Span<NodeState> Slice(int slot)
    {
        RequireRented(slot);
        return _buffer.AsSpan(slot * _nodeCount, _nodeCount);
    }
}
