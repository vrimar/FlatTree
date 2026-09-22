namespace FlatTree.Tests;

public sealed class WhileTests
{
    [Test]
    public void AFalseConditionSucceedsWithoutTickingTheBody()
    {
        Agent agent = new Agent();
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        BehaviourTree<AgentContext> tree = n.Build(
            n.While("clear", HasLeft, n.Do("take", Take), 4)
        );

        tree.Tick(tree.NewState(), new AgentContext(agent)).ShouldBe(TickResult.Success);
        agent.Visited.ShouldBeEmpty();
    }

    [Test]
    public void InstantIterationsRunBackToBackUntilTheConditionTurnsFalse()
    {
        Agent agent = new Agent { Count = 3 };
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        BehaviourTree<AgentContext> tree = n.Build(n.While(HasLeft, n.Do("take", Take), 4));

        tree.Tick(tree.NewState(), new AgentContext(agent)).ShouldBe(TickResult.Success);
        agent.Visited.ShouldBe([3, 2, 1]);
    }

    [Test]
    public void SpendingTheBudgetWithTheConditionStillTrueFails()
    {
        Agent agent = new Agent { Count = 10 };
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        While<AgentContext> sut = n.While(HasLeft, n.Do("take", Take), 4);
        BehaviourTree<AgentContext> tree = n.Build(sut);
        NodeState[] state = tree.NewState();

        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Failure);
        agent.Visited.ShouldBe([10, 9, 8, 7]);
        state[sut.Id].Cursor.ShouldBe(0);
    }

    [Test]
    public void TheBudgetCapsOneTickNotTheWholeLoop()
    {
        Agent agent = new Agent { Count = 1 };
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        While<AgentContext> sut = n.While(HasLeft, n.Do("take", TakeWhenFinishing), 2);
        BehaviourTree<AgentContext> tree = n.Build(sut);
        NodeState[] state = tree.NewState();

        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Running);

        for (int lap = 1; lap <= 4; lap++)
        {
            agent.Finishing = true;
            tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Running);
            state[sut.Id].Cursor.ShouldBe(lap);
        }

        agent.Count = 0;
        agent.Finishing = true;
        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Success);
        agent.Visited.Count.ShouldBe(5);
    }

    [Test]
    public void AFailingBodyFailsTheLoop()
    {
        Agent agent = new Agent { Count = 3 };
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        BehaviourTree<AgentContext> tree = n.Build(
            n.While(HasLeft, n.Do("take", static (in AgentContext _) => TickResult.Failure), 4)
        );

        tree.Tick(tree.NewState(), new AgentContext(agent)).ShouldBe(TickResult.Failure);
    }

    [Test]
    public void ARunningBodyResumesWithoutRecheckingTheCondition()
    {
        Agent agent = new Agent { Count = 1 };
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        While<AgentContext> sut = n.While(HasLeft, n.Do("take", TakeWhenFinishing), 4);
        BehaviourTree<AgentContext> tree = n.Build(sut);
        NodeState[] state = tree.NewState();

        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Running);

        agent.Count = 0;
        agent.Finishing = true;
        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Success);

        agent.Visited.ShouldBe([0]);
    }

    [Test]
    public void AResetClearsTheIterationCount()
    {
        Agent agent = new Agent { Count = 5, Finishing = true };
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        While<AgentContext> sut = n.While(HasLeft, n.Do("take", TakeWhenFinishing), 4);
        BehaviourTree<AgentContext> tree = n.Build(sut);
        NodeState[] state = tree.NewState();

        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Running);
        state[sut.Id].Cursor.ShouldBe(1);

        tree.Reset(state, new AgentContext(agent));

        state[sut.Id].Cursor.ShouldBe(0);
    }

    [Test]
    public void AResetMidBodyReturnsTheBodyToFresh()
    {
        Agent agent = new Agent { Count = 1 };
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        Do<AgentContext> body = n.Do("take", TakeWhenFinishing);
        While<AgentContext> sut = n.While(HasLeft, body, 4);
        BehaviourTree<AgentContext> tree = n.Build(sut);
        NodeState[] state = tree.NewState();

        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Running);
        state[body.Id].Status.ShouldBe(NodeStatus.Running);

        tree.Reset(state, new AgentContext(agent));

        state[body.Id].Status.ShouldBe(NodeStatus.Fresh);
        state[sut.Id].Status.ShouldBe(NodeStatus.Fresh);
    }

    [Test]
    public void ANonPositiveBudgetIsRejected()
    {
        BtFactory<AgentContext> n = Bt.For<AgentContext>();

        Should.Throw<ArgumentOutOfRangeException>(() => n.While(HasLeft, n.Do("take", Take), 0));
    }

    [Test]
    public void ACapturingConditionIsRejected()
    {
        bool more = true;
        BtFactory<AgentContext> n = Bt.For<AgentContext>();

        Should.Throw<ArgumentException>(() =>
            n.While((in AgentContext _) => more, n.Do("take", Take), 4)
        );
    }

    private static bool HasLeft(in AgentContext ctx) => ctx.Agent.Count > 0;

    private static TickResult Take(in AgentContext ctx)
    {
        ctx.Agent.Visited.Add(ctx.Agent.Count--);
        return TickResult.Success;
    }

    private static TickResult TakeWhenFinishing(in AgentContext ctx)
    {
        if (!ctx.Agent.Finishing)
        {
            return TickResult.Running;
        }

        ctx.Agent.Finishing = false;
        ctx.Agent.Visited.Add(ctx.Agent.Count);
        return TickResult.Success;
    }
}
