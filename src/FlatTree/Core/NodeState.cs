using System.Runtime.InteropServices;

namespace FlatTree;

/// <summary>
/// Per-agent, per-node mutable state. The whole per-agent tree state is a single
/// <c>NodeState[]</c> indexed by <see cref="BtNode{TContext}.Id"/> (array-of-struct, NOT
/// struct-of-arrays). 16 bytes with natural alignment {byte + pad + int + long}.
/// </summary>
/// <remarks>
/// Sentinel discipline: <see cref="Reset"/>/<c>NewState</c> zero a slot, so a zeroed
/// <see cref="Stamp"/> is indistinguishable from a freshly reset value. Never use
/// <c>Stamp == 0</c> to mean "not started" — use an explicit flag bit in <see cref="Cursor"/>
/// as the discriminator and only read <see cref="Stamp"/> when that flag says it is valid.
/// </remarks>
[StructLayout(LayoutKind.Auto)]
public struct NodeState
{
    /// <summary>Owned and written ONLY by <see cref="BtNode{TContext}.Tick"/>.</summary>
    public NodeStatus Status;

    /// <summary>Child index / counter / packed flags+status, depending on the node.</summary>
    public int Cursor;

    /// <summary>Cooldown-or-wait start timestamp / shuffle seed, depending on the node.</summary>
    public long Stamp;
}
