namespace FlatTree;

/// <summary>
/// Base for decorators (single-child nodes): centralizes the <see cref="Child"/> plumbing
/// and the recursive reset. A plain decorator does NOT reset its child on completion (only on
/// an explicit <see cref="BtNode{TContext}.Reset"/>); decorators that need reset-on-terminate
/// override <see cref="BtNode{TContext}.OnTerminate"/>.
/// </summary>
public abstract class DecoratorNode<TContext> : BtNode<TContext>
    where TContext : IClock
{
    /// <summary>The single decorated child.</summary>
    public BtNode<TContext> Child { get; }

    protected DecoratorNode(string name, BtNode<TContext> child)
        : base(name)
    {
        ArgumentNullException.ThrowIfNull(child);
        Child = child;
    }

    protected override void DoReset(Span<NodeState> s)
    {
        Child.Reset(s);
    }

    protected internal override int ChildCount => 1;

    protected internal override BtNode<TContext> GetChildForBuild(int index) => Child;
}
