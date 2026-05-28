namespace FlatTree.Tests.Doubles;

/// <summary>
/// A stateful leaf used as a scriptable child in composite/decorator tests. It tracks lifecycle
/// call counts on the instance (valid because tests run a single agent against a single tree)
/// and returns a settable <see cref="ReturnStatus"/>.
/// </summary>
/// <remarks>
/// The flattened model has no <c>OnInitialize</c> hook; "initialize" is emulated by detecting a
/// <see cref="NodeStatus.Fresh"/> slot at the start of <see cref="Update"/> (it fires only when
/// transitioning out of Fresh).
/// </remarks>
public sealed class MockNode : BtNode<FakeClock>
{
    public MockNode()
        : base("MockNode") { }

    /// <summary>The status returned by <see cref="Update"/>. Settable between ticks.</summary>
    public TickResult ReturnStatus { get; set; } = TickResult.Success;

    public int InitializeCallCount { get; private set; }

    public int UpdateCallCount { get; private set; }

    public int TerminateCallCount { get; private set; }

    public int ResetCount { get; private set; }

    public TickResult TerminateStatus { get; private set; }

    public NodeStatus ResetStatus { get; private set; }

    protected override TickResult Update(Span<NodeState> s, in FakeClock ctx)
    {
        if (s[Id].Status == NodeStatus.Fresh)
        {
            InitializeCallCount++;
        }

        UpdateCallCount++;
        return ReturnStatus;
    }

    protected override void OnTerminate(Span<NodeState> s, TickResult status)
    {
        TerminateCallCount++;
        TerminateStatus = status;
    }

    protected override void DoReset(Span<NodeState> s)
    {
        ResetStatus = s[Id].Status;
        ResetCount++;
    }
}
