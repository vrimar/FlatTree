namespace FlatTree;

/// <summary>
/// Retries the child until it succeeds or <c>attempts</c> tries are spent, then Failure.
/// <c>Cursor</c> is the spent-attempt counter. Each failure resets the child and returns Running,
/// so the next attempt starts fresh; Success and Running pass through. On completion or reset the
/// counter is cleared and the child is reset.
/// </summary>
/// <remarks>
/// <c>attempts</c> is the total number of tries, not the retries after the first: <c>Retry(1, x)</c>
/// is plain <c>x</c>.
/// </remarks>
public sealed class Retry<TContext> : DecoratorNode<TContext>
    where TContext : IClock
{
    private readonly int _attempts;

    internal Retry(string name, BtNode<TContext> child, int attempts)
        : base(name, child)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(attempts, 1, nameof(attempts));
        _attempts = attempts;
    }

    /// <summary>The total number of tries allowed.</summary>
    public int Attempts => _attempts;

    protected override TickResult Update(Span<NodeState> s, in TContext ctx)
    {
        var childStatus = Child.Tick(s, in ctx);

        if (childStatus != TickResult.Failure)
        {
            return childStatus;
        }

        ref var st = ref s[Id];
        st.Cursor++;

        if (st.Cursor >= _attempts)
        {
            return TickResult.Failure;
        }

        Child.Reset(s, in ctx);
        return TickResult.Running;
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
