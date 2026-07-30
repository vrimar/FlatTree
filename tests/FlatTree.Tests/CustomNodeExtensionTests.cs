namespace FlatTree.Tests;

/// <summary>
/// A custom node with value semantics. Duplicate detection in <c>Build</c> must key on reference
/// identity, or two distinct instances that compare equal are rejected as a shared instance.
/// </summary>
public sealed class ValueEqualityLeaf : LeafNode<FakeClock>
{
    private readonly string _key;

    public ValueEqualityLeaf(string key)
        : base(key) => _key = key;

    protected override TickResult Update(Span<NodeState> s, in FakeClock ctx) => TickResult.Success;

    public override bool Equals(object? obj) => obj is ValueEqualityLeaf other && other._key == _key;

    public override int GetHashCode() => _key.GetHashCode(StringComparison.Ordinal);
}

/// <summary>
/// A user-defined composite living in a DIFFERENT assembly than FlatTree. It exists to prove
/// F8: the builder-traversal hooks are <c>protected internal</c>, so an external composite's
/// children participate in <c>Build</c> (get ids) and tick correctly.
/// </summary>
public sealed class AllSucceed<TContext> : BtNode<TContext>
    where TContext : IClock
{
    private readonly BtNode<TContext>[] _children;

    public AllSucceed(string name, params BtNode<TContext>[] children)
        : base(name)
    {
        _children = children;
    }

    protected override TickResult Update(Span<NodeState> s, in TContext ctx)
    {
        var allSuccess = true;
        foreach (var child in _children)
        {
            var result = child.Tick(s, in ctx);
            if (result == TickResult.Failure)
            {
                return TickResult.Failure;
            }

            if (result != TickResult.Success)
            {
                allSuccess = false;
            }
        }

        return allSuccess ? TickResult.Success : TickResult.Running;
    }

    protected override int ChildCount => _children.Length;

    protected override BtNode<TContext> GetChildForBuild(int index) => _children[index];
}

public sealed class CustomNodeExtensionTests
{
    [Test]
    public void CustomComposite_ChildrenParticipateInBuild_AndTick()
    {
        var n = Bt.For<FakeClock>();
        var a = n.Do("a", static (in FakeClock _) => TickResult.Success);
        var b = n.Do("b", static (in FakeClock _) => TickResult.Success);
        var root = new AllSucceed<FakeClock>("all", a, b);

        var tree = n.Build(root);

        // Children were discovered via the (now protected internal) traversal hooks.
        root.Id.ShouldBe(0);
        a.Id.ShouldBe(1);
        b.Id.ShouldBe(2);
        tree.NodeCount.ShouldBe(3);

        tree.Tick(tree.NewState(), new FakeClock()).ShouldBe(TickResult.Success);
    }

    [Test]
    public void CustomComposite_ShortCircuitsOnFailure()
    {
        var n = Bt.For<FakeClock>();
        var root = new AllSucceed<FakeClock>(
            "all",
            n.Do("a", static (in FakeClock _) => TickResult.Success),
            n.Do("b", static (in FakeClock _) => TickResult.Failure)
        );
        var tree = n.Build(root);

        tree.Tick(tree.NewState(), new FakeClock()).ShouldBe(TickResult.Failure);
    }

    [Test]
    public void CustomNodesWithValueEquality_AreDistinguishedByReference()
    {
        var n = Bt.For<FakeClock>();
        var a = new ValueEqualityLeaf("same");
        var b = new ValueEqualityLeaf("same");

        a.Equals(b).ShouldBeTrue();
        ReferenceEquals(a, b).ShouldBeFalse();

        var tree = n.Build(n.Sequence("root", a, b));

        tree.NodeCount.ShouldBe(3);
        a.Id.ShouldNotBe(b.Id);
        tree.Tick(tree.NewState(), new FakeClock()).ShouldBe(TickResult.Success);
    }

    [Test]
    public void ATrulySharedInstanceIsStillRejected()
    {
        var n = Bt.For<FakeClock>();
        var shared = new ValueEqualityLeaf("shared");

        var error = Should.Throw<InvalidOperationException>(() =>
            n.Build(n.Sequence("root", shared, shared))
        );

        error.Message.ShouldContain("appears more than once");
    }
}
