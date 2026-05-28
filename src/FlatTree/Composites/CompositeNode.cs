namespace FlatTree;

/// <summary>
/// Base for composites: centralizes the shared <see cref="Children"/> array plumbing and the
/// "reset all children" loop. On completion a composite resets itself (so re-entry starts
/// fresh) via <see cref="OnTerminate"/> → <see cref="DoReset"/>.
/// </summary>
public abstract class CompositeNode<TContext> : BtNode<TContext>
    where TContext : IClock
{
    /// <summary>The composite's children, in declaration order.</summary>
    public BtNode<TContext>[] Children { get; }

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

        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] is null)
            {
                throw new ArgumentException(
                    "Children cannot contain null elements.",
                    nameof(children)
                );
            }
        }

        Children = children;
    }

    protected void ResetChildren(Span<NodeState> s)
    {
        var children = Children;
        for (var i = 0; i < children.Length; i++)
        {
            children[i].Reset(s);
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
        var children = Children;

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
        ref var st = ref s[Id];
        var children = Children;
        var n = children.Length;

        if (st.Stamp == 0)
        {
            st.Stamp = DeterministicRng.DrawNonZeroSeed(randomProvider);
        }

        Span<int> permutation = stackalloc int[n];
        new DeterministicRng(st.Stamp).FillShuffled(permutation);

        do
        {
            var childStatus = children[permutation[st.Cursor]].Tick(s, in ctx);

            if (childStatus != continueOn)
            {
                return childStatus;
            }
        } while (++st.Cursor < n);

        return continueOn;
    }

    protected override void OnTerminate(Span<NodeState> s, TickResult status)
    {
        DoReset(s);
    }

    protected override void DoReset(Span<NodeState> s)
    {
        ResetChildren(s);
    }

    protected internal override int ChildCount => Children.Length;

    protected internal override BtNode<TContext> GetChildForBuild(int index) => Children[index];
}
