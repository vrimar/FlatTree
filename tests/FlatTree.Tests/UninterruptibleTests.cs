namespace FlatTree.Tests;

/// <summary>
/// Build rejects a subtree marked <see cref="BtNode{TContext}.Uninterruptible"/> when a reactive
/// parent could reset it mid-flight. The failure it prevents is silent, so it must be a
/// build-time error rather than a convention.
/// </summary>
public sealed class UninterruptibleTests
{
    private static TickResult Succeed(in FakeClock c) => TickResult.Success;

    [Test]
    public void Build_RejectsAnUninterruptibleNodeDirectlyUnderAPrioritySelector()
    {
        var n = Bt.For<FakeClock>();

        var ex = Should.Throw<InvalidOperationException>(() =>
            n.Build(
                n.PrioritySelector(
                    "root",
                    n.Do("high", Succeed),
                    n.Uninterruptible(n.Do("commit", Succeed))
                )
            )
        );

        ex.Message.ShouldContain("commit");
        ex.Message.ShouldContain("root");
    }

    [Test]
    public void Build_RejectsAnUninterruptibleNodeNestedDeepUnderAPrioritySelector()
    {
        var n = Bt.For<FakeClock>();

        Should.Throw<InvalidOperationException>(() =>
            n.Build(
                n.PrioritySelector(
                    "root",
                    n.Sequence(
                        "branch",
                        n.AlwaysSucceed("wrap", n.Uninterruptible(n.Do("commit", Succeed)))
                    )
                )
            )
        );
    }

    [Test]
    public void Build_RejectsAnUninterruptibleNodeUnderAPrioritySequence()
    {
        var n = Bt.For<FakeClock>();

        Should.Throw<InvalidOperationException>(() =>
            n.Build(
                n.PrioritySequence("root", n.Uninterruptible(n.Sequence("commit", n.Do(Succeed))))
            )
        );
    }

    [Test]
    public void Build_RejectsAnUninterruptibleCompositeAndReportsTheReactiveAncestor()
    {
        var n = Bt.For<FakeClock>();
        var commit = n.Uninterruptible(n.Sequence("commit", n.Do("step", Succeed)));

        var ex = Should.Throw<InvalidOperationException>(() =>
            n.Build(n.PrioritySelector("reactive-root", n.Selector("plain", commit)))
        );

        ex.Message.ShouldContain("reactive-root");
        ex.Message.ShouldContain("PrioritySelector");
    }

    [Test]
    public void Build_AcceptsAnUninterruptibleNodeOutsideAnyReactiveSubtree()
    {
        var n = Bt.For<FakeClock>();

        var tree = n.Build(
            n.Sequence(
                "root",
                n.Selector("plain", n.Uninterruptible(n.Do("commit", Succeed))),
                n.Do("after", Succeed)
            )
        );

        tree.NodeCount.ShouldBe(4);
    }

    [Test]
    public void Build_AcceptsAReactiveNodeNestedInsideAnUninterruptibleSubtree()
    {
        var n = Bt.For<FakeClock>();

        var tree = n.Build(
            n.Uninterruptible(
                n.Sequence("commit", n.PrioritySelector("inner", n.Do("pick", Succeed)))
            )
        );

        tree.Root.Uninterruptible.ShouldBeTrue();
    }

    [Test]
    public void Uninterruptible_DefaultsToFalseAndIsSetByTheMarker()
    {
        var n = Bt.For<FakeClock>();
        var plain = n.Do("plain", Succeed);
        var marked = n.Uninterruptible(n.Do("marked", Succeed));

        plain.Uninterruptible.ShouldBeFalse();
        marked.Uninterruptible.ShouldBeTrue();
    }

    [Test]
    public void Uninterruptible_ReturnsTheSameInstanceSoItComposesInline()
    {
        var n = Bt.For<FakeClock>();
        var node = n.Do("x", Succeed);

        n.Uninterruptible(node).ShouldBeSameAs(node);
    }

    [Test]
    public void MarkedTree_StillTicksNormally()
    {
        var n = Bt.For<FakeClock>();
        var harness = new Harness(
            n,
            n.Sequence("root", n.Uninterruptible(n.Do("commit", Succeed)))
        );

        harness.Tick().ShouldBe(TickResult.Success);
    }

    [Test]
    public void RejectsAnUninterruptibleNodeUnderSimpleParallel()
    {
        var n = Bt.For<FakeClock>();

        var error = Should.Throw<InvalidOperationException>(() =>
            n.Build(
                n.SimpleParallel(
                    "parallel",
                    SimpleParallelPolicy.BothMustSucceed,
                    n.Do("other", Succeed),
                    n.Uninterruptible(n.Do("commit", Succeed))
                )
            )
        );

        error.Message.ShouldContain("commit");
        error.Message.ShouldContain("parallel");
    }

    [Test]
    public void RejectsAnUninterruptibleNodeUnderTimeLimit()
    {
        var n = Bt.For<FakeClock>();

        var error = Should.Throw<InvalidOperationException>(() =>
            n.Build(
                n.TimeLimit(
                    "deadline",
                    TimeSpan.FromMilliseconds(500),
                    n.Uninterruptible(n.Do("commit", Succeed))
                )
            )
        );

        error.Message.ShouldContain("commit");
        error.Message.ShouldContain("deadline");
    }

    [Test]
    public void HonoursACustomNodeThatOptsIntoPreemptingRunningChildren()
    {
        var n = Bt.For<FakeClock>();

        var error = Should.Throw<InvalidOperationException>(() =>
            n.Build(new PreemptingDecorator(n.Uninterruptible(n.Do("commit", Succeed))))
        );

        error.Message.ShouldContain("commit");
        error.Message.ShouldContain("preempting");
    }

    [Test]
    public void ACustomNodeThatDoesNotPreempt_IsNotTreatedAsReactive()
    {
        var n = Bt.For<FakeClock>();

        Should.NotThrow(() =>
            n.Build(new PassthroughDecorator(n.Uninterruptible(n.Do("commit", Succeed))))
        );
    }

    private sealed class PreemptingDecorator : DecoratorNode<FakeClock>
    {
        public PreemptingDecorator(BtNode<FakeClock> child)
            : base("preempting", child) { }

        protected override TickResult Update(Span<NodeState> s, in FakeClock ctx) =>
            Child.Tick(s, in ctx);

        protected override bool PreemptsRunningChildren => true;
    }

    private sealed class PassthroughDecorator : DecoratorNode<FakeClock>
    {
        public PassthroughDecorator(BtNode<FakeClock> child)
            : base("passthrough", child) { }

        protected override TickResult Update(Span<NodeState> s, in FakeClock ctx) =>
            Child.Tick(s, in ctx);
    }
}
