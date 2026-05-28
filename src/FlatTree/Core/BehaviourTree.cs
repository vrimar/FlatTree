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

    /// <summary>Total number of nodes; the length of a per-agent <c>NodeState[]</c>.</summary>
    public int NodeCount => _nodes.Length;

    /// <summary>The root node.</summary>
    public BtNode<TContext> Root { get; }

    /// <summary>
    /// Every node in the tree, indexed by <see cref="BtNode{TContext}.Id"/> (DFS pre-order).
    /// Read-only; intended for debugging, inspection, and visualization.
    /// </summary>
    public IReadOnlyList<BtNode<TContext>> Nodes => _nodes;

    internal BehaviourTree(BtNode<TContext> root, BtNode<TContext>[] nodes)
    {
        Root = root;
        _nodes = nodes;
    }

    /// <summary>Allocates a fresh per-agent state array (the only per-agent allocation).</summary>
    public NodeState[] NewState() => new NodeState[NodeCount];

    /// <summary>
    /// Ticks the tree against an agent's state. Zero allocation. The state may be a slice of a
    /// larger contiguous buffer (e.g. <see cref="BehaviourTreePool{TContext}"/>).
    /// </summary>
    public TickResult Tick(Span<NodeState> s, in TContext ctx)
    {
        Debug.Assert(s.Length == NodeCount, StateLengthMessage);
        return Root.Tick(s, in ctx);
    }

    /// <summary>Ticks the tree against an agent's state. Zero allocation.</summary>
    public TickResult Tick(NodeState[] s, in TContext ctx) => Tick(s.AsSpan(), in ctx);

    /// <summary>Resets an agent's state back to fresh.</summary>
    public void Reset(Span<NodeState> s)
    {
        Debug.Assert(s.Length == NodeCount, StateLengthMessage);
        Root.Reset(s);
    }

    /// <summary>Resets an agent's state back to fresh.</summary>
    public void Reset(NodeState[] s) => Reset(s.AsSpan());

    /// <summary>
    /// The status of <paramref name="node"/> for the agent represented by <paramref name="s"/> —
    /// a safe, allocation-free alternative to indexing the state array by <c>node.Id</c>.
    /// </summary>
    public NodeStatus StatusOf(ReadOnlySpan<NodeState> s, BtNode<TContext> node)
    {
        Debug.Assert(s.Length == NodeCount, StateLengthMessage);
        Debug.Assert(
            (uint)node.Id < (uint)NodeCount && _nodes[node.Id] == node,
            "node is not part of this tree."
        );
        return s[node.Id].Status;
    }

    /// <summary>
    /// The status of <paramref name="node"/> for the agent represented by <paramref name="s"/> —
    /// a safe, allocation-free alternative to indexing the state array by <c>node.Id</c>.
    /// </summary>
    public NodeStatus StatusOf(NodeState[] s, BtNode<TContext> node) =>
        StatusOf((ReadOnlySpan<NodeState>)s, node);
}
