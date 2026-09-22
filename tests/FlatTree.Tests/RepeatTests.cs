namespace FlatTree.Tests;

public sealed class RepeatTests
{
    [Test]
    public void WhileRepeatCountNotReached_ReturnRunning()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode child = new MockNode { ReturnStatus = TickResult.Success };
        Repeat<FakeClock> sut = n.Repeat("Repeat", 10, child);
        Harness h = new Harness(n, sut);

        for (int i = 0; i < 9; i++)
        {
            h.Tick().ShouldBe(TickResult.Running);
            h.CursorOf(sut).ShouldBe(i + 1);
            child.TerminateCallCount.ShouldBe(i + 1);
        }
    }

    [Test]
    public void WhenRepeatCountIsReached_ReturnSuccessAndResetCounter()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode child = new MockNode { ReturnStatus = TickResult.Success };
        Repeat<FakeClock> sut = n.Repeat("Repeat", 10, child);
        Harness h = new Harness(n, sut);

        var status = TickResult.Running;
        for (int i = 0; i < 10; i++)
        {
            status = h.Tick();
        }

        status.ShouldBe(TickResult.Success);
        h.CursorOf(sut).ShouldBe(0);

        h.Tick().ShouldBe(TickResult.Running);
        h.CursorOf(sut).ShouldBe(1);
    }

    [Test]
    public void WhenChildReturnsFailure_ReturnFailureAndResetCounter()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode child = new MockNode { ReturnStatus = TickResult.Success };
        Repeat<FakeClock> sut = n.Repeat("Repeat", 10, child);
        Harness h = new Harness(n, sut);

        h.Tick();
        child.ReturnStatus = TickResult.Failure;
        h.Tick().ShouldBe(TickResult.Failure);

        h.CursorOf(sut).ShouldBe(0);
    }

    [Test]
    public void WhenChildReturnsRunning_ReturnRunning()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode child = new MockNode { ReturnStatus = TickResult.Running };
        Repeat<FakeClock> sut = n.Repeat("Repeat", 10, child);
        Harness h = new Harness(n, sut);

        for (int i = 0; i < 10; i++)
        {
            h.Tick().ShouldBe(TickResult.Running);
            child.TerminateCallCount.ShouldBe(0);
            h.CursorOf(sut).ShouldBe(0);
        }
    }

    [Test]
    public void WhenResettingWhileRunning_ReInitializeCounter()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode child = new MockNode { ReturnStatus = TickResult.Running };
        Repeat<FakeClock> sut = n.Repeat("Repeat", 15, child);
        Harness h = new Harness(n, sut);

        h.Tick();
        h.Tick();
        h.Tick();

        h.ResetTree();

        h.CursorOf(sut).ShouldBe(0);
    }

    [Test]
    public void ChildIsFreshBetweenIterations()
    {
        BtFactory<FakeClock> n = Bt.For<FakeClock>();
        MockNode child = new MockNode { ReturnStatus = TickResult.Success };
        Repeat<FakeClock> sut = n.Repeat("Repeat", 5, child);
        Harness h = new Harness(n, sut);

        // Each of the first 4 successes resets the child (so it re-initializes each time).
        for (int i = 0; i < 4; i++)
        {
            h.Tick().ShouldBe(TickResult.Running);
        }

        child.InitializeCallCount.ShouldBe(4);
        h.Tick().ShouldBe(TickResult.Success);
        child.UpdateCallCount.ShouldBe(5);
    }

    [Test]
    public void ExitWhenEndsTheLoopAtTheFirstSuccessItHoldsAt()
    {
        Agent agent = new Agent();
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        Repeat<AgentContext> sut = n.Repeat("laps", 10, n.Do("lap", CountLap), Finishing);
        BehaviourTree<AgentContext> tree = n.Build(sut);
        NodeState[] state = tree.NewState();

        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Running);

        agent.Finishing = true;
        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Success);

        agent.Count.ShouldBe(2);
        state[sut.Id].Cursor.ShouldBe(0);
    }

    [Test]
    public void ExitWhenNeverTearsDownARunningChild()
    {
        Agent agent = new Agent { Finishing = true };
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        BehaviourTree<AgentContext> tree = n.Build(
            n.Repeat(10, n.Do("lap", static (in AgentContext _) => TickResult.Running), Finishing)
        );

        tree.Tick(tree.NewState(), new AgentContext(agent)).ShouldBe(TickResult.Running);
    }

    [Test]
    public void ExitWhenStillLetsAFailureThrough()
    {
        Agent agent = new Agent { Finishing = true };
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        BehaviourTree<AgentContext> tree = n.Build(
            n.Repeat(10, n.Do("lap", static (in AgentContext _) => TickResult.Failure), Finishing)
        );

        tree.Tick(tree.NewState(), new AgentContext(agent)).ShouldBe(TickResult.Failure);
    }

    [Test]
    public void ExitWhenSeesTheContextAfterTheChildResetAsForeverDoes()
    {
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        BehaviourTree<AgentContext> repeat = n.Build(n.Repeat(10, new FlagsOnReset(), Finishing));
        BehaviourTree<AgentContext> forever = n.Build(n.Forever(new FlagsOnReset(), Finishing));
        AgentContext repeatAgent = new AgentContext(new Agent());
        AgentContext foreverAgent = new AgentContext(new Agent());

        repeat.Tick(repeat.NewState(), repeatAgent).ShouldBe(TickResult.Success);
        forever.Tick(forever.NewState(), foreverAgent).ShouldBe(TickResult.Success);
    }

    [Test]
    public void ACapturingExitWhenIsRejected()
    {
        bool done = false;
        BtFactory<AgentContext> n = Bt.For<AgentContext>();

        Should.Throw<ArgumentException>(() =>
            n.Repeat(10, n.Do("lap", CountLap), (in AgentContext _) => done)
        );
    }

    private static bool Finishing(in AgentContext ctx) => ctx.Agent.Finishing;

    private static TickResult CountLap(in AgentContext ctx)
    {
        ctx.Agent.Count++;
        return TickResult.Success;
    }

    private sealed class FlagsOnReset() : LeafNode<AgentContext>("flags-on-reset")
    {
        protected override TickResult Update(Span<NodeState> s, in AgentContext ctx) =>
            TickResult.Success;

        protected override void DoReset(Span<NodeState> s, in AgentContext ctx) =>
            ctx.Agent.Finishing = true;
    }
}
