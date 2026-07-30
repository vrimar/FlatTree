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
