namespace FlatTree.Tests;

public sealed class ForeverExitTests
{
    [Test]
    public void TheLoopEndsAtTheFirstCompletionThePredicateHoldsAt()
    {
        Agent agent = new Agent();
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        BehaviourTree<AgentContext> tree = n.Build(
            n.Forever("laps", n.Do("lap", CountLap), Finishing)
        );
        NodeState[] state = tree.NewState();

        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Running);
        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Running);

        agent.Finishing = true;
        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Success);
    }

    [Test]
    public void ARunningChildIsNeverTornDownMidLap()
    {
        Agent agent = new Agent { Finishing = true };
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        BehaviourTree<AgentContext> tree = n.Build(
            n.Forever(n.Do("lap", static (in AgentContext _) => TickResult.Running), Finishing)
        );

        tree.Tick(tree.NewState(), new AgentContext(agent)).ShouldBe(TickResult.Running);
    }

    [Test]
    public void AChildFailureIsAlsoALapBoundary()
    {
        Agent agent = new Agent { Finishing = true };
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        BehaviourTree<AgentContext> tree = n.Build(
            n.Forever(n.Do("lap", static (in AgentContext _) => TickResult.Failure), Finishing)
        );

        tree.Tick(tree.NewState(), new AgentContext(agent)).ShouldBe(TickResult.Success);
    }

    [Test]
    public void WithoutAPredicateItStillNeverTerminates()
    {
        Agent agent = new Agent();
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        BehaviourTree<AgentContext> tree = n.Build(
            n.Forever(n.Do("lap", static (in AgentContext _) => TickResult.Success))
        );
        NodeState[] state = tree.NewState();

        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Running);
        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Running);
    }

    private static bool Finishing(in AgentContext ctx) => ctx.Agent.Finishing;

    private static TickResult CountLap(in AgentContext ctx)
    {
        ctx.Agent.Count++;
        return TickResult.Success;
    }
}
