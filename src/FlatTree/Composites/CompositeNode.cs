namespace FlatTree;

/// <summary>
/// Base for composites: centralizes the shared <see cref="Children"/> array plumbing and the
/// "reset all children" loop. On completion a composite resets itself (so re-entry starts
/// fresh) via <see cref="OnTerminate"/> → <see cref="DoReset"/>.
/// </summary>
public abstract class CompositeNode<TContext> : BtNode<TContext>
    where TContext : IClock
{
    /// <summary>
    /// Upper bound on children for shuffled and weighted traversal. <see cref="TickShuffled"/> and
    /// <see cref="WeightedSelector{TContext}"/> hold their <c>stackalloc</c> visit order live across
    /// the recursive child ticks, so nesting accumulates stack; the bound keeps that from
    /// overflowing.
    /// </summary>
    public const int MaxShuffledChildren = 64;

    // Derived composites tick this directly: an array local keeps the JIT's bounds-check elision,
    // which indexing through the Children span in a hot loop would not.
    private protected readonly BtNode<TContext>[] _children;

    /// <summary>
    /// The composite's children, in declaration order. Read-only, and the constructor copies the
    /// array it is given, so a built tree cannot be reshaped through either the caller's array or
    /// this property — which would otherwise give two nodes the same per-agent state slot.
    /// </summary>
    public ReadOnlySpan<BtNode<TContext>> Children => _children;

    protected CompositeNode(string name, BtNode<TContext>[] children)
        : base(name)
    {
        ArgumentNullException.ThrowIfNull(children);

        if (children.Length == 0)
        {
            throw new ArgumentException(
                "A composite must have at least one child.",
                nameof(children)
            );
        }

        // Copy, or the validation below only ever held for a snapshot the caller can still edit.
        // Exactly typed, so a covariant argument (Derived[] as BtNode[]) cannot alias in either.
        _children = new BtNode<TContext>[children.Length];

        for (int i = 0; i < children.Length; i++)
        {
            _children[i] =
                children[i]
                ?? throw new ArgumentException(
                    "Children cannot contain null elements.",
                    nameof(children)
                );
        }
    }

    /// <summary>
    /// Enforces <see cref="MaxShuffledChildren"/>. Call from the constructor of a composite that
    /// holds a <c>stackalloc</c> visit order across its child ticks, as <see cref="TickShuffled"/>
    /// does.
    /// </summary>
    protected void RequireShuffleableChildCount(string paramName)
    {
        if (_children.Length > MaxShuffledChildren)
        {
            throw new ArgumentException(
                $"A shuffled composite supports at most {MaxShuffledChildren} children "
                    + $"(got {_children.Length}).",
                paramName
            );
        }
    }

    protected void ResetChildren(Span<NodeState> s, in TContext ctx)
    {
        var children = _children;
        for (var i = 0; i < children.Length; i++)
        {
            children[i].Reset(s, in ctx);
        }
    }

    /// <summary>
    /// Ticks children left-to-right, resuming from <c>Cursor</c>; returns the first child
    /// status that is not <paramref name="continueOn"/>, or <paramref name="continueOn"/> when
    /// every child reported it. Shared by <see cref="Selector{TContext}"/> (continue on
    /// Failure) and <see cref="Sequence{TContext}"/> (continue on Success).
    /// </summary>
    protected TickResult TickSequential(Span<NodeState> s, in TContext ctx, TickResult continueOn)
    {
        ref var st = ref s[Id];
        var children = _children;

        do
        {
            var childStatus = children[st.Cursor].Tick(s, in ctx);

            if (childStatus != continueOn)
            {
                return childStatus;
            }
        } while (++st.Cursor < children.Length);

        return continueOn;
    }

    /// <summary>
    /// Like <see cref="TickSequential"/> but visits children in a per-agent shuffled order.
    /// <c>Stamp</c> holds the shuffle seed (drawn lazily on first tick) and <c>Cursor</c> is the
    /// visitation index into the permutation reproduced each tick from the seed (so a running
    /// child resumes at the same position). Shared by <c>RandomSelector</c>/<c>RandomSequence</c>.
    /// </summary>
    protected TickResult TickShuffled(
        Span<NodeState> s,
        in TContext ctx,
        TickResult continueOn,
        IRandomProvider randomProvider
    )
    {
        Span<int> permutation = stackalloc int[_children.Length];
        SeededRng(s, randomProvider).FillShuffled(permutation);
        return TickInOrder(s, in ctx, continueOn, permutation);
    }

    private protected DeterministicRng SeededRng(Span<NodeState> s, IRandomProvider randomProvider)
    {
        ref var st = ref s[Id];

        if (st.Stamp == 0)
        {
            st.Stamp = DeterministicRng.DrawNonZeroSeed(randomProvider);
        }

        return new DeterministicRng(st.Stamp);
    }

    private protected TickResult TickInOrder(
        Span<NodeState> s,
        in TContext ctx,
        TickResult continueOn,
        ReadOnlySpan<int> order
    )
    {
        ref var st = ref s[Id];
        var children = _children;

        do
        {
            var childStatus = children[order[st.Cursor]].Tick(s, in ctx);

            if (childStatus != continueOn)
            {
                return childStatus;
            }
        } while (++st.Cursor < order.Length);

        return continueOn;
    }

    protected override void OnTerminate(Span<NodeState> s, TickResult status, in TContext ctx)
    {
        DoReset(s, in ctx);
    }

    protected override void DoReset(Span<NodeState> s, in TContext ctx)
    {
        ResetChildren(s, in ctx);
    }

    protected internal override int ChildCount => _children.Length;

    protected internal override BtNode<TContext> GetChildForBuild(int index) => _children[index];
}
