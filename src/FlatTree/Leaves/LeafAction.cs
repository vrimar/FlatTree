namespace FlatTree;

/// <summary>
/// The action delegate for a <see cref="Do{TContext}"/> leaf. Takes the context by <c>in</c>, so
/// a lambda must spell out the modifier: <c>static (in Ctx c) =&gt; ...</c>. Must capture nothing.
/// </summary>
public delegate TickResult LeafAction<TContext>(in TContext ctx)
    where TContext : IClock;

/// <summary>
/// The predicate delegate for a <see cref="Condition{TContext}"/> leaf. Takes the context by
/// <c>in</c>, so a lambda must spell out the modifier: <c>static (in Ctx c) =&gt; ...</c>. Must
/// capture nothing.
/// </summary>
public delegate bool LeafPredicate<TContext>(in TContext ctx)
    where TContext : IClock;

/// <summary>
/// A <see cref="LeafAction{TContext}"/> that also receives per-site state authored on the node.
/// Every agent shares it, so treat it as read-only.
/// </summary>
public delegate TickResult LeafAction<TContext, TState>(in TContext ctx, in TState state)
    where TContext : IClock;

/// <summary>
/// A <see cref="LeafPredicate{TContext}"/> that also receives per-site state authored on the node.
/// Every agent shares it, so treat it as read-only.
/// </summary>
public delegate bool LeafPredicate<TContext, TState>(in TContext ctx, in TState state)
    where TContext : IClock;

/// <summary>The effect for an <see cref="Act{TContext}"/> leaf. Must capture nothing.</summary>
public delegate void LeafEffect<TContext>(in TContext ctx)
    where TContext : IClock;
