namespace FlatTree;

/// <summary>
/// An immutable, shared behaviour tree built once per archetype. Holds the node graph and
/// the node count; carries no per-agent state. Create one per archetype at startup and
/// share it across every agent of that type.
/// </summary>
public sealed class BehaviourTree<TContext>
    where TContext : IClock
{
    private const string StateLengthMessage =
        "State array length must equal NodeCount; pass an array from this tree's NewState().";

    private readonly BtNode<TContext>[] _nodes;
    private readonly BtNode<TContext>[] _tagged;
    private readonly Dictionary<int, (int Start, int Length)>? _tagRanges;

    /// <summary>Total number of nodes; the length of a per-agent <c>NodeState[]</c>.</summary>
    public int NodeCount => _nodes.Length;

    /// <summary>The root node.</summary>
    public BtNode<TContext> Root { get; }

    /// <summary>
    /// Every node in the tree, indexed by <see cref="BtNode{TContext}.Id"/> (DFS pre-order).
    /// Read-only: this is the array the tree indexes by <c>Id</c>, so handing it out mutably would
    /// let a caller desynchronise a node from its own state slot. For debugging and visualization.
    /// </summary>
    public ReadOnlySpan<BtNode<TContext>> Nodes => _nodes;

    internal BehaviourTree(BtNode<TContext> root, BtNode<TContext>[] nodes)
    {
        Root = root;
        _nodes = nodes;
        (_tagged, _tagRanges) = GroupByTag(nodes);
    }

    /// <summary>Allocates a fresh per-agent state array (the only per-agent allocation).</summary>
    public NodeState[] NewState() => new NodeState[NodeCount];

    /// <summary>
    /// Allocates a per-agent array of <typeparamref name="T"/> parallel to <see cref="NewState"/>,
    /// indexed by the same <see cref="BtNode{TContext}.Id"/> — per-node state beyond the
    /// <c>Cursor</c>/<c>Stamp</c> scratch. Carry it on the context so nodes can reach it.
    /// </summary>
    public T[] NewSidecar<T>() => new T[NodeCount];

    /// <summary>
    /// Allocates a contiguous sidecar slab for <paramref name="capacity"/> agents, indexed by the
    /// slot a <see cref="BehaviourTreePool{TContext}"/> of the same capacity hands out.
    /// </summary>
    public NodeSlab<T> NewSlab<T>(int capacity) => new(NodeCount, capacity);

    /// <summary>
    /// Every node carrying <paramref name="tag"/>, in <see cref="BtNode{TContext}.Id"/> order.
    /// Empty when nothing carries it. Tag zero is "untagged" and always returns empty.
    /// </summary>
    public ReadOnlySpan<BtNode<TContext>> NodesWith(int tag) =>
        _tagRanges is not null && _tagRanges.TryGetValue(tag, out var range)
            ? _tagged.AsSpan(range.Start, range.Length)
            : ReadOnlySpan<BtNode<TContext>>.Empty;

    private static (BtNode<TContext>[] Tagged, Dictionary<int, (int, int)>? Ranges) GroupByTag(
        BtNode<TContext>[] nodes
    )
    {
        var tagged = nodes.Where(static node => node.Tag != 0).ToArray();

        if (tagged.Length == 0)
        {
            return ([], null);
        }

        Array.Sort(tagged, static (a, b) => a.Tag != b.Tag ? a.Tag.CompareTo(b.Tag) : a.Id - b.Id);

        var ranges = new Dictionary<int, (int, int)>();
        var start = 0;

        for (var i = 1; i <= tagged.Length; i++)
        {
            if (i < tagged.Length && tagged[i].Tag == tagged[start].Tag)
            {
                continue;
            }

            ranges[tagged[start].Tag] = (start, i - start);
            start = i;
        }

        return (tagged, ranges);
    }

    /// <summary>
    /// Ticks the tree against an agent's state. Zero allocation. The state may be a slice of a
    /// larger contiguous buffer (e.g. <see cref="BehaviourTreePool{TContext}"/>).
    /// </summary>
    public TickResult Tick(Span<NodeState> s, in TContext ctx)
    {
        RequireStateLength(s.Length);
        return Root.Tick(s, in ctx);
    }

    /// <summary>Ticks the tree against an agent's state. Zero allocation.</summary>
    public TickResult Tick(NodeState[] s, in TContext ctx) => Tick(s.AsSpan(), in ctx);

    /// <summary>
    /// Resets an agent's state back to fresh, giving every non-fresh node a chance to clean up
    /// against <paramref name="ctx"/>. Zero allocation.
    /// </summary>
    public void Reset(Span<NodeState> s, in TContext ctx)
    {
        RequireStateLength(s.Length);
        Root.Reset(s, in ctx);
    }

    /// <summary>Resets an agent's state back to fresh.</summary>
    public void Reset(NodeState[] s, in TContext ctx) => Reset(s.AsSpan(), in ctx);

    /// <summary>
    /// Resets an agent's state back to fresh, reaching nodes that
    /// <see cref="Reset(Span{NodeState}, in TContext)"/> cannot. Use this to recover an agent after
    /// an exception escaped <see cref="Tick(Span{NodeState}, in TContext)"/>: a node whose
    /// <c>Update</c> threw never had its status written, so it reports Fresh over a dirty subtree
    /// and the ordinary cascade short-circuits above it. Like <c>Reset</c>, this preserves state a
    /// node deliberately keeps across a reset (a <see cref="Cooldown{TContext}"/> timer).
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="Reset(Span{NodeState}, in TContext)"/> this calls <c>DoReset</c> on EVERY
    /// node, including ones that never ticked — a node that acquired and then threw leaves a slot
    /// that reads as untouched, so the slot cannot be used to decide. Cleanup must therefore be
    /// idempotent and safe when nothing was acquired: guard it on a flag the node itself sets, the
    /// way the state is guarded elsewhere. Within one call each node is visited once, children
    /// before parents. A cleanup that throws does not stop the sweep; the failures are collected
    /// and rethrown together. Allocation-free unless a cleanup throws.
    /// </remarks>
    public void ResetAll(Span<NodeState> s, in TContext ctx)
    {
        RequireStateLength(s.Length);

        List<Exception>? failures = null;

        // Ids are DFS pre-order, so descending visits every child before its parent: by the time a
        // parent's DoReset cascades, its children are already Fresh and short-circuit.
        for (var i = _nodes.Length - 1; i >= 0; i--)
        {
            try
            {
                _nodes[i].ResetForRecovery(s, in ctx);
            }
            catch (Exception ex)
            {
                // One node's cleanup failing must not strand every remaining node's.
                (failures ??= new List<Exception>()).Add(ex);
            }
        }

        if (failures is not null)
        {
            throw new AggregateException("One or more nodes failed to reset.", failures);
        }
    }

    /// <summary>
    /// Resets an agent's state back to fresh, reaching nodes that
    /// <see cref="Reset(NodeState[], in TContext)"/> cannot.
    /// </summary>
    public void ResetAll(NodeState[] s, in TContext ctx) => ResetAll(s.AsSpan(), in ctx);

    /// <summary>
    /// The status of <paramref name="node"/> for the agent represented by <paramref name="s"/> —
    /// a safe, allocation-free alternative to indexing the state array by <c>node.Id</c>.
    /// </summary>
    public NodeStatus StatusOf(ReadOnlySpan<NodeState> s, BtNode<TContext> node)
    {
        ArgumentNullException.ThrowIfNull(node);
        RequireStateLength(s.Length);

        if ((uint)node.Id >= (uint)NodeCount || _nodes[node.Id] != node)
        {
            ThrowNotInTree(node);
        }

        return s[node.Id].Status;
    }

    /// <summary>
    /// The status of <paramref name="node"/> for the agent represented by <paramref name="s"/> —
    /// a safe, allocation-free alternative to indexing the state array by <c>node.Id</c>.
    /// </summary>
    public NodeStatus StatusOf(NodeState[] s, BtNode<TContext> node) =>
        StatusOf((ReadOnlySpan<NodeState>)s, node);

    private void RequireStateLength(int length)
    {
        if (length != NodeCount)
        {
            ThrowStateLength(length);
        }
    }

    [DoesNotReturn]
    private void ThrowStateLength(int length) =>
        throw new ArgumentException(
            $"{StateLengthMessage} (got {length}, expected {NodeCount}).",
            "s"
        );

    [DoesNotReturn]
    private static void ThrowNotInTree(BtNode<TContext> node) =>
        throw new ArgumentException(
            node.Id == -1
                ? $"Node '{node.Name}' has not been built into a tree."
                : $"Node '{node.Name}' belongs to a different tree.",
            nameof(node)
        );
}
