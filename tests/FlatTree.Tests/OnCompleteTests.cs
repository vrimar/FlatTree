namespace FlatTree.Tests;

public sealed class OnCompleteTests
{
    [Test]
    [Arguments(TickResult.Success)]
    [Arguments(TickResult.Failure)]
    public void TheHandlerRunsOnEitherOutcomeAndSeesIt(TickResult outcome)
    {
        Agent agent = new Agent { Count = (int)outcome };
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        BehaviourTree<AgentContext> tree = n.Build(
            n.OnComplete("finish", n.Do("steps", Scripted), RecordOutcome)
        );

        tree.Tick(tree.NewState(), new AgentContext(agent)).ShouldBe(outcome);
        agent.Visited.ShouldBe([(int)outcome]);
    }

    [Test]
    public void TheHandlerDecidesTheStatus()
    {
        Agent agent = new Agent { Count = (int)TickResult.Failure };
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        BehaviourTree<AgentContext> tree = n.Build(
            n.OnComplete(
                n.Do("steps", Scripted),
                static (in AgentContext _, TickResult _) => TickResult.Success
            )
        );

        tree.Tick(tree.NewState(), new AgentContext(agent)).ShouldBe(TickResult.Success);
    }

    [Test]
    public void ARunningChildPassesThroughWithoutTheHandler()
    {
        Agent agent = new Agent { Count = (int)TickResult.Running };
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        BehaviourTree<AgentContext> tree = n.Build(
            n.OnComplete(n.Do("steps", Scripted), RecordOutcome)
        );

        tree.Tick(tree.NewState(), new AgentContext(agent)).ShouldBe(TickResult.Running);
        agent.Visited.ShouldBeEmpty();
    }

    [Test]
    public void AResetOfARunningChildDoesNotRunTheHandler()
    {
        Agent agent = new Agent { Count = (int)TickResult.Running };
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        BehaviourTree<AgentContext> tree = n.Build(
            n.OnComplete(n.Do("steps", Scripted), RecordOutcome)
        );
        NodeState[] state = tree.NewState();

        tree.Tick(state, new AgentContext(agent));
        tree.Reset(state, new AgentContext(agent));
        tree.ResetAll(state, new AgentContext(agent));

        agent.Visited.ShouldBeEmpty();
    }

    [Test]
    public void TheStatefulHandlerReceivesTheAuthoredState()
    {
        Agent agent = new Agent { Count = (int)TickResult.Success };
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        OnComplete<AgentContext, int> sut = n.OnComplete(
            "finish",
            n.Do("steps", Scripted),
            40,
            static (in AgentContext c, in int site, TickResult outcome) =>
            {
                c.Agent.Visited.Add(site + (int)outcome);
                return outcome;
            }
        );
        BehaviourTree<AgentContext> tree = n.Build(sut);

        tree.Tick(tree.NewState(), new AgentContext(agent)).ShouldBe(TickResult.Success);
        agent.Visited.ShouldBe([40 + (int)TickResult.Success]);
        sut.State.ShouldBe(40);
    }

    [Test]
    public void TheNamelessStatefulOverloadDefaultsTheName()
    {
        Agent agent = new Agent { Count = (int)TickResult.Failure };
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        OnComplete<AgentContext, int> sut = n.OnComplete(
            n.Do("steps", Scripted),
            40,
            static (in AgentContext c, in int site, TickResult outcome) =>
            {
                c.Agent.Visited.Add(site);
                return TickResult.Success;
            }
        );
        BehaviourTree<AgentContext> tree = n.Build(sut);

        tree.Tick(tree.NewState(), new AgentContext(agent)).ShouldBe(TickResult.Success);
        agent.Visited.ShouldBe([40]);
        sut.Name.ShouldBe("OnComplete");
    }

    [Test]
    public void AHandlerReturningAnUndefinedStatusIsRejected()
    {
        Agent agent = new Agent { Count = (int)TickResult.Success };
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        BehaviourTree<AgentContext> tree = n.Build(
            n.OnComplete(
                n.Do("steps", Scripted),
                static (in AgentContext _, TickResult _) => default
            )
        );
        NodeState[] state = tree.NewState();

        Should.Throw<InvalidOperationException>(() => tree.Tick(state, new AgentContext(agent)));
    }

    [Test]
    public void AHandlerReturningRunningIsRejected()
    {
        Agent agent = new Agent { Count = (int)TickResult.Success };
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        BehaviourTree<AgentContext> tree = n.Build(
            n.OnComplete(
                n.Do("steps", Scripted),
                static (in AgentContext _, TickResult _) => TickResult.Running
            )
        );
        NodeState[] state = tree.NewState();

        Should.Throw<InvalidOperationException>(() => tree.Tick(state, new AgentContext(agent)));
    }

    [Test]
    public void ACapturingHandlerIsRejected()
    {
        int seen = 0;
        BtFactory<AgentContext> n = Bt.For<AgentContext>();

        Should.Throw<ArgumentException>(() =>
            n.OnComplete(
                n.Do("steps", Scripted),
                (in AgentContext _, TickResult outcome) =>
                {
                    seen++;
                    return outcome;
                }
            )
        );
    }

    private static TickResult Scripted(in AgentContext ctx) => (TickResult)ctx.Agent.Count;

    private static TickResult RecordOutcome(in AgentContext ctx, TickResult outcome)
    {
        ctx.Agent.Visited.Add((int)outcome);
        return outcome;
    }
}
