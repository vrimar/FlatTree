using System.Reflection;

namespace FlatTree;

/// <summary>
/// Per-context factory for authoring trees as nested factory calls. A composite takes its
/// children as arguments, so nesting is expressed as real argument nesting (CSharpier-safe).
/// Every node method has a named and a name-less overload; the name is for debugging/inspection
/// only, so omit it where a label adds nothing. The injected <see cref="IRandomProvider"/> is
/// captured once and handed to every random node; call sites carry no <c>&lt;TContext&gt;</c> noise.
/// </summary>
public sealed class BtFactory<TContext>
    where TContext : IClock
{
    private readonly IRandomProvider _randomProvider;

    internal BtFactory(IRandomProvider randomProvider)
    {
        _randomProvider = randomProvider;
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
    ) => new(name, children, _randomProvider);

    public RandomSelector<TContext> RandomSelector(params BtNode<TContext>[] children) =>
        new(nameof(RandomSelector), children, _randomProvider);

    public RandomSequence<TContext> RandomSequence(
        string name,
        params BtNode<TContext>[] children
    ) => new(name, children, _randomProvider);

    public RandomSequence<TContext> RandomSequence(params BtNode<TContext>[] children) =>
        new(nameof(RandomSequence), children, _randomProvider);

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

    public Forever<TContext> Forever(string name, BtNode<TContext> child) => new(name, child);

    public Forever<TContext> Forever(BtNode<TContext> child) => new(nameof(Forever), child);

    public Cooldown<TContext> Cooldown(string name, TimeSpan duration, BtNode<TContext> child) =>
        new(name, child, duration);

    public Cooldown<TContext> Cooldown(TimeSpan duration, BtNode<TContext> child) =>
        new(nameof(Cooldown), child, duration);

    public RateLimiter<TContext> RateLimiter(
        string name,
        TimeSpan interval,
        BtNode<TContext> child
    ) => new(name, child, interval);

    public RateLimiter<TContext> RateLimiter(TimeSpan interval, BtNode<TContext> child) =>
        new(nameof(RateLimiter), child, interval);

    public TimeLimit<TContext> TimeLimit(string name, TimeSpan limit, BtNode<TContext> child) =>
        new(name, child, limit);

    public TimeLimit<TContext> TimeLimit(TimeSpan limit, BtNode<TContext> child) =>
        new(nameof(TimeLimit), child, limit);

    public Chance<TContext> Chance(string name, double probability, BtNode<TContext> child) =>
        new(name, child, probability, _randomProvider);

    public Chance<TContext> Chance(double probability, BtNode<TContext> child) =>
        new(nameof(Chance), child, probability, _randomProvider);

    // --- leaves ---

    public Do<TContext> Do(string name, LeafAction<TContext> action)
    {
        RequireNoCapture(action, nameof(action));
        return new(name, action);
    }

    public Do<TContext> Do(LeafAction<TContext> action)
    {
        RequireNoCapture(action, nameof(action));
        return new(nameof(Do), action);
    }

    public StatefulDo<TContext> Do(string name, StatefulAction<TContext> action)
    {
        RequireNoCapture(action, nameof(action));
        return new(name, action);
    }

    public StatefulDo<TContext> Do(StatefulAction<TContext> action)
    {
        RequireNoCapture(action, nameof(action));
        return new(nameof(Do), action);
    }

    public Condition<TContext> Condition(string name, LeafPredicate<TContext> predicate)
    {
        RequireNoCapture(predicate, nameof(predicate));
        return new(name, predicate);
    }

    public Condition<TContext> Condition(LeafPredicate<TContext> predicate)
    {
        RequireNoCapture(predicate, nameof(predicate));
        return new(nameof(Condition), predicate);
    }

    public Wait<TContext> Wait(string name, TimeSpan duration) => new(name, duration);

    public Wait<TContext> Wait(TimeSpan duration) => new(nameof(Wait), duration);

    // --- markers ---

    /// <summary>
    /// Marks <paramref name="node"/> as <see cref="BtNode{TContext}.Uninterruptible"/> and
    /// returns it, so it applies inline while authoring.
    /// </summary>
    public TNode Uninterruptible<TNode>(TNode node)
        where TNode : BtNode<TContext>
    {
        ArgumentNullException.ThrowIfNull(node);
        node.Uninterruptible = true;
        return node;
    }

    // A static method group has a null Target; a non-capturing lambda is cached on a compiler
    // singleton with no instance fields; a capturing closure's display class has one instance
    // field per captured variable. Build-time only — never on the tick path.
    private static void RequireNoCapture(Delegate action, string paramName)
    {
        var target = action.Target;
        if (target is null)
        {
            return;
        }

        var capturesState =
            target
                .GetType()
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Length > 0;

        if (capturesState)
        {
            throw new ArgumentException(
                "Leaf delegates must capture nothing (use a 'static' lambda or a method group). "
                    + "One node instance is shared by every agent, so a capturing delegate leaks "
                    + "that agent's state to all others.",
                paramName
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
        Collect(root, nodes, null);
        return new BehaviourTree<TContext>(root, nodes.ToArray());
    }

    private static void Collect(
        BtNode<TContext> node,
        List<BtNode<TContext>> nodes,
        BtNode<TContext>? reactiveAncestor
    )
    {
        if (node.Id != -1)
        {
            throw new InvalidOperationException(
                $"Node '{node.Name}' is already part of a tree. A node instance cannot be shared; "
                    + "build reusable subtrees as methods that construct a fresh node each call."
            );
        }

        if (node.Uninterruptible && reactiveAncestor is not null)
        {
            throw new InvalidOperationException(
                $"Uninterruptible node '{node.Name}' is reachable beneath "
                    + $"'{reactiveAncestor.Name}' ({reactiveAncestor.GetType().Name.Split('`')[0]}), "
                    + "which resets lower-priority branches mid-flight. Move it out of the reactive "
                    + "subtree, or guard the branch so it is never preempted."
            );
        }

        node.Id = nodes.Count;
        nodes.Add(node);

        var childReactiveAncestor =
            reactiveAncestor ?? (IsReactive(node) ? node : null);

        var childCount = node.ChildCount;
        for (var i = 0; i < childCount; i++)
        {
            Collect(node.GetChildForBuild(i), nodes, childReactiveAncestor);
        }
    }

    private static bool IsReactive(BtNode<TContext> node) =>
        node is PrioritySelector<TContext> or PrioritySequence<TContext>;
}
