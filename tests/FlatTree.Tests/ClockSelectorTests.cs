namespace FlatTree.Tests;

public sealed class ClockSelectorTests
{
    [Test]
    public void AWaitPacesOnTheClockItWasGiven()
    {
        Agent agent = new Agent();
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        BehaviourTree<AgentContext> tree = n.Build(
            n.Wait("w", TimeSpan.FromMilliseconds(1000), AgentContext.Real)
        );
        NodeState[] state = tree.NewState();

        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Running);

        agent.AdvanceSim(5000);
        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Running);

        agent.AdvanceReal(1000);
        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Success);
    }

    [Test]
    public void WithoutASelectorItPacesOnNowMs()
    {
        Agent agent = new Agent();
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        BehaviourTree<AgentContext> tree = n.Build(n.Wait(TimeSpan.FromMilliseconds(1000)));
        NodeState[] state = tree.NewState();

        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Running);

        agent.AdvanceSim(1000);
        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Success);
    }

    [Test]
    public void ATimeLimitPacesOnTheClockItWasGiven()
    {
        Agent agent = new Agent();
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        BehaviourTree<AgentContext> tree = n.Build(
            n.TimeLimit(
                TimeSpan.FromMilliseconds(500),
                n.Do("run", static (in AgentContext _) => TickResult.Running),
                AgentContext.Real
            )
        );
        NodeState[] state = tree.NewState();

        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Running);

        agent.AdvanceSim(5000);
        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Running);

        agent.AdvanceReal(500);
        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Failure);
    }

    [Test]
    public void ACooldownPacesOnTheClockItWasGiven()
    {
        Agent agent = new Agent();
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        BehaviourTree<AgentContext> tree = n.Build(
            n.Cooldown(
                TimeSpan.FromMilliseconds(500),
                n.Do("act", static (in AgentContext _) => TickResult.Success),
                AgentContext.Real
            )
        );
        NodeState[] state = tree.NewState();

        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Success);

        agent.AdvanceSim(5000);
        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Failure);

        agent.AdvanceReal(500);
        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Success);
    }

    [Test]
    public void ACapturingSelectorIsRejected()
    {
        Agent agent = new Agent();
        BtFactory<AgentContext> n = Bt.For<AgentContext>();

        Should.Throw<ArgumentException>(() =>
            n.Wait(TimeSpan.FromMilliseconds(10), (in AgentContext _) => agent.RealNowMs)
        );
    }
}
