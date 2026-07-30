using System.Text;

namespace FlatTree.Debugging;

/// <summary>
/// Opt-in debugging helpers (<c>using FlatTree.Debugging;</c>) that render a tree's structure
/// and, optionally, a single agent's live state. These walk the node graph and allocate strings,
/// so they are for diagnostics only — never call them on the hot path.
/// </summary>
public static class BehaviourTreeDebug
{
    /// <summary>An indented view of the tree structure (no per-agent state).</summary>
    public static string ToDebugString<TContext>(this BehaviourTree<TContext> tree)
        where TContext : IClock => BuildDebugString(tree, default);

    /// <summary>
    /// An indented view annotated with <paramref name="state"/>: each node shows its
    /// <see cref="NodeStatus"/> and any non-zero <c>Cursor</c>/<c>Stamp</c> scratch.
    /// </summary>
    public static string ToDebugString<TContext>(
        this BehaviourTree<TContext> tree,
        ReadOnlySpan<NodeState> state
    )
        where TContext : IClock => BuildDebugString(tree, state);

    /// <summary>A Graphviz DOT digraph of the tree structure.</summary>
    public static string ToDot<TContext>(this BehaviourTree<TContext> tree)
        where TContext : IClock => BuildDot(tree, default);

    /// <summary>
    /// A Graphviz DOT digraph with each node filled by its <paramref name="state"/> status
    /// (Running=yellow, Success=green, Failure=red, Fresh=lightgray).
    /// </summary>
    public static string ToDot<TContext>(
        this BehaviourTree<TContext> tree,
        ReadOnlySpan<NodeState> state
    )
        where TContext : IClock => BuildDot(tree, state);

    private static string BuildDebugString<TContext>(
        BehaviourTree<TContext> tree,
        ReadOnlySpan<NodeState> state
    )
        where TContext : IClock
    {
        ArgumentNullException.ThrowIfNull(tree);
        RequireStateLength(tree, state);
        var sb = new StringBuilder();
        AppendNode(sb, tree.Root, state, 0);
        return sb.ToString();
    }

    private static void RequireStateLength<TContext>(
        BehaviourTree<TContext> tree,
        ReadOnlySpan<NodeState> state
    )
        where TContext : IClock
    {
        if (!state.IsEmpty && state.Length != tree.NodeCount)
        {
            throw new ArgumentException(
                $"State array length must equal NodeCount (got {state.Length}, "
                    + $"expected {tree.NodeCount}).",
                nameof(state)
            );
        }
    }

    private static void AppendNode<TContext>(
        StringBuilder sb,
        BtNode<TContext> node,
        ReadOnlySpan<NodeState> state,
        int depth
    )
        where TContext : IClock
    {
        sb.Append(' ', depth * 2)
            .Append('[')
            .Append(node.Id)
            .Append("] ")
            .Append(node.Name)
            .Append(" (")
            .Append(TypeName(node))
            .Append(')');

        if (!state.IsEmpty)
        {
            var st = state[node.Id];
            sb.Append(" = ").Append(st.Status);

            if (st.Cursor != 0)
            {
                sb.Append(" cursor=").Append(st.Cursor);
            }

            if (st.Stamp != 0)
            {
                sb.Append(" stamp=").Append(st.Stamp);
            }
        }

        sb.Append('\n');

        var childCount = node.ChildCount;
        for (var i = 0; i < childCount; i++)
        {
            AppendNode(sb, node.GetChildForBuild(i), state, depth + 1);
        }
    }

    private static string BuildDot<TContext>(
        BehaviourTree<TContext> tree,
        ReadOnlySpan<NodeState> state
    )
        where TContext : IClock
    {
        ArgumentNullException.ThrowIfNull(tree);
        RequireStateLength(tree, state);
        var sb = new StringBuilder();
        sb.Append("digraph BehaviourTree {\n");
        sb.Append("  node [shape=box, fontname=\"monospace\"];\n");

        foreach (var node in tree.Nodes)
        {
            sb.Append("  n")
                .Append(node.Id)
                .Append(" [label=\"")
                .Append(Escape(node.Name))
                .Append("\\n(")
                .Append(TypeName(node))
                .Append(")\"");

            if (!state.IsEmpty)
            {
                sb.Append(", style=filled, fillcolor=").Append(Color(state[node.Id].Status));
            }

            sb.Append("];\n");
        }

        foreach (var node in tree.Nodes)
        {
            var childCount = node.ChildCount;
            for (var i = 0; i < childCount; i++)
            {
                sb.Append("  n")
                    .Append(node.Id)
                    .Append(" -> n")
                    .Append(node.GetChildForBuild(i).Id)
                    .Append(";\n");
            }
        }

        sb.Append("}\n");
        return sb.ToString();
    }

    private static string TypeName<TContext>(BtNode<TContext> node)
        where TContext : IClock
    {
        var name = node.GetType().Name;
        var tick = name.IndexOf('`');
        return tick < 0 ? name : name[..tick];
    }

    private static string Color(NodeStatus status) =>
        status switch
        {
            NodeStatus.Running => "yellow",
            NodeStatus.Success => "green",
            NodeStatus.Failure => "red",
            _ => "lightgray",
        };

    private static string Escape(string value) =>
        value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n");
}
