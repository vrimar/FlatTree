namespace FlatTree;

/// <summary>Base for leaf nodes (no children). Exists for organizational symmetry.</summary>
public abstract class LeafNode<TContext> : BtNode<TContext>
    where TContext : IClock
{
    protected LeafNode(string name)
        : base(name) { }
}
