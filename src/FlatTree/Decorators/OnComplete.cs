namespace FlatTree;

/// <summary>
/// Hands the child's outcome, Success or Failure, to a handler that decides what to report. Running
/// passes through; a reset child never completes, so abort cleanup belongs in <c>DoReset</c>.
/// </summary>
public sealed class OnComplete<TContext> : OutcomeDecorator<TContext>
    where TContext : IClock
{
    private readonly CompletionHandler<TContext> _handler;

    internal OnComplete(string name, BtNode<TContext> child, CompletionHandler<TContext> handler)
        : base(name, child, failureOnly: false)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _handler = handler;
    }

    private protected override TickResult Handle(in TContext ctx, TickResult outcome) =>
        _handler(in ctx, outcome);
}

/// <summary>
/// An <see cref="OnComplete{TContext}"/> whose handler also receives per-site state authored on the
/// node. Every agent shares it, so treat it as read-only.
/// </summary>
public sealed class OnComplete<TContext, TState> : OutcomeDecorator<TContext>
    where TContext : IClock
{
    private readonly TState _state;
    private readonly CompletionHandler<TContext, TState> _handler;

    internal OnComplete(
        string name,
        BtNode<TContext> child,
        TState state,
        CompletionHandler<TContext, TState> handler
    )
        : base(name, child, failureOnly: false)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _state = state;
        _handler = handler;
    }

    /// <summary>The state handed to the handler.</summary>
    public TState State => _state;

    private protected override TickResult Handle(in TContext ctx, TickResult outcome) =>
        _handler(in ctx, in _state, outcome);
}
