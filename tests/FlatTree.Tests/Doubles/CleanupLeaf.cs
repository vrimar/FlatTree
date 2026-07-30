namespace FlatTree.Tests.Doubles;

/// <summary>
/// A leaf that parks in Running, acquires a named resource on its first tick and releases it
/// through the context when it terminates or is reset. Models the consumer's abort-time cleanup:
/// the only way it can un-acquire is by receiving the context in <c>DoReset</c>.
/// </summary>
public sealed class CleanupLeaf : LeafNode<RecordingClock>
{
    private const int AcquiredFlag = 1;

    private readonly string _resource;
    private readonly TickResult _result;

    public CleanupLeaf(string resource, TickResult result = TickResult.Running)
        : base(resource)
    {
        _resource = resource;
        _result = result;
    }

    protected override TickResult Update(Span<NodeState> s, in RecordingClock ctx)
    {
        s[Id].Cursor |= AcquiredFlag;
        return _result;
    }

    protected override void OnTerminate(
        Span<NodeState> s,
        TickResult status,
        in RecordingClock ctx
    ) => ReleaseIfHeld(s, ctx, "terminate");

    protected override void DoReset(Span<NodeState> s, in RecordingClock ctx) =>
        ReleaseIfHeld(s, ctx, "reset");

    private void ReleaseIfHeld(Span<NodeState> s, RecordingClock ctx, string via)
    {
        ref var st = ref s[Id];
        if ((st.Cursor & AcquiredFlag) == 0)
        {
            return;
        }

        st.Cursor &= ~AcquiredFlag;
        ctx.Release($"{_resource}:{via}");
    }
}
