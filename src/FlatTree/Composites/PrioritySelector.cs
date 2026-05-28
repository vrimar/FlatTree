namespace FlatTree;

/// <summary>
/// Re-evaluates ALL children from index 0 every tick (no persisted cursor). Returns the
/// first non-<see cref="NodeStatus.Failure"/> status; when it does, every lower-priority
/// child (index <c>j &gt; i</c>) is explicitly reset so a previously-running lower-priority
/// child is cleared when a higher-priority child becomes non-failed.
/// </summary>
public sealed class PrioritySelector<TContext> : CompositeNode<TContext>
    where TContext : IClock
{
    internal PrioritySelector(string name, BtNode<TContext>[] children)
        : base(name, children) { }

    protected override TickResult Update(Span<NodeState> s, in TContext ctx)
    {
        var children = Children;
        var n = children.Length;

        for (var i = 0; i < n; i++)
        {
            var childStatus = children[i].Tick(s, in ctx);

            if (childStatus != TickResult.Failure)
            {
                for (var j = i + 1; j < n; j++)
                {
                    children[j].Reset(s);
                }

                return childStatus;
            }
        }

        return TickResult.Failure;
    }
}
