namespace FlatTree;

/// <summary>
/// A selector whose children are visited in a per-agent weighted order: each position draws one of
/// the children not yet visited with probability proportional to its weight, so a child weighted 3
/// is tried first three times as often as one weighted 1. Returns the first
/// non-<see cref="TickResult.Failure"/> status, or Failure if all fail, so a drawn child that fails
/// falls through to the next draw. <c>Stamp</c> holds the seed and <c>Cursor</c> is the visitation
/// index into the order reproduced from it each <c>Update</c> into a <c>stackalloc</c> span (no
/// heap), so a running child resumes at the same position. <c>DoReset</c> clears the seed, so the
/// next activation draws a new one and visits in a new order. Lazy draw: <c>Stamp == 0</c> means
/// "unseeded" — a non-zero seed is drawn and stored on the next tick. The draw uses integer
/// arithmetic only, so a seed yields the same order on every platform.
/// </summary>
public sealed class WeightedSelector<TContext> : CompositeNode<TContext>
    where TContext : IClock
{
    private readonly int[] _weights;
    private readonly int _totalWeight;
    private readonly RandomSource<TContext> _randomSource;

    internal WeightedSelector(
        string name,
        (int Weight, BtNode<TContext> Child)[] children,
        RandomSource<TContext> randomSource
    )
        : base(name, ChildrenOf(children))
    {
        RequireShuffleableChildCount(nameof(children));

        _weights = new int[children.Length];
        long total = 0;

        for (var i = 0; i < children.Length; i++)
        {
            var weight = children[i].Weight;

            if (weight <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(children),
                    weight,
                    $"Every weight must be positive (child {i})."
                );
            }

            _weights[i] = weight;
            total += weight;
        }

        if (total > int.MaxValue)
        {
            throw new ArgumentException(
                $"The weights must sum to at most {int.MaxValue} (got {total}).",
                nameof(children)
            );
        }

        _totalWeight = (int)total;
        _randomSource = randomSource;
    }

    /// <summary>
    /// The weight of each child, parallel to <see cref="CompositeNode{TContext}.Children"/>.
    /// </summary>
    public ReadOnlySpan<int> Weights => _weights;

    protected override TickResult Update(Span<NodeState> s, in TContext ctx)
    {
        Span<int> order = stackalloc int[_weights.Length];
        SeededRng(s, _randomSource(in ctx)).FillWeighted(_weights, _totalWeight, order);
        return TickInOrder(s, in ctx, TickResult.Failure, order);
    }

    // Clear, never re-draw: an ancestor's reset cascade can run DoReset a second time.
    protected override void DoReset(Span<NodeState> s, in TContext ctx)
    {
        ref var st = ref s[Id];
        st.Cursor = 0;
        st.Stamp = 0;
        base.DoReset(s, in ctx);
    }

    private static BtNode<TContext>[] ChildrenOf((int Weight, BtNode<TContext> Child)[] children)
    {
        ArgumentNullException.ThrowIfNull(children);

        var nodes = new BtNode<TContext>[children.Length];
        for (var i = 0; i < children.Length; i++)
        {
            nodes[i] = children[i].Child;
        }

        return nodes;
    }
}
