namespace FlatTree.Tests;

public sealed class PollingLeafTests
{
    [Test]
    public void BeginRunsOnceAndPollEveryTick()
    {
        Agent agent = new Agent();
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        ProbeLeaf sut = new ProbeLeaf(TimeSpan.Zero);
        BehaviourTree<AgentContext> tree = n.Build(sut);
        NodeState[] state = tree.NewState();

        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Running);
        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Running);

        sut.BeginCount.ShouldBe(1);
        sut.PollCount.ShouldBe(2);
    }

    [Test]
    public void AFailingBeginNeverPolls()
    {
        Agent agent = new Agent();
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        ProbeLeaf sut = new ProbeLeaf(TimeSpan.Zero) { BeginStatus = TickResult.Failure };
        BehaviourTree<AgentContext> tree = n.Build(sut);

        tree.Tick(tree.NewState(), new AgentContext(agent)).ShouldBe(TickResult.Failure);

        sut.PollCount.ShouldBe(0);
    }

    [Test]
    public void ExpiringCallsOnTimeout()
    {
        Agent agent = new Agent();
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        ProbeLeaf sut = new ProbeLeaf(TimeSpan.FromMilliseconds(500));
        BehaviourTree<AgentContext> tree = n.Build(sut);
        NodeState[] state = tree.NewState();

        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Running);

        agent.AdvanceSim(500);
        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Failure);

        sut.TimeoutCount.ShouldBe(1);
    }

    [Test]
    public void ZeroTimeoutPollsForever()
    {
        Agent agent = new Agent();
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        ProbeLeaf sut = new ProbeLeaf(TimeSpan.Zero);
        BehaviourTree<AgentContext> tree = n.Build(sut);
        NodeState[] state = tree.NewState();

        tree.Tick(state, new AgentContext(agent));
        agent.AdvanceSim(long.MaxValue / 2);
        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Running);

        sut.TimeoutCount.ShouldBe(0);
    }

    [Test]
    public void ResettingAStartedLeafCancelsIt()
    {
        Agent agent = new Agent();
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        ProbeLeaf sut = new ProbeLeaf(TimeSpan.Zero);
        BehaviourTree<AgentContext> tree = n.Build(sut);
        NodeState[] state = tree.NewState();

        tree.Tick(state, new AgentContext(agent));
        tree.Reset(state, new AgentContext(agent));

        sut.CancelCount.ShouldBe(1);
        state[sut.Id].Cursor.ShouldBe(0);
        state[sut.Id].Stamp.ShouldBe(0);
    }

    [Test]
    public void AnUnstartedLeafIsNeverCancelled()
    {
        Agent agent = new Agent();
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        ProbeLeaf sut = new ProbeLeaf(TimeSpan.Zero);
        BehaviourTree<AgentContext> tree = n.Build(sut);

        tree.ResetAll(tree.NewState(), new AgentContext(agent));

        sut.CancelCount.ShouldBe(0);
    }

    [Test]
    public void CompletingReleasesTheScratchSoTheNextActivationBeginsAgain()
    {
        Agent agent = new Agent();
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        ProbeLeaf sut = new ProbeLeaf(TimeSpan.Zero) { PollStatus = TickResult.Success };
        BehaviourTree<AgentContext> tree = n.Build(sut);
        NodeState[] state = tree.NewState();

        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Success);
        tree.Tick(state, new AgentContext(agent)).ShouldBe(TickResult.Success);

        sut.BeginCount.ShouldBe(2);
        sut.CancelCount.ShouldBe(0);
    }

    [Test]
    public void ScratchSurvivesAcrossPollsAndClearsOnReset()
    {
        Agent agent = new Agent();
        BtFactory<AgentContext> n = Bt.For<AgentContext>();
        ScratchLeaf sut = new ScratchLeaf();
        BehaviourTree<AgentContext> tree = n.Build(sut);
        NodeState[] state = tree.NewState();

        tree.Tick(state, new AgentContext(agent));
        tree.Tick(state, new AgentContext(agent));
        sut.LastSeen.ShouldBe(41);

        tree.Reset(state, new AgentContext(agent));
        state[sut.Id].Cursor.ShouldBe(0);
    }

    private sealed class ProbeLeaf(TimeSpan timeout) : PollingLeaf<AgentContext>("probe", timeout)
    {
        public TickResult BeginStatus { get; set; } = TickResult.Success;

        public TickResult PollStatus { get; set; } = TickResult.Running;

        public int BeginCount { get; private set; }

        public int PollCount { get; private set; }

        public int TimeoutCount { get; private set; }

        public int CancelCount { get; private set; }

        protected override TickResult Begin(Span<NodeState> s, in AgentContext ctx)
        {
            BeginCount++;
            return BeginStatus;
        }

        protected override TickResult Poll(Span<NodeState> s, in AgentContext ctx)
        {
            PollCount++;
            return PollStatus;
        }

        protected override TickResult OnTimeout(Span<NodeState> s, in AgentContext ctx)
        {
            TimeoutCount++;
            return TickResult.Failure;
        }

        protected override void Cancel(Span<NodeState> s, in AgentContext ctx) => CancelCount++;
    }

    private sealed class ScratchLeaf : PollingLeaf<AgentContext>
    {
        public ScratchLeaf()
            : base("scratch", TimeSpan.Zero) { }

        public int LastSeen { get; private set; }

        protected override TickResult Begin(Span<NodeState> s, in AgentContext ctx)
        {
            SetScratch(s, 41);
            return TickResult.Success;
        }

        protected override TickResult Poll(Span<NodeState> s, in AgentContext ctx)
        {
            LastSeen = Scratch(s);
            return TickResult.Running;
        }
    }
}
