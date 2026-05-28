namespace FlatTree;

/// <summary>
/// Repeats the child until it has succeeded <c>count</c> times. <c>Cursor</c> is the success
/// counter. Each time the child succeeds before the count is reached, the child is reset and
/// Running is returned; on the final success, Success is returned. Failure/Running pass
/// through. On completion or reset, the counter is cleared and the child is reset.
/// </summary>
public sealed class Repeat<TContext> : DecoratorNode<TContext>
    where TContext : IClock
{
    private readonly int _count;

    internal Repeat(string name, BtNode<TContext> child, int count)
        : base(name, child)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1, nameof(count));
        _count = count;
    }

    /// <summary>The required number of child successes.</summary>
    public int Count => _count;

    protected override TickResult Update(Span<NodeState> s, in TContext ctx)
    {
        var childStatus = Child.Tick(s, in ctx);

        if (childStatus == TickResult.Success)
        {
            ref var st = ref s[Id];
            st.Cursor++;

            if (st.Cursor < _count)
            {
                Child.Reset(s);
                return TickResult.Running;
            }
        }

        return childStatus;
    }

    protected override void OnTerminate(Span<NodeState> s, TickResult status)
    {
        s[Id].Cursor = 0;
        Child.Reset(s);
    }

    protected override void DoReset(Span<NodeState> s)
    {
        s[Id].Cursor = 0;
        base.DoReset(s);
    }
}
