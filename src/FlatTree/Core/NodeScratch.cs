namespace FlatTree;

/// <summary>
/// Helpers over a node's own <see cref="NodeState"/> slot for the started-once-then-timed idiom:
/// <c>Cursor</c> bit 0 is the started flag, <c>Stamp</c> the instant it started. A node that packs
/// <c>Cursor</c> differently must not mix these in.
/// </summary>
public static class NodeScratch
{
    /// <summary>The <c>Cursor</c> bit <see cref="TryBegin"/> latches. Bits 1-31 are the node's own.</summary>
    public const int BegunFlag = 1;

    /// <summary>
    /// Stamps <paramref name="now"/> and returns <c>true</c> on the tick that starts the node;
    /// <c>false</c> on every tick after, leaving the stamp alone.
    /// </summary>
    public static bool TryBegin(this ref NodeState st, long now)
    {
        if ((st.Cursor & BegunFlag) != 0)
        {
            return false;
        }

        st.Cursor |= BegunFlag;
        st.Stamp = now;
        return true;
    }

    /// <summary>Whether the node has started and its <c>Stamp</c> is therefore meaningful.</summary>
    public static bool HasBegun(this ref NodeState st) => (st.Cursor & BegunFlag) != 0;

    /// <summary>Logical time since <see cref="TryBegin"/>. Meaningless unless <see cref="HasBegun"/>.</summary>
    public static long Since(this ref NodeState st, long now) => now - st.Stamp;

    /// <summary>Clears the started flag, keeping the stamp and any bits the node owns.</summary>
    public static void ClearBegun(this ref NodeState st) => st.Cursor &= ~BegunFlag;

    /// <summary>Clears both scratch slots, so the next activation starts from zero.</summary>
    public static void ClearScratch(this ref NodeState st)
    {
        st.Cursor = 0;
        st.Stamp = 0;
    }
}
