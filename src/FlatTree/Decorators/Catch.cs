namespace FlatTree;

/// <summary>
/// Runs the child and, on Failure, hands it to a handler that says what the decorator reports in
/// its place — the swallowing <see cref="AlwaysSucceed{TContext}"/> does, with somewhere to record
/// why. Success and Running pass through untouched.
/// </summary>
/// <remarks>
/// The handler must return Success or Failure: Running would claim work in flight over a child that
/// has already completed.
/// </remarks>
public sealed class Catch<TContext> : DecoratorNode<TContext>
    where TContext : IClock
{
    private readonly FailureHandler<TContext> _handler;

    internal Catch(string name, BtNode<TContext> child, FailureHandler<TContext> handler)
        : base(name, child)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _handler = handler;
    }

    protected override TickResult Update(Span<NodeState> s, in TContext ctx)
    {
        var childStatus = Child.Tick(s, in ctx);

        if (childStatus != TickResult.Failure)
        {
            return childStatus;
        }

        var handled = _handler(in ctx);

        if (handled == TickResult.Running)
        {
            ThrowRunningVerdict();
        }

        return handled;
    }

    [DoesNotReturn]
    private void ThrowRunningVerdict() =>
        throw new InvalidOperationException(
            $"The failure handler on '{Name}' returned Running; it must return Success or Failure."
        );
}

/// <summary>
/// A <see cref="Catch{TContext}"/> whose handler also receives state authored on the node — the
/// per-site data a non-capturing delegate cannot carry itself.
/// </summary>
public sealed class Catch<TContext, TState> : DecoratorNode<TContext>
    where TContext : IClock
{
    private readonly TState _state;
    private readonly FailureHandler<TContext, TState> _handler;

    internal Catch(
        string name,
        BtNode<TContext> child,
        TState state,
        FailureHandler<TContext, TState> handler
    )
        : base(name, child)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _state = state;
        _handler = handler;
    }

    /// <summary>The state handed to the handler.</summary>
    public TState State => _state;

    protected override TickResult Update(Span<NodeState> s, in TContext ctx)
    {
        var childStatus = Child.Tick(s, in ctx);

        if (childStatus != TickResult.Failure)
        {
            return childStatus;
        }

        var handled = _handler(in ctx, in _state);

        if (handled == TickResult.Running)
        {
            ThrowRunningVerdict();
        }

        return handled;
    }

    [DoesNotReturn]
    private void ThrowRunningVerdict() =>
        throw new InvalidOperationException(
            $"The failure handler on '{Name}' returned Running; it must return Success or Failure."
        );
}
