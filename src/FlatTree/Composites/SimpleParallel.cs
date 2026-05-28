namespace FlatTree;

/// <summary>
/// Runs all children "in parallel" within a tick. <c>Cursor</c> packs each child's last status
/// into 2 bits (so up to 16 children are supported). On the first tick (or after the node
/// completed last tick) every child is ticked; while the node is running, only a child still
/// <see cref="NodeStatus.Fresh"/>/<see cref="NodeStatus.Running"/> is ticked (a completed child
/// is skipped and its recorded status reused). On completion all children are reset.
/// </summary>
public sealed class SimpleParallel<TContext> : CompositeNode<TContext>
    where TContext : IClock
{
    /// <summary>The maximum number of children (2 status bits per child in a 32-bit cursor).</summary>
    public const int MaxChildren = 16;

    private const int StatusBits = 2;
    private const int StatusMask = 0x3;

    private readonly SimpleParallelPolicy _policy;

    internal SimpleParallel(string name, SimpleParallelPolicy policy, BtNode<TContext>[] children)
        : base(name, children)
    {
        if (children.Length > MaxChildren)
        {
            throw new ArgumentException(
                $"SimpleParallel supports at most {MaxChildren} children.",
                nameof(children)
            );
        }

        _policy = policy;
    }

    /// <summary>The completion policy.</summary>
    public SimpleParallelPolicy Policy => _policy;

    protected override TickResult Update(Span<NodeState> s, in TContext ctx)
    {
        ref var st = ref s[Id];
        var children = Children;
        var n = children.Length;

        // st.Status still holds the PREVIOUS tick's status (Tick writes it after Update).
        var freshStart = st.Status != NodeStatus.Running;
        var prevCursor = st.Cursor;
        var packed = 0;

        for (var i = 0; i < n; i++)
        {
            NodeStatus childStatus;

            if (freshStart)
            {
                childStatus = (NodeStatus)children[i].Tick(s, in ctx);
            }
            else
            {
                var prev = (NodeStatus)((prevCursor >> (i * StatusBits)) & StatusMask);
                childStatus = prev is NodeStatus.Fresh or NodeStatus.Running
                    ? (NodeStatus)children[i].Tick(s, in ctx)
                    : prev;
            }

            packed |= ((int)childStatus & StatusMask) << (i * StatusBits);
        }

        st.Cursor = packed;

        return Evaluate(packed, n);
    }

    private TickResult Evaluate(int packed, int n)
    {
        var anySuccess = false;
        var anyFailure = false;
        var allSuccess = true;
        var allFailure = true;

        for (var i = 0; i < n; i++)
        {
            var childStatus = (NodeStatus)((packed >> (i * StatusBits)) & StatusMask);

            if (childStatus == NodeStatus.Success)
            {
                anySuccess = true;
                allFailure = false;
            }
            else if (childStatus == NodeStatus.Failure)
            {
                anyFailure = true;
                allSuccess = false;
            }
            else
            {
                allSuccess = false;
                allFailure = false;
            }
        }

        if (_policy == SimpleParallelPolicy.BothMustSucceed)
        {
            if (anyFailure)
            {
                return TickResult.Failure;
            }

            return allSuccess ? TickResult.Success : TickResult.Running;
        }

        if (anySuccess)
        {
            return TickResult.Success;
        }

        return allFailure ? TickResult.Failure : TickResult.Running;
    }

    protected override void DoReset(Span<NodeState> s)
    {
        s[Id].Cursor = 0;
        base.DoReset(s);
    }
}
