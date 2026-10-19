namespace FlatTree;

/// <summary>
/// Runs <c>then</c> when the condition succeeds and <c>otherwise</c> when it fails, and finishes with
/// whatever the chosen branch finishes with. The condition is read once, on entry; one that returns
/// Running is ticked again until it settles. A failing branch never falls back to the other, which is
/// what sets this apart from a <see cref="Selector{TContext}"/> over a guarded
/// <see cref="Sequence{TContext}"/>. With no <c>otherwise</c>, a failed condition is a Success that
/// does nothing, so an if inside a sequence never stops it.
/// </summary>
/// <remarks><c>Cursor</c> is the branch: 0 while deciding, then 1 or 2, the child index it runs.</remarks>
public sealed class If<TContext> : CompositeNode<TContext>
    where TContext : IClock
{
    internal If(
        string name,
        BtNode<TContext> condition,
        BtNode<TContext> then,
        BtNode<TContext>? otherwise
    )
        : base(name, IfBranches.Of(condition, then, otherwise)) { }

    protected override TickResult Update(Span<NodeState> s, in TContext ctx)
    {
        ref var st = ref s[Id];
        var children = _children;

        if (st.Cursor == IfBranches.Deciding)
        {
            var verdict = children[IfBranches.Condition].Tick(s, in ctx);

            if (verdict == TickResult.Running)
            {
                return TickResult.Running;
            }

            if (verdict == TickResult.Failure && children.Length == IfBranches.WithoutElse)
            {
                return TickResult.Success;
            }

            st.Cursor = verdict == TickResult.Success ? IfBranches.Then : IfBranches.Else;
        }

        return children[st.Cursor].Tick(s, in ctx);
    }

    protected override void DoReset(Span<NodeState> s, in TContext ctx)
    {
        s[Id].Cursor = IfBranches.Deciding;
        base.DoReset(s, in ctx);
    }

    public override string Describe(in NodeState state) => IfBranches.Describe(state.Cursor);
}

/// <summary>
/// An <see cref="If{TContext}"/> that reads its condition again every tick and runs the branch the
/// answer now picks. When the answer changes, the branch that was running is reset before the other
/// starts, so a running branch can be torn down mid-flight. A condition that returns Running leaves
/// both branches untouched for that tick.
/// </summary>
/// <remarks><c>Cursor</c> is the branch last run: 0 before the first answer, then 1 or 2.</remarks>
public sealed class ReactiveIf<TContext> : CompositeNode<TContext>
    where TContext : IClock
{
    internal ReactiveIf(
        string name,
        BtNode<TContext> condition,
        BtNode<TContext> then,
        BtNode<TContext>? otherwise
    )
        : base(name, IfBranches.Of(condition, then, otherwise)) { }

    protected override TickResult Update(Span<NodeState> s, in TContext ctx)
    {
        ref var st = ref s[Id];
        var children = _children;
        var verdict = children[IfBranches.Condition].Tick(s, in ctx);

        if (verdict == TickResult.Running)
        {
            return TickResult.Running;
        }

        var branch = verdict == TickResult.Success ? IfBranches.Then : IfBranches.Else;

        if (
            st.Cursor != IfBranches.Deciding
            && st.Cursor != branch
            && st.Cursor < children.Length
        )
        {
            children[st.Cursor].Reset(s, in ctx);
        }

        st.Cursor = branch;

        return branch < children.Length ? children[branch].Tick(s, in ctx) : TickResult.Success;
    }

    protected override void DoReset(Span<NodeState> s, in TContext ctx)
    {
        s[Id].Cursor = IfBranches.Deciding;
        base.DoReset(s, in ctx);
    }

    protected internal override bool PreemptsRunningChildren => true;

    public override string Describe(in NodeState state) => IfBranches.Describe(state.Cursor);
}

internal static class IfBranches
{
    public const int Deciding = 0;
    public const int Condition = 0;
    public const int Then = 1;
    public const int Else = 2;
    public const int WithoutElse = 2;

    public static BtNode<TContext>[] Of<TContext>(
        BtNode<TContext> condition,
        BtNode<TContext> then,
        BtNode<TContext>? otherwise
    )
        where TContext : IClock => otherwise is null ? [condition, then] : [condition, then, otherwise];

    public static string Describe(int cursor) =>
        cursor switch
        {
            Then => "then",
            Else => "else",
            _ => "",
        };
}
