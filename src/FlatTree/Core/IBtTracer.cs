namespace FlatTree;

/// <summary>
/// Observes every node of one tree as it ticks and resets, for tracing and tooling. Set it through
/// <see cref="BehaviourTree{TContext}.Tracer"/>. The tree is shared by every agent of its archetype, so
/// one tracer sees them all; tell agents apart through the context each call passes. Called on the tick path:
/// keep it cheap, and allocation-free if the tree's ticks must stay so.
/// </summary>
public interface IBtTracer<TContext>
    where TContext : IClock
{
    /// <summary><paramref name="node"/> finished a tick with <paramref name="result"/>, after its
    /// completion hook ran. <paramref name="previous"/> is the status it held going in: anything but
    /// Running means this tick started it.</summary>
    void Ticked(
        BtNode<TContext> node,
        NodeStatus previous,
        TickResult result,
        in TContext ctx
    );

    /// <summary><paramref name="node"/>, which had ticked, was reset to Fresh.
    /// <paramref name="previous"/> is the status it held: Running when it was torn down
    /// mid-flight.</summary>
    void Reset(BtNode<TContext> node, NodeStatus previous, in TContext ctx);
}
