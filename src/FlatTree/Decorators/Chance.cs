namespace FlatTree;

/// <summary>
/// Probabilistic gate: rolls <c>r = NextDouble()</c> in <c>[0, 1)</c>; if <c>r &lt; probability</c>
/// the child is ticked and its status returned, otherwise Failure is returned (the child is not
/// ticked). So <c>probability</c> is the chance of running the child: <c>Chance(0.4)</c> runs the
/// child roughly 40% of the time, and <c>probability == 1</c> always runs it. Validated to
/// <c>(0, 1]</c> at construction.
/// </summary>
/// <remarks>
/// The roll happens once per activation, not per tick: a child that returns Running latches the gate
/// open (<c>Cursor</c> bit 0) and is ticked to completion. Re-rolling would make a multi-tick child's
/// odds <c>probability^ticks</c> and abandon it mid-flight on a lost roll.
/// </remarks>
public sealed class Chance<TContext> : DecoratorNode<TContext>
    where TContext : IClock
{
    private const int LatchedFlag = 1;

    private readonly double _probability;
    private readonly IRandomProvider _randomProvider;

    internal Chance(
        string name,
        BtNode<TContext> child,
        double probability,
        IRandomProvider randomProvider
    )
        : base(name, child)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(probability, 0, nameof(probability));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(probability, 1, nameof(probability));

        _probability = probability;
        _randomProvider = randomProvider;
    }

    /// <summary>The probability of running the child, in <c>(0, 1]</c>.</summary>
    public double Probability => _probability;

    protected override TickResult Update(Span<NodeState> s, in TContext ctx)
    {
        ref var st = ref s[Id];

        if ((st.Cursor & LatchedFlag) == 0 && _randomProvider.NextDouble() >= _probability)
        {
            return TickResult.Failure;
        }

        var childStatus = Child.Tick(s, in ctx);

        if (childStatus == TickResult.Running)
        {
            st.Cursor |= LatchedFlag;
        }

        return childStatus;
    }

    protected override void OnTerminate(Span<NodeState> s, TickResult status, in TContext ctx) =>
        s[Id].Cursor &= ~LatchedFlag;

    protected override void DoReset(Span<NodeState> s, in TContext ctx)
    {
        s[Id].Cursor &= ~LatchedFlag;
        base.DoReset(s, in ctx);
    }
}
