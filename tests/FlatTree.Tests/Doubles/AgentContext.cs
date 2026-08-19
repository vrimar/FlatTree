namespace FlatTree.Tests.Doubles;

/// <summary>
/// A context carrying two clocks and its own randomness, for the selector-driven nodes. Mutable
/// through the wrapping <see cref="Agent"/> so a test can advance either clock independently.
/// </summary>
public readonly struct AgentContext(Agent agent) : IClock
{
    public Agent Agent { get; } = agent;

    public long NowMs => Agent.SimNowMs;

    public long RealNowMs => Agent.RealNowMs;

    public static long Sim(in AgentContext ctx) => ctx.NowMs;

    public static long Real(in AgentContext ctx) => ctx.RealNowMs;

    public static IRandomProvider Rng(in AgentContext ctx) => ctx.Agent.Rng;
}

public sealed class Agent(IRandomProvider? rng = null)
{
    public long SimNowMs { get; private set; }

    public long RealNowMs { get; private set; }

    public IRandomProvider Rng { get; } = rng ?? DefaultRandomProvider.Instance;

    public int Count { get; set; }

    public List<int> Visited { get; } = [];

    public int Faults { get; set; }

    public bool Finishing { get; set; }

    public void AdvanceSim(long ms) => SimNowMs += ms;

    public void AdvanceReal(long ms) => RealNowMs += ms;
}
