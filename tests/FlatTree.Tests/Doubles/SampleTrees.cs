namespace FlatTree.Tests.Doubles;

/// <summary>Sample trees used across tests. Leaf delegates are static (capture nothing).</summary>
public static class SampleTrees
{
    private static TickResult AlwaysSucceedAction(in FakeClock c) => TickResult.Success;

    private static TickResult AlwaysRunningAction(in FakeClock c) => TickResult.Running;

    private static TickResult PeriodicAction(in FakeClock c) =>
        (c.NowMs / 100) % 2 == 0 ? TickResult.Success : TickResult.Running;

    private static bool TruePredicate(in FakeClock c) => true;

    private static bool FalsePredicate(in FakeClock c) => false;

    private static bool PeriodicPredicate(in FakeClock c) => (c.NowMs / 100) % 3 == 0;

    /// <summary>Builds a tree that exercises every node type. <paramref name="nodeCount"/> is its node count.</summary>
    public static BehaviourTree<FakeClock> EveryNodeType(out int nodeCount)
    {
        var n = Bt.For<FakeClock>();

        var root = n.PrioritySelector(
            "root",
            n.Sequence(
                "seq",
                n.Condition("seq-cond", PeriodicPredicate),
                n.Cooldown(
                    "cooldown",
                    TimeSpan.FromMilliseconds(300),
                    n.Do("cooldown-do", AlwaysSucceedAction)
                )
            ),
            n.PrioritySequence(
                "pseq",
                n.Condition("pseq-cond", TruePredicate),
                n.Wait("wait", TimeSpan.FromMilliseconds(250)),
                n.SimpleParallel(
                    "parallel",
                    SimpleParallelPolicy.BothMustSucceed,
                    n.AutoReset(
                        "autoreset",
                        n.Repeat("repeat", 3, n.Condition("repeat-cond", PeriodicPredicate))
                    ),
                    n.TimeLimit(
                        "timelimit",
                        TimeSpan.FromMilliseconds(500),
                        n.UntilFailed(
                            "untilfailed",
                            n.Inverter("invert", n.Condition("invert-cond", FalsePredicate))
                        )
                    )
                )
            ),
            n.Selector(
                "sel",
                n.RateLimiter(
                    "ratelimiter",
                    TimeSpan.FromMilliseconds(200),
                    n.Do("rl-do", PeriodicAction)
                ),
                n.AlwaysSucceed(
                    "always-succeed",
                    n.AlwaysFail("always-fail", n.Do("af-do", AlwaysSucceedAction))
                ),
                n.UntilSuccess("untilsuccess", n.Do("us-do", PeriodicAction)),
                n.Chance("chance", 0.5, n.Do("chance-do", AlwaysSucceedAction))
            ),
            n.RandomSelector(
                "randsel",
                n.Do("rs-do-1", AlwaysRunningAction),
                n.Do("rs-do-2", AlwaysSucceedAction)
            ),
            n.RandomSequence(
                "randseq",
                n.Do("rq-do-1", AlwaysSucceedAction),
                n.Do("rq-do-2", AlwaysSucceedAction)
            )
        );

        nodeCount = 32;
        return n.Build(root);
    }
}
