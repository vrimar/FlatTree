namespace FlatTree;

/// <summary>
/// Repeats the child until it has succeeded <c>count</c> times. <c>Cursor</c> is the success
/// counter. Each time the child succeeds before the count is reached, the child is reset and
/// Running is returned; on the final success, Success is returned. Failure/Running pass
/// through. On completion or reset, the counter is cleared and the child is reset.
/// </summary>
/// <remarks>
/// <c>exitWhen</c> is checked after each child success, once the child is reset as
/// <see cref="Forever{TContext}"/> does, and ends it with Success when it holds.
/// </remarks>
public sealed class Repeat<TContext> : DecoratorNode<TContext>
    where TContext : IClock
{
    private readonly int _count;
    private readonly LeafPredicate<TContext>? _exitWhen;

    internal Repeat(
        string name,
        BtNode<TContext> child,
        int count,
        LeafPredicate<TContext>? exitWhen = null
    )
        : base(name, child)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1, nameof(count));
        _count = count;
        _exitWhen = exitWhen;
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
                Child.Reset(s, in ctx);

                if (_exitWhen is null || !_exitWhen(in ctx))
                {
                    return TickResult.Running;
                }
            }
        }

        return childStatus;
    }

    protected override void OnTerminate(Span<NodeState> s, TickResult status, in TContext ctx)
    {
        s[Id].Cursor = 0;
        Child.Reset(s, in ctx);
    }

    protected override void DoReset(Span<NodeState> s, in TContext ctx)
    {
        s[Id].Cursor = 0;
        base.DoReset(s, in ctx);
    }
}
