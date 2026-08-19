namespace FlatTree.Tests;

/// <summary>
/// Adversarial cases for the nodes added in 0.3.0: state that has to survive a yield, state that has
/// to be gone after a reset, and the interactions between the new loops and the existing composites.
/// </summary>
public sealed class NewNodeEdgeCaseTests
{
    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(1 << 29)]
    [Arguments(1 << 30)]
    [Arguments(int.MaxValue)]
    public void PollingLeafScratchRoundTripsAcrossItsWholeRange(int scratch)
    {
        Agent agent = new Agent();
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        ScratchProbe sut = new ScratchProbe(scratch);
        BehaviourTree<AgentContext> tree = n.Build(sut);
        NodeState[] state = tree.NewState();

        tree.Tick(state, new AgentContext(agent));
        tree.Tick(state, new AgentContext(agent));

        sut.Seen.ShouldBe(scratch);
        state[sut.Id].HasBegun().ShouldBeTrue();
    }

    [Test]
    public void ANegativeScratchIsRejected()
    {
        Agent agent = new Agent();
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        BehaviourTree<AgentContext> tree = n.Build(new ScratchProbe(-1));
        NodeState[] state = tree.NewState();

        Should.Throw<ArgumentOutOfRangeException>(() => tree.Tick(state, new AgentContext(agent)));
    }

    [Test]
    public void PollingLeafScratchSurvivesTheStartedFlag()
    {
        Agent agent = new Agent();
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        ScratchProbe sut = new ScratchProbe(3);
        BehaviourTree<AgentContext> tree = n.Build(sut);
        NodeState[] state = tree.NewState();

        tree.Tick(state, new AgentContext(agent));
        tree.Tick(state, new AgentContext(agent));

        sut.Seen.ShouldBe(3);
        state[sut.Id].HasBegun().ShouldBeTrue();
    }

    [Test]
    public void ARetryUnderASequenceResumesTheSequenceAfterASuccessfulAttempt()
    {
        Agent agent = new Agent();
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        FlakyLeaf flaky = new FlakyLeaf(failuresBeforeSuccess: 2);
        BehaviourTree<AgentContext> tree = n.Build(
            n.Sequence("seq", n.Retry("retry", 4, flaky), n.Do("after", CountLap))
        );
        NodeState[] state = tree.NewState();

        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Running);
        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Running);
        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Success);

        agent.Count.ShouldBe(1);
        flaky.Attempts.ShouldBe(3);
    }

    [Test]
    public void ARetryThatExhaustsItsAttemptsFailsTheSequence()
    {
        Agent agent = new Agent();
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        FlakyLeaf flaky = new FlakyLeaf(failuresBeforeSuccess: 99);
        BehaviourTree<AgentContext> tree = n.Build(
            n.Sequence("seq", n.Retry("retry", 3, flaky), n.Do("after", CountLap))
        );
        NodeState[] state = tree.NewState();

        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Running);
        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Running);
        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Failure);

        agent.Count.ShouldBe(0);
        flaky.Attempts.ShouldBe(3);
    }

    [Test]
    public void EachRetryAttemptStartsFromAFreshChild()
    {
        Agent agent = new Agent();
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        FlakyLeaf flaky = new FlakyLeaf(failuresBeforeSuccess: 99);
        Retry<AgentContext> sut = n.Retry(3, n.Sequence("attempt", flaky));
        BehaviourTree<AgentContext> tree = n.Build(sut);
        NodeState[] state = tree.NewState();

        tree.Tick(state, new AgentContext(agent));
        state[flaky.Id].Status.ShouldBe(NodeStatus.Fresh);
    }

    [Test]
    public void ARetryBodyInsideAForEachRestartsPerItem()
    {
        Agent agent = new Agent { Count = 0 };
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        FlakyLeaf flaky = new FlakyLeaf(failuresBeforeSuccess: 1, repeating: true);
        BehaviourTree<AgentContext> tree = n.Build(
            n.ForEach("each", static (in AgentContext _) => 3, n.Retry("retry", 2, flaky))
        );
        NodeState[] state = tree.NewState();

        // A failed attempt yields the tick, so each item costs two: three items, four ticks.
        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Running);
        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Running);
        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Running);
        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Success);

        // Every item failed once and then succeeded, with no attempt carried over.
        flaky.Attempts.ShouldBe(6);
    }

    [Test]
    public void AForEachThatFailsMidwayRestartsFromTheFirstItem()
    {
        Agent agent = new Agent { Count = 4 };
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        ForEach<AgentContext> sut = n.ForEach(
            static (in AgentContext c) => c.Agent.Count,
            n.Do("body", FailOnThirdVisit),
            static (in AgentContext c, int index) => c.Agent.Visited.Add(index)
        );
        BehaviourTree<AgentContext> tree = n.Build(sut);
        NodeState[] state = tree.NewState();

        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Failure);
        state[sut.Id].Cursor.ShouldBe(0);

        agent.Visited.Clear();
        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Failure);
        agent.Visited.ShouldBe([0, 1, 2]);
    }

    [Test]
    public void AResetForcesAJitteredWaitToRedraw()
    {
        Agent agent = new Agent(new SeededRandomProvider(11));
        BtFactory<AgentContext> n = Bt.For<AgentContext>(AgentContext.Rng);
        Wait<AgentContext> sut = n.Wait(
            "jitter",
            TimeSpan.FromMilliseconds(1000),
            TimeSpan.FromMilliseconds(2000)
        );
        BehaviourTree<AgentContext> tree = n.Build(sut);
        NodeState[] state = tree.NewState();

        tree.Tick(state, new AgentContext(agent));
        long first = state[sut.Id].Stamp;

        tree.Reset(state, new AgentContext(agent));
        state[sut.Id].Stamp.ShouldBe(0);

        tree.Tick(state, new AgentContext(agent));
        state[sut.Id].Stamp.ShouldNotBe(first);
    }

    [Test]
    public void AJitteredWaitHoldsItsDrawnDeadlineAcrossTicks()
    {
        Agent agent = new Agent(new SeededRandomProvider(5));
        BtFactory<AgentContext> n = Bt.For<AgentContext>(AgentContext.Rng);
        Wait<AgentContext> sut = n.Wait(
            "jitter",
            TimeSpan.FromMilliseconds(1000),
            TimeSpan.FromMilliseconds(2000)
        );
        BehaviourTree<AgentContext> tree = n.Build(sut);
        NodeState[] state = tree.NewState();

        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Running);
        long deadline = state[sut.Id].Stamp;

        agent.AdvanceSim(999);
        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Running);
        state[sut.Id].Stamp.ShouldBe(deadline);

        agent.AdvanceSim(1001);
        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Success);
    }

    [Test]
    public void AWaitWhoseAccessorReturnsZeroSucceedsImmediately()
    {
        Agent agent = new Agent();
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        BehaviourTree<AgentContext> tree = n.Build(
            n.Wait("none", static (in AgentContext _) => TimeSpan.Zero)
        );

        tree.Tick(tree.NewState(), new AgentContext(agent)).ShouldBe(TickResult.Success);
    }

    [Test]
    public void AWaitAccessorIsReadOncePerActivation()
    {
        Agent agent = new Agent { Count = 500 };
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        BehaviourTree<AgentContext> tree = n.Build(n.Wait("paced", CountedDuration));
        NodeState[] state = tree.NewState();

        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Running);
        agent.Faults.ShouldBe(1);

        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Running);
        agent.Faults.ShouldBe(1);
    }

    [Test]
    public void ResetAllCancelsAPollingLeafThatHasBegun()
    {
        Agent agent = new Agent();
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        ScratchProbe sut = new ScratchProbe(1);
        BehaviourTree<AgentContext> tree = n.Build(sut);
        NodeState[] state = tree.NewState();

        tree.Tick(state, new AgentContext(agent));
        tree.ResetAll(state, new AgentContext(agent));

        sut.CancelCount.ShouldBe(1);

        tree.ResetAll(state, new AgentContext(agent));
        sut.CancelCount.ShouldBe(1);
    }

    [Test]
    public void APreemptedPollingLeafIsCancelledByTheReactiveParent()
    {
        Agent agent = new Agent();
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        ScratchProbe sut = new ScratchProbe(1);
        BehaviourTree<AgentContext> tree = n.Build(
            n.PrioritySelector("root", n.Condition("gate", Gate), n.Sequence("low", sut))
        );
        NodeState[] state = tree.NewState();

        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Running);
        sut.CancelCount.ShouldBe(0);

        agent.Finishing = true;
        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Success);
        sut.CancelCount.ShouldBe(1);
    }

    [Test]
    public void ACatchDoesNotSwallowARunningChild()
    {
        Agent agent = new Agent();
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        ScratchProbe probe = new ScratchProbe(1);
        BehaviourTree<AgentContext> tree = n.Build(
            n.Catch("guard", probe, static (in AgentContext c) => TickResult.Success)
        );
        NodeState[] state = tree.NewState();

        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Running);
        agent.Faults.ShouldBe(0);
    }

    [Test]
    public void AStatefulCatchHandsTheAuthoredStateToItsHandler()
    {
        Agent agent = new Agent();
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        BehaviourTree<AgentContext> tree = n.Build(
            n.Catch(
                "guard",
                n.Do("fail", static (in AgentContext _) => TickResult.Failure),
                (Code: 7, Where: "site"),
                static (in AgentContext c, in (int Code, string Where) site) =>
                {
                    c.Agent.Faults += site.Code;
                    c.Agent.Visited.Add(site.Where.Length);
                    return TickResult.Success;
                }
            )
        );

        tree.Tick(tree.NewState(), new AgentContext(agent)).ShouldBe(TickResult.Success);
        agent.Faults.ShouldBe(7);
        agent.Visited.ShouldBe([4]);
    }

    [Test]
    public void TagsSurviveManyNodesAndManyTags()
    {
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        BtNode<AgentContext>[] children = new BtNode<AgentContext>[24];

        for (int i = 0; i < children.Length; i++)
        {
            Do<AgentContext> leaf = n.Do($"leaf-{i}", static (in AgentContext _) => TickResult.Success);
            children[i] = i % 3 == 0 ? n.Tagged(leaf, 1 + (i % 2)) : leaf;
        }

        BehaviourTree<AgentContext> tree = n.Build(n.Sequence("root", children));

        tree.NodesWith(1).Length.ShouldBe(4);
        tree.NodesWith(2).Length.ShouldBe(4);
        tree.NodesWith(3).IsEmpty.ShouldBeTrue();

        int previous = -1;
        foreach (BtNode<AgentContext> node in tree.NodesWith(1))
        {
            node.Tag.ShouldBe(1);
            node.Id.ShouldBeGreaterThan(previous);
            previous = node.Id;
        }
    }

    [Test]
    public void ATimeLimitOnASelectedClockRestartsAfterAReset()
    {
        Agent agent = new Agent();
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        TimeLimit<AgentContext> sut = n.TimeLimit(
            TimeSpan.FromMilliseconds(500),
            n.Do("run", static (in AgentContext _) => TickResult.Running),
            AgentContext.Real
        );
        BehaviourTree<AgentContext> tree = n.Build(sut);
        NodeState[] state = tree.NewState();

        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Running);

        agent.AdvanceReal(400);
        tree.Reset(state, new AgentContext(agent));
        state[sut.Id].Stamp.ShouldBe(0);

        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Running);

        agent.AdvanceReal(400);
        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Running);

        agent.AdvanceReal(100);
        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Failure);
    }

    [Test]
    public void ACooldownOnASelectedClockKeepsItsTimerAcrossAReset()
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
        tree.Reset(state, new AgentContext(agent));

        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Failure);

        agent.AdvanceReal(500);
        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Success);
    }

    [Test]
    public void AForeverExitPredicateIsOnlyConsultedAtABoundary()
    {
        Agent agent = new Agent { Finishing = true };
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        FlakyLeaf body = new FlakyLeaf(failuresBeforeSuccess: 0, running: true);
        BehaviourTree<AgentContext> tree = n.Build(
            n.Forever("laps", body, static (in AgentContext c) => c.Agent.Finishing)
        );
        NodeState[] state = tree.NewState();

        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Running);
        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Running);

        body.Running = false;
        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Success);
    }

    [Test]
    public void TheNewLoopsKeepTheirCursorsPerAgent()
    {
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        FlakyLeaf flaky = new FlakyLeaf(failuresBeforeSuccess: 99);
        Retry<AgentContext> retry = n.Retry("retry", 3, flaky);
        ForEach<AgentContext> each = n.ForEach(
            static (in AgentContext c) => c.Agent.Count,
            n.Sequence("body", retry)
        );
        BehaviourTree<AgentContext> tree = n.Build(each);

        Agent first = new Agent { Count = 2 };
        Agent second = new Agent { Count = 5 };
        NodeState[] a = tree.NewState();
        NodeState[] b = tree.NewState();

        tree.Tick(a, new AgentContext(first));
        tree.Tick(b, new AgentContext(second));
        tree.Tick(b, new AgentContext(second));

        a[each.Id].Cursor.ShouldBe(0);
        b[each.Id].Cursor.ShouldBe(0);
        a[tree.Root.Id].Status.ShouldBe(NodeStatus.Running);

        // Agent b spent one more attempt than a, on its own slot only.
        a[retry.Id].Cursor.ShouldBe(1);
        b[retry.Id].Cursor.ShouldBe(2);
    }

    [Test]
    public void APollingLeafKeepsItsScratchPerAgent()
    {
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        ScratchProbe sut = new ScratchProbe(9);
        BehaviourTree<AgentContext> tree = n.Build(sut);

        Agent first = new Agent();
        Agent second = new Agent();
        second.AdvanceSim(500);

        NodeState[] a = tree.NewState();
        NodeState[] b = tree.NewState();

        tree.Tick(a, new AgentContext(first));
        tree.Tick(b, new AgentContext(second));

        a[sut.Id].Stamp.ShouldBe(0);
        b[sut.Id].Stamp.ShouldBe(500);
        a[sut.Id].Cursor.ShouldBe(b[sut.Id].Cursor);

        tree.Reset(a, new AgentContext(first));
        a[sut.Id].HasBegun().ShouldBeFalse();
        b[sut.Id].HasBegun().ShouldBeTrue();
        b[sut.Id].Stamp.ShouldBe(500);
    }

    private static bool Gate(in AgentContext ctx) => ctx.Agent.Finishing;

    private static TickResult CountLap(in AgentContext ctx)
    {
        ctx.Agent.Count++;
        return TickResult.Success;
    }

    private static TimeSpan CountedDuration(in AgentContext ctx)
    {
        ctx.Agent.Faults++;
        return TimeSpan.FromMilliseconds(ctx.Agent.Count);
    }

    private static TickResult FailOnThirdVisit(in AgentContext ctx) =>
        ctx.Agent.Visited.Count >= 3 ? TickResult.Failure : TickResult.Success;

    private sealed class ScratchProbe(int scratch) : PollingLeaf<AgentContext>("probe", TimeSpan.Zero)
    {
        public int Seen { get; private set; } = -1;

        public int CancelCount { get; private set; }

        protected override TickResult Begin(Span<NodeState> s, in AgentContext ctx)
        {
            SetScratch(s, scratch);
            return TickResult.Success;
        }

        protected override TickResult Poll(Span<NodeState> s, in AgentContext ctx)
        {
            Seen = Scratch(s);
            return TickResult.Running;
        }

        protected override void Cancel(Span<NodeState> s, in AgentContext ctx) => CancelCount++;
    }

    private sealed class FlakyLeaf : LeafNode<AgentContext>
    {
        private readonly int _failuresBeforeSuccess;
        private readonly bool _repeating;

        public FlakyLeaf(int failuresBeforeSuccess, bool repeating = false, bool running = false)
            : base("flaky")
        {
            _failuresBeforeSuccess = failuresBeforeSuccess;
            _repeating = repeating;
            Running = running;
        }

        public int Attempts { get; private set; }

        public bool Running { get; set; }

        protected override TickResult Update(Span<NodeState> s, in AgentContext ctx)
        {
            if (Running)
            {
                return TickResult.Running;
            }

            Attempts++;
            int within = _repeating
                ? (Attempts - 1) % (_failuresBeforeSuccess + 1)
                : Attempts - 1;

            return within < _failuresBeforeSuccess ? TickResult.Failure : TickResult.Success;
        }
    }
}
