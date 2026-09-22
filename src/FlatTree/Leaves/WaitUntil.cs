namespace FlatTree;

/// <summary>
/// Leaf that succeeds once a predicate holds, checked from the activating tick. A lapsed deadline
/// reports what the timeout handler returns, Failure without one.
/// </summary>
public sealed class WaitUntil<TContext> : WaitUntilLeaf<TContext>
    where TContext : IClock
{
    private readonly LeafPredicate<TContext> _predicate;
    private readonly FailureHandler<TContext>? _onTimeout;

    internal WaitUntil(
        string name,
        LeafPredicate<TContext> predicate,
        TimeSpan timeout,
        FailureHandler<TContext>? onTimeout,
        ClockSelector<TContext>? clock
    )
        : base(name, timeout, onTimeout is not null, clock)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        _predicate = predicate;
        _onTimeout = onTimeout;
    }

    private protected override bool Holds(in TContext ctx) => _predicate(in ctx);

    private protected override TickResult? Expire(in TContext ctx) => _onTimeout?.Invoke(in ctx);
}

/// <summary>
/// A <see cref="WaitUntil{TContext}"/> whose predicate and timeout handler also receive per-site
/// state authored on the node. Every agent shares it, so treat it as read-only.
/// </summary>
public sealed class WaitUntil<TContext, TState> : WaitUntilLeaf<TContext>
    where TContext : IClock
{
    private readonly TState _state;
    private readonly LeafPredicate<TContext, TState> _predicate;
    private readonly FailureHandler<TContext, TState>? _onTimeout;

    internal WaitUntil(
        string name,
        TState state,
        LeafPredicate<TContext, TState> predicate,
        TimeSpan timeout,
        FailureHandler<TContext, TState>? onTimeout,
        ClockSelector<TContext>? clock
    )
        : base(name, timeout, onTimeout is not null, clock)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        _state = state;
        _predicate = predicate;
        _onTimeout = onTimeout;
    }

    /// <summary>The state handed to the predicate and the timeout handler.</summary>
    public TState State => _state;

    private protected override bool Holds(in TContext ctx) => _predicate(in ctx, in _state);

    private protected override TickResult? Expire(in TContext ctx) =>
        _onTimeout?.Invoke(in ctx, in _state);
}
