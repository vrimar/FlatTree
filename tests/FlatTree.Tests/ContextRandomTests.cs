namespace FlatTree.Tests;

public sealed class ContextRandomTests
{
    [Test]
    public void EachAgentDrawsFromItsOwnStream()
    {
        BtFactory<AgentContext> n = Bt.For<AgentContext>(AgentContext.Rng);
        BehaviourTree<AgentContext> tree = n.Build(
            n.Wait("jitter", TimeSpan.FromMilliseconds(1000), TimeSpan.FromMilliseconds(2000))
        );

        Agent first = new Agent(new SeededRandomProvider(7));
        Agent second = new Agent(new SeededRandomProvider(7));
        Agent third = new Agent(new SeededRandomProvider(99));

        long firstDeadline = DrawDeadline(tree, first);
        long secondDeadline = DrawDeadline(tree, second);
        long thirdDeadline = DrawDeadline(tree, third);

        secondDeadline.ShouldBe(firstDeadline);
        thirdDeadline.ShouldNotBe(firstDeadline);
    }

    [Test]
    public void OneAgentsDrawsDoNotAdvanceAnothers()
    {
        BtFactory<AgentContext> n = Bt.For<AgentContext>(AgentContext.Rng);
        BehaviourTree<AgentContext> tree = n.Build(
            n.Wait("jitter", TimeSpan.FromMilliseconds(1000), TimeSpan.FromMilliseconds(2000))
        );

        Agent noisy = new Agent(new SeededRandomProvider(7));
        DrawDeadline(tree, noisy);
        DrawDeadline(tree, noisy);

        Agent quiet = new Agent(new SeededRandomProvider(7));
        Agent alone = new Agent(new SeededRandomProvider(7));

        DrawDeadline(tree, quiet).ShouldBe(DrawDeadline(tree, alone));
    }

    [Test]
    public void AChanceGateRollsAgainstTheAgentsProvider()
    {
        ScriptedRandomProvider always = new ScriptedRandomProvider();
        always.SetNextDouble(0.0);
        ScriptedRandomProvider never = new ScriptedRandomProvider();
        never.SetNextDouble(0.99);

        BtFactory<AgentContext> n = Bt.For<AgentContext>(AgentContext.Rng);
        BehaviourTree<AgentContext> tree = n.Build(
            n.Chance(0.5, n.Do("act", static (in AgentContext _) => TickResult.Success))
        );

        tree.Tick(tree.NewState(), new AgentContext(new Agent(always)))
            .ShouldBe(TickResult.Success);
        tree.Tick(tree.NewState(), new AgentContext(new Agent(never))).ShouldBe(TickResult.Failure);
    }

    [Test]
    public void ACapturingSourceIsRejected()
    {
        Agent agent = new Agent();

        Should.Throw<ArgumentException>(() =>
            Bt.For<AgentContext>((in AgentContext _) => agent.Rng)
        );
    }

    private static long DrawDeadline(BehaviourTree<AgentContext> tree, Agent agent)
    {
        NodeState[] state = tree.NewState();
        tree.Tick(state, new AgentContext(agent));
        return state[tree.Root.Id].Stamp;
    }
}
