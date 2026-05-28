namespace FlatTree;

/// <summary>
/// An action delegate for a stateful leaf (<see cref="StatefulDo{TContext}"/>) that needs to
/// keep progress across ticks. It receives the node's own per-agent scratch — the
/// <c>Cursor</c> and <c>Stamp</c> slots — by <c>ref</c>, so a multi-tick action (e.g. "move to
/// target over N ticks") can persist progress without smuggling it through the context.
/// The node's <c>Status</c> is intentionally not exposed; it is owned by the tick loop.
/// </summary>
/// <remarks>
/// Like <see cref="Do{TContext}"/>, the delegate must capture nothing — use a <c>static</c>
/// lambda or a method group — because one node instance is shared by every agent.
/// </remarks>
public delegate TickResult StatefulAction<TContext>(in TContext ctx, ref int cursor, ref long stamp)
    where TContext : IClock;
