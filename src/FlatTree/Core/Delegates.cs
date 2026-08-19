namespace FlatTree;

/// <summary>
/// Picks which logical clock a timed node paces on. Defaults to <see cref="IClock.NowMs"/>; supply
/// one when the context carries more than one. Must capture nothing.
/// </summary>
public delegate long ClockSelector<TContext>(in TContext ctx)
    where TContext : IClock;

/// <summary>
/// A duration only the context knows, resolved once per activation rather than fixed at build
/// time. Must be non-negative, and must capture nothing.
/// </summary>
public delegate TimeSpan DurationOf<TContext>(in TContext ctx)
    where TContext : IClock;

/// <summary>
/// An iteration count only the context knows, read by <see cref="ForEach{TContext}"/>.
/// </summary>
public delegate int CountOf<TContext>(in TContext ctx)
    where TContext : IClock;

/// <summary>
/// Publishes the current iteration index before <see cref="ForEach{TContext}"/> ticks its body.
/// </summary>
public delegate void IterationHook<TContext>(in TContext ctx, int index)
    where TContext : IClock;

/// <summary>
/// Handles a child's Failure inside <see cref="Catch{TContext}"/> and says what the decorator
/// reports in its place. Must return Success or Failure, and must capture nothing.
/// </summary>
public delegate TickResult FailureHandler<TContext>(in TContext ctx)
    where TContext : IClock;

/// <summary>
/// A <see cref="FailureHandler{TContext}"/> that also receives the state authored on the node —
/// where per-site data belongs, since a delegate that closed over it is shared by every agent.
/// </summary>
public delegate TickResult FailureHandler<TContext, TState>(in TContext ctx, in TState state)
    where TContext : IClock;

/// <summary>
/// Supplies the randomness a <see cref="Chance{TContext}"/>, <see cref="RandomSelector{TContext}"/>,
/// <see cref="RandomSequence{TContext}"/> or jittered <see cref="Wait{TContext}"/> draws from.
/// Sourcing it from the context is what gives each agent its own stream: a tree-wide provider is one
/// advancing stream shared by every agent. Must capture nothing.
/// </summary>
public delegate IRandomProvider RandomSource<TContext>(in TContext ctx)
    where TContext : IClock;

internal static class DefaultClock<TContext>
    where TContext : IClock
{
    public static readonly ClockSelector<TContext> Selector = static (in TContext ctx) => ctx.NowMs;
}
