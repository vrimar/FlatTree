namespace FlatTree;

/// <summary>
/// Per-context factory for authoring trees as nested factory calls. A composite takes its
/// children as arguments, so nesting is expressed as real argument nesting (CSharpier-safe).
/// Every node method has a named and a name-less overload; the name is for debugging/inspection
/// only, so omit it where a label adds nothing. The <see cref="RandomSource{TContext}"/> given to
/// <see cref="Bt.For{TContext}(RandomSource{TContext})"/> is handed to every random node; call sites
/// carry no <c>&lt;TContext&gt;</c> noise.
/// </summary>
public sealed class BtFactory<TContext>
    where TContext : IClock
{
    private readonly RandomSource<TContext> _randomSource;

    internal BtFactory(RandomSource<TContext> randomSource)
    {
        _randomSource = randomSource;
    }

    // --- composites ---

    public Selector<TContext> Selector(string name, params BtNode<TContext>[] children) =>
        new(name, children);

    public Selector<TContext> Selector(params BtNode<TContext>[] children) =>
        new(nameof(Selector), children);

    public Sequence<TContext> Sequence(string name, params BtNode<TContext>[] children) =>
        new(name, children);

    public Sequence<TContext> Sequence(params BtNode<TContext>[] children) =>
        new(nameof(Sequence), children);

    public PrioritySelector<TContext> PrioritySelector(
        string name,
        params BtNode<TContext>[] children
    ) => new(name, children);

    public PrioritySelector<TContext> PrioritySelector(params BtNode<TContext>[] children) =>
        new(nameof(PrioritySelector), children);

    public PrioritySequence<TContext> PrioritySequence(
        string name,
        params BtNode<TContext>[] children
    ) => new(name, children);

    public PrioritySequence<TContext> PrioritySequence(params BtNode<TContext>[] children) =>
        new(nameof(PrioritySequence), children);

    public RandomSelector<TContext> RandomSelector(
        string name,
        params BtNode<TContext>[] children
    ) => new(name, children, _randomSource);

    public RandomSelector<TContext> RandomSelector(params BtNode<TContext>[] children) =>
        new(nameof(RandomSelector), children, _randomSource);

    public RandomSequence<TContext> RandomSequence(
        string name,
        params BtNode<TContext>[] children
    ) => new(name, children, _randomSource);

    public RandomSequence<TContext> RandomSequence(params BtNode<TContext>[] children) =>
        new(nameof(RandomSequence), children, _randomSource);

    public SimpleParallel<TContext> SimpleParallel(
        string name,
        SimpleParallelPolicy policy,
        params BtNode<TContext>[] children
    ) => new(name, policy, children);

    public SimpleParallel<TContext> SimpleParallel(
        SimpleParallelPolicy policy,
        params BtNode<TContext>[] children
    ) => new(nameof(SimpleParallel), policy, children);

    // --- decorators ---

    public Inverter<TContext> Inverter(string name, BtNode<TContext> child) => new(name, child);

    public Inverter<TContext> Inverter(BtNode<TContext> child) => new(nameof(Inverter), child);

    public AlwaysSucceed<TContext> AlwaysSucceed(string name, BtNode<TContext> child) =>
        new(name, child);

    public AlwaysSucceed<TContext> AlwaysSucceed(BtNode<TContext> child) =>
        new(nameof(AlwaysSucceed), child);

    public AlwaysFail<TContext> AlwaysFail(string name, BtNode<TContext> child) => new(name, child);

    public AlwaysFail<TContext> AlwaysFail(BtNode<TContext> child) =>
        new(nameof(AlwaysFail), child);

    public AutoReset<TContext> AutoReset(string name, BtNode<TContext> child) => new(name, child);

    public AutoReset<TContext> AutoReset(BtNode<TContext> child) => new(nameof(AutoReset), child);

    public UntilSuccess<TContext> UntilSuccess(string name, BtNode<TContext> child) =>
        new(name, child);

    public UntilSuccess<TContext> UntilSuccess(BtNode<TContext> child) =>
        new(nameof(UntilSuccess), child);

    public UntilFailed<TContext> UntilFailed(string name, BtNode<TContext> child) =>
        new(name, child);

    public UntilFailed<TContext> UntilFailed(BtNode<TContext> child) =>
        new(nameof(UntilFailed), child);

    public Repeat<TContext> Repeat(string name, int count, BtNode<TContext> child) =>
        new(name, child, count);

    public Repeat<TContext> Repeat(int count, BtNode<TContext> child) =>
        new(nameof(Repeat), child, count);

    public Retry<TContext> Retry(string name, int attempts, BtNode<TContext> child) =>
        new(name, child, attempts);

    public Retry<TContext> Retry(int attempts, BtNode<TContext> child) =>
        new(nameof(Retry), child, attempts);

    public Catch<TContext> Catch(
        string name,
        BtNode<TContext> child,
        FailureHandler<TContext> handler
    )
    {
        BtGuard.RequireNoCapture(handler, nameof(handler));
        return new(name, child, handler);
    }

    public Catch<TContext> Catch(BtNode<TContext> child, FailureHandler<TContext> handler)
    {
        BtGuard.RequireNoCapture(handler, nameof(handler));
        return new(nameof(Catch), child, handler);
    }

    public Catch<TContext, TState> Catch<TState>(
        string name,
        BtNode<TContext> child,
        TState state,
        FailureHandler<TContext, TState> handler
    )
    {
        BtGuard.RequireNoCapture(handler, nameof(handler));
        return new(name, child, state, handler);
    }

    public Catch<TContext, TState> Catch<TState>(
        BtNode<TContext> child,
        TState state,
        FailureHandler<TContext, TState> handler
    )
    {
        BtGuard.RequireNoCapture(handler, nameof(handler));
        return new(nameof(Catch), child, state, handler);
    }

    public ForEach<TContext> ForEach(
        string name,
        CountOf<TContext> count,
        BtNode<TContext> body,
        IterationHook<TContext>? onIteration = null
    )
    {
        RequireLoopDelegates(count, onIteration);
        return new(name, body, count, onIteration);
    }

    public ForEach<TContext> ForEach(
        CountOf<TContext> count,
        BtNode<TContext> body,
        IterationHook<TContext>? onIteration = null
    )
    {
        RequireLoopDelegates(count, onIteration);
        return new(nameof(ForEach), body, count, onIteration);
    }

    public Forever<TContext> Forever(string name, BtNode<TContext> child) => new(name, child);

    public Forever<TContext> Forever(BtNode<TContext> child) => new(nameof(Forever), child);

    public Forever<TContext> Forever(
        string name,
        BtNode<TContext> child,
        LeafPredicate<TContext> exitWhen
    )
    {
        BtGuard.RequireNoCapture(exitWhen, nameof(exitWhen));
        return new(name, child, exitWhen);
    }

    public Forever<TContext> Forever(BtNode<TContext> child, LeafPredicate<TContext> exitWhen)
    {
        BtGuard.RequireNoCapture(exitWhen, nameof(exitWhen));
        return new(nameof(Forever), child, exitWhen);
    }

    public Cooldown<TContext> Cooldown(
        string name,
        TimeSpan duration,
        BtNode<TContext> child,
        ClockSelector<TContext>? clock = null
    ) => new(name, child, duration, clock);

    public Cooldown<TContext> Cooldown(
        TimeSpan duration,
        BtNode<TContext> child,
        ClockSelector<TContext>? clock = null
    ) => new(nameof(Cooldown), child, duration, clock);

    public RateLimiter<TContext> RateLimiter(
        string name,
        TimeSpan interval,
        BtNode<TContext> child,
        ClockSelector<TContext>? clock = null
    ) => new(name, child, interval, clock);

    public RateLimiter<TContext> RateLimiter(
        TimeSpan interval,
        BtNode<TContext> child,
        ClockSelector<TContext>? clock = null
    ) => new(nameof(RateLimiter), child, interval, clock);

    public TimeLimit<TContext> TimeLimit(
        string name,
        TimeSpan limit,
        BtNode<TContext> child,
        ClockSelector<TContext>? clock = null
    ) => new(name, child, limit, clock);

    public TimeLimit<TContext> TimeLimit(
        TimeSpan limit,
        BtNode<TContext> child,
        ClockSelector<TContext>? clock = null
    ) => new(nameof(TimeLimit), child, limit, clock);

    public Chance<TContext> Chance(string name, double probability, BtNode<TContext> child) =>
        new(name, child, probability, _randomSource);

    public Chance<TContext> Chance(double probability, BtNode<TContext> child) =>
        new(nameof(Chance), child, probability, _randomSource);

    // --- leaves ---

    public Do<TContext> Do(string name, LeafAction<TContext> action)
    {
        BtGuard.RequireNoCapture(action, nameof(action));
        return new(name, action);
    }

    public Do<TContext> Do(LeafAction<TContext> action)
    {
        BtGuard.RequireNoCapture(action, nameof(action));
        return new(nameof(Do), action);
    }

    public StatefulDo<TContext> Do(string name, StatefulAction<TContext> action)
    {
        BtGuard.RequireNoCapture(action, nameof(action));
        return new(name, action);
    }

    public StatefulDo<TContext> Do(StatefulAction<TContext> action)
    {
        BtGuard.RequireNoCapture(action, nameof(action));
        return new(nameof(Do), action);
    }

    public Condition<TContext> Condition(string name, LeafPredicate<TContext> predicate)
    {
        BtGuard.RequireNoCapture(predicate, nameof(predicate));
        return new(name, predicate);
    }

    public Condition<TContext> Condition(LeafPredicate<TContext> predicate)
    {
        BtGuard.RequireNoCapture(predicate, nameof(predicate));
        return new(nameof(Condition), predicate);
    }

    public Wait<TContext> Wait(
        string name,
        TimeSpan duration,
        ClockSelector<TContext>? clock = null
    ) => new(name, duration, duration, clock, _randomSource);

    public Wait<TContext> Wait(TimeSpan duration, ClockSelector<TContext>? clock = null) =>
        new(nameof(Wait), duration, duration, clock, _randomSource);

    public Wait<TContext> Wait(
        string name,
        TimeSpan min,
        TimeSpan max,
        ClockSelector<TContext>? clock = null
    ) => new(name, min, max, clock, _randomSource);

    public Wait<TContext> Wait(TimeSpan min, TimeSpan max, ClockSelector<TContext>? clock = null) =>
        new(nameof(Wait), min, max, clock, _randomSource);

    public Wait<TContext> Wait(
        string name,
        DurationOf<TContext> duration,
        ClockSelector<TContext>? clock = null
    ) => new(name, duration, clock, _randomSource);

    public Wait<TContext> Wait(
        DurationOf<TContext> duration,
        ClockSelector<TContext>? clock = null
    ) => new(nameof(Wait), duration, clock, _randomSource);

    // --- markers ---

    /// <summary>
    /// Marks <paramref name="node"/> as <see cref="BtNode{TContext}.Uninterruptible"/> and
    /// returns it, so it applies inline while authoring.
    /// </summary>
    public TNode Uninterruptible<TNode>(TNode node)
        where TNode : BtNode<TContext>
    {
        RequireUnbuilt(node, "Mark it Uninterruptible");
        node.Uninterruptible = true;
        return node;
    }

    /// <summary>
    /// Labels <paramref name="node"/> with <paramref name="tag"/> and returns it, so it applies
    /// inline while authoring. Read the nodes back with
    /// <see cref="BehaviourTree{TContext}.NodesWith"/>.
    /// </summary>
    public TNode Tagged<TNode>(TNode node, int tag)
        where TNode : BtNode<TContext>
    {
        RequireUnbuilt(node, "Tag it");

        if (tag == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tag), "Tag zero means untagged.");
        }

        node.Tag = tag;
        return node;
    }

    private static void RequireLoopDelegates(
        CountOf<TContext> count,
        IterationHook<TContext>? onIteration
    )
    {
        BtGuard.RequireNoCapture(count, nameof(count));

        if (onIteration is not null)
        {
            BtGuard.RequireNoCapture(onIteration, nameof(onIteration));
        }
    }

    // Build is what reads these markers, so setting one afterwards would silently do nothing.
    private static void RequireUnbuilt(BtNode<TContext> node, string what)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (node.Id != -1)
        {
            throw new InvalidOperationException(
                $"Node '{node.Name}' is already built into a tree. {what} before Build."
            );
        }
    }

    // --- build ---

    /// <summary>
    /// Walks the immutable node graph, assigns each node an <see cref="BtNode{TContext}.Id"/>
    /// via DFS pre-order, computes the node count, and returns the shared tree. Throws if a node
    /// is already in a tree, or if an <see cref="BtNode{TContext}.Uninterruptible"/> node sits
    /// beneath a reactive parent.
    /// </summary>
    public BehaviourTree<TContext> Build(BtNode<TContext> root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var nodes = new List<BtNode<TContext>>();

        // Reference equality, not Equals: a custom node may define value semantics, and two
        // distinct instances that compare equal are a legal tree.
        Collect(
            root,
            nodes,
            new HashSet<BtNode<TContext>>(ReferenceEqualityComparer.Instance),
            null
        );

        // Assign only once the whole walk has passed validation: numbering as we descend would
        // leave a rejected graph half-numbered and permanently unbuildable.
        for (var i = 0; i < nodes.Count; i++)
        {
            nodes[i].Id = i;
        }

        return new BehaviourTree<TContext>(root, nodes.ToArray());
    }

    private static void Collect(
        BtNode<TContext> node,
        List<BtNode<TContext>> nodes,
        HashSet<BtNode<TContext>> seen,
        BtNode<TContext>? reactiveAncestor
    )
    {
        // A node's children are validated at construction, but the caller may hold the array it
        // passed in and null an element before Build.
        ArgumentNullException.ThrowIfNull(node);

        if (node.Id != -1)
        {
            throw new InvalidOperationException(
                $"Node '{node.Name}' is already part of a tree. A node instance cannot be shared; "
                    + "build reusable subtrees as methods that construct a fresh node each call."
            );
        }

        if (!seen.Add(node))
        {
            throw new InvalidOperationException(
                $"Node '{node.Name}' appears more than once in this tree. A node instance cannot be "
                    + "shared; build reusable subtrees as methods that construct a fresh node each call."
            );
        }

        if (node.Uninterruptible && reactiveAncestor is not null)
        {
            throw new InvalidOperationException(
                $"Uninterruptible node '{node.Name}' is reachable beneath "
                    + $"'{reactiveAncestor.Name}' ({reactiveAncestor.GetType().Name.Split('`')[0]}), "
                    + "which tears down running children mid-flight. Move it out of that subtree, "
                    + "or guard the branch so it is never preempted."
            );
        }

        nodes.Add(node);

        var childReactiveAncestor = reactiveAncestor ?? (IsReactive(node) ? node : null);

        var childCount = node.ChildCount;
        for (var i = 0; i < childCount; i++)
        {
            Collect(node.GetChildForBuild(i), nodes, seen, childReactiveAncestor);
        }
    }

    private static bool IsReactive(BtNode<TContext> node) => node.PreemptsRunningChildren;
}
