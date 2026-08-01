namespace FlatTree.Bench;

/// <summary>
/// A deliberately classic Behaviour Tree: one node object per node <b>per agent</b>, each node's
/// mutable state stored in the object itself. This is the baseline FlatTree's flattened layout
/// (one shared node graph + a contiguous per-agent <c>NodeState[]</c>) is measured against.
/// </summary>
/// <remarks>
/// <para>
/// Tick semantics mirror <see cref="BenchTrees.Boss"/> exactly — the same re-evaluating
/// <c>PrioritySelector</c>, resuming <c>Sequence</c> cursor, reset-on-completion cascade, and
/// cooldown-survives-reset rule. Any divergence invalidates the comparison, so a change here must
/// be mirrored from the corresponding FlatTree node.
/// </para>
/// <para>
/// It is not a strawman: the context is the same <c>readonly struct</c> passed by <c>in</c>, the
/// leaf delegates are the same cached statics, and no exceptions are used for control flow. The
/// single variable is where per-agent state lives.
/// </para>
/// </remarks>
public abstract class NaiveNode
{
    private NodeStatus _status;

    public TickResult Tick(in BenchContext ctx)
    {
        var status = Update(in ctx);
        _status = (NodeStatus)status;

        if (status != TickResult.Running)
        {
            OnTerminate(in ctx);
        }

        return status;
    }

    public void Reset(in BenchContext ctx)
    {
        if (_status == NodeStatus.Fresh)
        {
            return;
        }

        DoReset(in ctx);
        _status = NodeStatus.Fresh;
    }

    protected abstract TickResult Update(in BenchContext ctx);

    protected virtual void OnTerminate(in BenchContext ctx) { }

    protected virtual void DoReset(in BenchContext ctx) { }
}

public abstract class NaiveComposite : NaiveNode
{
    protected readonly NaiveNode[] Children;

    protected NaiveComposite(NaiveNode[] children)
    {
        Children = children;
    }

    protected override void OnTerminate(in BenchContext ctx) => DoReset(in ctx);

    protected override void DoReset(in BenchContext ctx)
    {
        for (var i = 0; i < Children.Length; i++)
        {
            Children[i].Reset(in ctx);
        }
    }
}

public sealed class NaivePrioritySelector : NaiveComposite
{
    public NaivePrioritySelector(params NaiveNode[] children)
        : base(children) { }

    protected override TickResult Update(in BenchContext ctx)
    {
        var children = Children;
        var n = children.Length;

        for (var i = 0; i < n; i++)
        {
            var childStatus = children[i].Tick(in ctx);

            if (childStatus != TickResult.Failure)
            {
                for (var j = i + 1; j < n; j++)
                {
                    children[j].Reset(in ctx);
                }

                return childStatus;
            }
        }

        return TickResult.Failure;
    }
}

public sealed class NaiveSequence : NaiveComposite
{
    private int _cursor;

    public NaiveSequence(params NaiveNode[] children)
        : base(children) { }

    protected override TickResult Update(in BenchContext ctx)
    {
        var children = Children;

        do
        {
            var childStatus = children[_cursor].Tick(in ctx);

            if (childStatus != TickResult.Success)
            {
                return childStatus;
            }
        } while (++_cursor < children.Length);

        return TickResult.Success;
    }

    protected override void DoReset(in BenchContext ctx)
    {
        _cursor = 0;
        base.DoReset(in ctx);
    }
}

public sealed class NaiveCooldown : NaiveNode
{
    private readonly NaiveNode _child;
    private readonly long _durationMs;
    private bool _onCooldown;
    private long _stamp;

    public NaiveCooldown(TimeSpan duration, NaiveNode child)
    {
        _durationMs = (long)duration.TotalMilliseconds;
        _child = child;
    }

    protected override TickResult Update(in BenchContext ctx)
    {
        var now = ctx.NowMs;

        if (_onCooldown)
        {
            if ((now - _stamp) < _durationMs)
            {
                return TickResult.Failure;
            }

            _onCooldown = false;
            _stamp = 0;
        }

        var childStatus = _child.Tick(in ctx);

        if (childStatus == TickResult.Success)
        {
            _onCooldown = true;
            _stamp = now;
        }

        return childStatus;
    }

    protected override void DoReset(in BenchContext ctx) => _child.Reset(in ctx);
}

public sealed class NaiveWait : NaiveNode
{
    private readonly long _durationMs;
    private bool _started;
    private long _stamp;

    public NaiveWait(TimeSpan duration)
    {
        _durationMs = (long)duration.TotalMilliseconds;
    }

    protected override TickResult Update(in BenchContext ctx)
    {
        var now = ctx.NowMs;

        if (!_started)
        {
            _started = true;
            _stamp = now;
        }

        return (now - _stamp) >= _durationMs ? TickResult.Success : TickResult.Running;
    }

    protected override void OnTerminate(in BenchContext ctx) => _started = false;

    protected override void DoReset(in BenchContext ctx) => _started = false;
}

public sealed class NaiveCondition : NaiveNode
{
    private readonly LeafPredicate<BenchContext> _predicate;

    public NaiveCondition(LeafPredicate<BenchContext> predicate)
    {
        _predicate = predicate;
    }

    protected override TickResult Update(in BenchContext ctx) =>
        _predicate(in ctx) ? TickResult.Success : TickResult.Failure;
}

public sealed class NaiveDo : NaiveNode
{
    private readonly LeafAction<BenchContext> _action;

    public NaiveDo(LeafAction<BenchContext> action)
    {
        _action = action;
    }

    protected override TickResult Update(in BenchContext ctx) => _action(in ctx);
}

/// <summary>Classic-layout counterparts of the <see cref="BenchTrees"/> shapes.</summary>
public static class NaiveTrees
{
    /// <summary>Node-for-node counterpart of <see cref="BenchTrees.Boss"/>.</summary>
    public static NaiveNode Boss() =>
        new NaivePrioritySelector(
            new NaiveSequence(
                new NaiveCondition(BenchTrees.Periodic3),
                new NaiveCooldown(TimeSpan.FromSeconds(30), new NaiveDo(BenchTrees.Succeed))
            ),
            new NaiveSequence(
                new NaiveCondition(BenchTrees.True),
                new NaiveWait(TimeSpan.FromMilliseconds(500)),
                new NaiveDo(BenchTrees.Succeed)
            ),
            new NaiveDo(BenchTrees.Succeed)
        );
}
