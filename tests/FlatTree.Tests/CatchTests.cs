namespace FlatTree.Tests;

public sealed class CatchTests
{
    [Test]
    public void WhenChildFails_TheHandlerDecidesTheStatus()
    {
        Agent agent = new Agent();
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        Catch<AgentContext> sut = n.Catch(
            "Catch",
            n.Do("fail", static (in AgentContext _) => TickResult.Failure),
            Record
        );
        BehaviourTree<AgentContext> tree = n.Build(sut);

        tree.Tick(tree.NewState(), new AgentContext(agent)).ShouldBe(TickResult.Success);
        agent.Faults.ShouldBe(1);
    }

    [Test]
    public void WhenChildSucceedsOrRuns_TheHandlerNeverRuns()
    {
        Agent agent = new Agent();
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        Catch<AgentContext> sut = n.Catch(
            n.Do("run", static (in AgentContext _) => TickResult.Running),
            Record
        );
        BehaviourTree<AgentContext> tree = n.Build(sut);

        tree.Tick(tree.NewState(), new AgentContext(agent)).ShouldBe(TickResult.Running);
        agent.Faults.ShouldBe(0);
    }

    [Test]
    public void AHandlerMayAlsoKeepTheFailure()
    {
        Agent agent = new Agent();
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        Catch<AgentContext> sut = n.Catch(
            n.Do("fail", static (in AgentContext _) => TickResult.Failure),
            static (in AgentContext c) =>
            {
                c.Agent.Faults++;
                return TickResult.Failure;
            }
        );
        BehaviourTree<AgentContext> tree = n.Build(sut);

        tree.Tick(tree.NewState(), new AgentContext(agent)).ShouldBe(TickResult.Failure);
        agent.Faults.ShouldBe(1);
    }

    [Test]
    public void AHandlerReturningRunningIsRejected()
    {
        Agent agent = new Agent();
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        Catch<AgentContext> sut = n.Catch(
            n.Do("fail", static (in AgentContext _) => TickResult.Failure),
            static (in AgentContext _) => TickResult.Running
        );
        BehaviourTree<AgentContext> tree = n.Build(sut);
        NodeState[] state = tree.NewState();

        Should.Throw<InvalidOperationException>(() => tree.Tick(state, new AgentContext(agent)));
    }

    [Test]
    public void ACapturingHandlerIsRejected()
    {
        int seen = 0;
        BtFactory<AgentContext> n = Bt.For<AgentContext>();

        Should.Throw<ArgumentException>(() =>
            n.Catch(
                n.Do("fail", static (in AgentContext _) => TickResult.Failure),
                (in AgentContext _) =>
                {
                    seen++;
                    return TickResult.Success;
                }
            )
        );
    }

    private static TickResult Record(in AgentContext ctx)
    {
        ctx.Agent.Faults++;
        return TickResult.Success;
    }
}
