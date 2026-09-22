namespace FlatTree.Tests;

public sealed class WaitUntilTests
{
    [Test]
    public void AConditionAlreadyTrueSucceedsOnTheActivatingTick()
    {
        Agent agent = new Agent { Finishing = true };
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        BehaviourTree<AgentContext> tree = n.Build(
            n.WaitUntil("done", Finishing, TimeSpan.FromMilliseconds(500))
        );

        tree.Tick(tree.NewState(), new AgentContext(agent)).ShouldBe(TickResult.Success);
    }

    [Test]
    public void RunsUntilTheConditionHolds()
    {
        Agent agent = new Agent();
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        BehaviourTree<AgentContext> tree = n.Build(
            n.WaitUntil(Finishing, TimeSpan.FromMilliseconds(500))
        );
        NodeState[] state = tree.NewState();

        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Running);

        agent.AdvanceSim(499);
        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Running);

        agent.Finishing = true;
        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Success);
    }

    [Test]
    public void WithoutAHandlerTheDeadlineFails()
    {
        Agent agent = new Agent();
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        WaitUntil<AgentContext> sut = n.WaitUntil(Finishing, TimeSpan.FromMilliseconds(500));
        BehaviourTree<AgentContext> tree = n.Build(sut);
        NodeState[] state = tree.NewState();

        tree.Tick(state, new AgentContext(agent));

        agent.AdvanceSim(500);
        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Failure);

        state[sut.Id].Cursor.ShouldBe(0);
        state[sut.Id].Stamp.ShouldBe(0);
    }

    [Test]
    public void TheHandlerRunsOnlyOnTheDeadlineAndDecidesTheStatus()
    {
        Agent agent = new Agent();
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        BehaviourTree<AgentContext> tree = n.Build(
            n.WaitUntil("done", Finishing, TimeSpan.FromMilliseconds(500), RecordAndSucceed)
        );
        NodeState[] state = tree.NewState();

        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Running);
        agent.Faults.ShouldBe(0);

        agent.AdvanceSim(500);
        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Success);
        agent.Faults.ShouldBe(1);
    }

    [Test]
    public void TheStatefulPredicateAndHandlerReceiveTheAuthoredState()
    {
        Agent agent = new Agent();
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        WaitUntil<AgentContext, int> sut = n.WaitUntil(
            "arrive",
            7,
            ReachedSite,
            TimeSpan.FromMilliseconds(500),
            RecordSite
        );
        BehaviourTree<AgentContext> tree = n.Build(sut);
        NodeState[] state = tree.NewState();

        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Running);
        agent.AdvanceSim(500);

        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Failure);
        agent.Visited.ShouldBe([7]);
        sut.State.ShouldBe(7);
    }

    [Test]
    public void TheStatefulPredicateSucceedsOnceItsSiteIsReached()
    {
        Agent agent = new Agent { Count = 3 };
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        BehaviourTree<AgentContext> tree = n.Build(
            n.WaitUntil(7, ReachedSite, TimeSpan.FromMilliseconds(500))
        );
        NodeState[] state = tree.NewState();

        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Running);

        agent.Count = 7;
        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Success);
    }

    [Test]
    public void EveryStatefulOverloadBuildsTheSameLeaf()
    {
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        TimeSpan timeout = TimeSpan.FromMilliseconds(500);

        n.WaitUntil("arrive", 7, ReachedSite, timeout).Name.ShouldBe("arrive");
        n.WaitUntil(7, ReachedSite, timeout).Name.ShouldBe("WaitUntil");
        n.WaitUntil(7, ReachedSite, timeout, RecordSite).Name.ShouldBe("WaitUntil");
        n.WaitUntil(7, ReachedSite, timeout, RecordSite).State.ShouldBe(7);
    }

    [Test]
    public void WithoutAHandlerTheStatefulDeadlineFails()
    {
        Agent agent = new Agent();
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        BehaviourTree<AgentContext> tree = n.Build(
            n.WaitUntil("arrive", 7, ReachedSite, TimeSpan.FromMilliseconds(500))
        );
        NodeState[] state = tree.NewState();

        tree.Tick(state, new AgentContext(agent));
        agent.AdvanceSim(500);

        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Failure);
    }

    [Test]
    public void AHandlerReturningRunningIsRejected()
    {
        Agent agent = new Agent();
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        BehaviourTree<AgentContext> tree = n.Build(
            n.WaitUntil(
                Finishing,
                TimeSpan.FromMilliseconds(500),
                static (in AgentContext _) => TickResult.Running
            )
        );
        NodeState[] state = tree.NewState();

        tree.Tick(state, new AgentContext(agent));
        agent.AdvanceSim(500);

        Should.Throw<InvalidOperationException>(() => tree.Tick(state, new AgentContext(agent)));
    }

    [Test]
    public void AZeroTimeoutWaitsForever()
    {
        Agent agent = new Agent();
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        BehaviourTree<AgentContext> tree = n.Build(n.WaitUntil(Finishing, TimeSpan.Zero));
        NodeState[] state = tree.NewState();

        tree.Tick(state, new AgentContext(agent));
        agent.AdvanceSim(long.MaxValue / 2);

        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Running);
    }

    [Test]
    public void AZeroTimeoutWithAHandlerIsRejected()
    {
        BtFactory<AgentContext> n = Bt.For<AgentContext>();

        Should.Throw<ArgumentOutOfRangeException>(() =>
            n.WaitUntil(Finishing, TimeSpan.Zero, RecordAndSucceed)
        );
        Should.Throw<ArgumentOutOfRangeException>(() =>
            n.WaitUntil("arrive", 7, ReachedSite, TimeSpan.Zero, RecordSite)
        );
    }

    [Test]
    public void TheDeadlineRunsOnTheSelectedClock()
    {
        Agent agent = new Agent();
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        BehaviourTree<AgentContext> tree = n.Build(
            n.WaitUntil(Finishing, TimeSpan.FromMilliseconds(500), AgentContext.Real)
        );
        NodeState[] state = tree.NewState();

        tree.Tick(state, new AgentContext(agent));

        agent.AdvanceSim(1000);
        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Running);

        agent.AdvanceReal(500);
        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Failure);
    }

    [Test]
    public void AResetRestartsTheDeadline()
    {
        Agent agent = new Agent();
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        BehaviourTree<AgentContext> tree = n.Build(
            n.WaitUntil(Finishing, TimeSpan.FromMilliseconds(500))
        );
        NodeState[] state = tree.NewState();

        tree.Tick(state, new AgentContext(agent));
        agent.AdvanceSim(400);
        tree.Reset(state, new AgentContext(agent));

        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Running);
        agent.AdvanceSim(400);
        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Running);
    }

    [Test]
    public void CapturingDelegatesAreRejected()
    {
        bool done = false;
        BtFactory<AgentContext> n = Bt.For<AgentContext>();

        Should.Throw<ArgumentException>(() =>
            n.WaitUntil((in AgentContext _) => done, TimeSpan.FromMilliseconds(500))
        );
        Should.Throw<ArgumentException>(() =>
            n.WaitUntil(
                Finishing,
                TimeSpan.FromMilliseconds(500),
                (in AgentContext _) =>
                {
                    done = true;
                    return TickResult.Failure;
                }
            )
        );
    }

    private static bool Finishing(in AgentContext ctx) => ctx.Agent.Finishing;

    private static TickResult RecordAndSucceed(in AgentContext ctx)
    {
        ctx.Agent.Faults++;
        return TickResult.Success;
    }

    private static bool ReachedSite(in AgentContext ctx, in int site) => ctx.Agent.Count == site;

    private static TickResult RecordSite(in AgentContext ctx, in int site)
    {
        ctx.Agent.Visited.Add(site);
        return TickResult.Failure;
    }
}
