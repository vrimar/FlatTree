namespace FlatTree;

/// <summary>
/// Base class for every node. A node is immutable and shared across all agents of an
/// archetype: it holds no per-agent state. All mutable, per-agent state lives in the
/// <c>NodeState[]</c> slot at <see cref="Id"/>.
/// </summary>
/// <remarks>
/// The central design rule: in this flattened-array model there is NO implicit reset. Wherever
/// behaviour relies on "the child re-runs next time" or "reset all children", the corresponding
/// node makes an explicit recursive <see cref="Reset"/> call on its children (see the node
/// implementations).
/// </remarks>
public abstract class BtNode<TContext>
    where TContext : IClock
{
    /// <summary>Assigned once by the builder via DFS pre-order. Indexes the agent state array.</summary>
    public int Id { get; internal set; } = -1;

    /// <summary>Human-readable name, for debugging/inspection only.</summary>
    public string Name { get; }

    /// <summary>
    /// Marks work that must not be torn down mid-flight (irreversible or externally-visible side
    /// effects). <see cref="BtFactory{TContext}.Build"/> rejects such a node beneath any node that
    /// reports <see cref="PreemptsRunningChildren"/>, since those tear a running child down without
    /// warning.
    /// </summary>
    public bool Uninterruptible { get; protected internal set; }

    /// <summary>
    /// Whether this node can <see cref="Reset"/> a child that is still Running. Override to
    /// <c>true</c> on a custom node that does, so <see cref="BtFactory{TContext}.Build"/> can reject
    /// an <see cref="Uninterruptible"/> node beneath it. Resetting a *completed* child doesn't count.
    /// </summary>
    protected internal virtual bool PreemptsRunningChildren => false;

    protected BtNode(string name)
    {
        Name = name;
    }

    /// <summary>
    /// The only writer of <c>s[Id].Status</c> on the tick path (<see cref="Reset"/> is the other,
    /// and only ever writes <see cref="NodeStatus.Fresh"/>). Runs <see cref="Update"/>, stores the
    /// resulting status, and fires <see cref="OnTerminate"/> when the node completes
    /// (returns a non-<see cref="TickResult.Running"/> status).
    /// </summary>
    public TickResult Tick(Span<NodeState> s, in TContext ctx)
    {
        var status = Update(s, in ctx);

        // TickResult values are aligned with NodeStatus, so this cast is free.
        s[Id].Status = (NodeStatus)status;

        if (status != TickResult.Running)
        {
            OnTerminate(s, status, in ctx);
        }

        return status;
    }

    /// <summary>
    /// Resets this node (and, for composites/decorators, recursively its children) back to
    /// <see cref="NodeStatus.Fresh"/>. A node that is already Fresh is assumed to have Fresh
    /// children and short-circuits.
    /// </summary>
    /// <remarks>
    /// The library's only abort notification: <see cref="DoReset"/> receives
    /// <paramref name="ctx"/> so a preempted node can release what it acquired.
    /// The Fresh short-circuit assumes <c>Update</c> ran to completion; if one threw, recover with
    /// <see cref="BehaviourTree{TContext}.ResetAll(Span{NodeState}, in TContext)"/> instead.
    /// </remarks>
    public void Reset(Span<NodeState> s, in TContext ctx)
    {
        if (s[Id].Status == NodeStatus.Fresh)
        {
            return;
        }

        DoReset(s, in ctx);
        s[Id].Status = NodeStatus.Fresh;
    }

    internal void ResetForRecovery(Span<NodeState> s, in TContext ctx)
    {
        DoReset(s, in ctx);
        s[Id].Status = NodeStatus.Fresh;
    }

    /// <summary>
    /// Computes the node's status for this tick. Must NEVER write its own <c>Status</c> (it
    /// may write its own <c>Cursor</c>/<c>Stamp</c> and its children's slots).
    /// </summary>
    protected abstract TickResult Update(Span<NodeState> s, in TContext ctx);

    /// <summary>Fires after <see cref="Update"/> when the node completes. May reset children.</summary>
    protected virtual void OnTerminate(Span<NodeState> s, TickResult status, in TContext ctx) { }

    /// <summary>
    /// Performs reset side effects (releasing what the node acquired). May read <c>s[Id]</c>, which
    /// still holds the pre-reset status. The default resets every child, so a custom node with
    /// children stays consistent without overriding; an override that adds its own cleanup should
    /// call <c>base.DoReset</c> unless it resets its children itself.
    /// </summary>
    protected virtual void DoReset(Span<NodeState> s, in TContext ctx)
    {
        // Without this a custom composite that forgets to override leaves children non-Fresh under
        // a Fresh parent, which Reset then short-circuits past forever.
        var count = ChildCount;
        for (var i = 0; i < count; i++)
        {
            GetChildForBuild(i).Reset(s, in ctx);
        }
    }

    // --- builder traversal (DFS pre-order Id assignment) ---
    // protected internal so external assemblies can author custom composites/decorators
    // whose children participate in Build.

    protected internal virtual int ChildCount => 0;

    protected internal virtual BtNode<TContext> GetChildForBuild(int index) =>
        throw new InvalidOperationException("Node has no children.");
}
