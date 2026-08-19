namespace FlatTree.Tests;

public sealed class ForEachTests
{
    [Test]
    public void InstantIterationsAllRunInOneTick()
    {
        Agent agent = new Agent { Count = 3 };
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        BehaviourTree<AgentContext> tree = n.Build(
            n.ForEach("each", Count, n.Do("body", Visit), Publish)
        );

        tree.Tick(tree.NewState(), new AgentContext(agent)).ShouldBe(TickResult.Success);
        agent.Visited.ShouldBe([0, 1, 2]);
    }

    [Test]
    public void ARunningBodyParksTheCursorAndResumesNextTick()
    {
        Agent agent = new Agent { Count = 2 };
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        MockLeaf body = new MockLeaf { ReturnStatus = TickResult.Running };
        ForEach<AgentContext> sut = n.ForEach(Count, body, Publish);
        BehaviourTree<AgentContext> tree = n.Build(sut);
        NodeState[] state = tree.NewState();

        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Running);
        state[sut.Id].Cursor.ShouldBe(0);

        body.ReturnStatus = TickResult.Success;
        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Success);
    }

    [Test]
    public void AFailingBodyFailsTheLoop()
    {
        Agent agent = new Agent { Count = 5 };
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        ForEach<AgentContext> sut = n.ForEach(Count, n.Do("body", FailOnSecond), Publish);
        BehaviourTree<AgentContext> tree = n.Build(sut);

        tree.Tick(tree.NewState(), new AgentContext(agent)).ShouldBe(TickResult.Failure);
        agent.Visited.ShouldBe([0, 1]);
    }

    [Test]
    public void ACountOfZeroSucceedsWithoutTickingTheBody()
    {
        Agent agent = new Agent { Count = 0 };
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        BehaviourTree<AgentContext> tree = n.Build(n.ForEach(Count, n.Do("body", Visit)));

        tree.Tick(tree.NewState(), new AgentContext(agent)).ShouldBe(TickResult.Success);
        agent.Visited.ShouldBeEmpty();
    }

    [Test]
    public void CompletingClearsTheCursor()
    {
        Agent agent = new Agent { Count = 2 };
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        ForEach<AgentContext> sut = n.ForEach(Count, n.Do("body", Visit));
        BehaviourTree<AgentContext> tree = n.Build(sut);
        NodeState[] state = tree.NewState();

        tree.Tick(state, new AgentContext(agent));

        state[sut.Id].Cursor.ShouldBe(0);
    }

    [Test]
    public void ACapturingCountIsRejected()
    {
        int count = 3;
        BtFactory<AgentContext> n = Bt.For<AgentContext>();

        Should.Throw<ArgumentException>(() =>
            n.ForEach((in AgentContext _) => count, n.Do("body", Visit))
        );
    }

    private static int Count(in AgentContext ctx) => ctx.Agent.Count;

    private static void Publish(in AgentContext ctx, int index) => ctx.Agent.Visited.Add(index);

    private static TickResult Visit(in AgentContext ctx) => TickResult.Success;

    private static TickResult FailOnSecond(in AgentContext ctx) =>
        ctx.Agent.Visited.Count >= 2 ? TickResult.Failure : TickResult.Success;

    private sealed class MockLeaf : LeafNode<AgentContext>
    {
        public MockLeaf()
            : base("body") { }

        public TickResult ReturnStatus { get; set; } = TickResult.Success;

        protected override TickResult Update(Span<NodeState> s, in AgentContext ctx) =>
            ReturnStatus;
    }
}
