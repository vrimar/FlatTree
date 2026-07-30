namespace FlatTree;

/// <summary>
/// Mirror of <see cref="PrioritySelector{TContext}"/>: re-evaluates ALL children from index
/// 0 every tick, returning the first non-<see cref="NodeStatus.Success"/> status and
/// resetting every lower-priority child (index <c>j &gt; i</c>) when it does.
/// </summary>
public sealed class PrioritySequence<TContext> : CompositeNode<TContext>
    where TContext : IClock
{
    internal PrioritySequence(string name, BtNode<TContext>[] children)
        : base(name, children) { }

    protected override TickResult Update(Span<NodeState> s, in TContext ctx)
    {
        var children = Children;
        var n = children.Length;

        for (var i = 0; i < n; i++)
        {
            var childStatus = children[i].Tick(s, in ctx);

            if (childStatus != TickResult.Success)
            {
                for (var j = i + 1; j < n; j++)
                {
                    children[j].Reset(s, in ctx);
                }

                return childStatus;
            }
        }

        return TickResult.Success;
    }

    protected internal override bool PreemptsRunningChildren => true;
}
