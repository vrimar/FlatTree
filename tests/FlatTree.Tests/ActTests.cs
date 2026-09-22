namespace FlatTree.Tests;

public sealed class ActTests
{
    [Test]
    public void RunsTheEffectAndSucceeds()
    {
        Agent agent = new Agent();
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        BehaviourTree<AgentContext> tree = n.Build(n.Act("count", Count));

        tree.Tick(tree.NewState(), new AgentContext(agent)).ShouldBe(TickResult.Success);
        agent.Count.ShouldBe(1);
    }

    [Test]
    public void RunsOncePerTick()
    {
        Agent agent = new Agent();
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        BehaviourTree<AgentContext> tree = n.Build(n.Act(Count));
        NodeState[] state = tree.NewState();

        tree.Tick(state, new AgentContext(agent));
        tree.Tick(state, new AgentContext(agent));

        agent.Count.ShouldBe(2);
    }

    [Test]
    public void ACapturingEffectIsRejected()
    {
        int seen = 0;
        BtFactory<AgentContext> n = Bt.For<AgentContext>();

        Should.Throw<ArgumentException>(() => n.Act("bad", (in AgentContext _) => seen++));
    }

    [Test]
    public void ANullEffectIsRejected()
    {
        BtFactory<AgentContext> n = Bt.For<AgentContext>();

        Should.Throw<ArgumentNullException>(() => n.Act("bad", (LeafEffect<AgentContext>)null!));
    }

    [Test]
    public void ValueReturningDelegatesAreACompileError()
    {
        IEnumerable<System.Reflection.MethodInfo> valueReturning = typeof(BtFactory<AgentContext>)
            .GetMethods()
            .Where(m => m.Name == nameof(BtFactory<AgentContext>.Act))
            .Where(m => m.GetParameters()[^1].ParameterType != typeof(LeafEffect<AgentContext>));

        valueReturning.Count().ShouldBe(4);
        valueReturning.ShouldAllBe(m =>
            m.GetCustomAttributes(typeof(ObsoleteAttribute), false)
                .Cast<ObsoleteAttribute>()
                .Single()
                .IsError
        );
    }

    private static void Count(in AgentContext ctx) => ctx.Agent.Count++;
}
