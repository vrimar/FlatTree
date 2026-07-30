namespace FlatTree.Bench;

/// <summary>Representative trees for benchmarking. Leaf delegates are static (capture nothing).</summary>
public static class BenchTrees
{
    private static TickResult Succeed(in BenchContext c) => TickResult.Success;

    private static TickResult Run(in BenchContext c) => TickResult.Running;

    private static TickResult Periodic(in BenchContext c) =>
        (c.NowMs / 100) % 2 == 0 ? TickResult.Success : TickResult.Running;

    private static bool True(in BenchContext c) => true;

    private static bool False(in BenchContext c) => false;

    private static bool Periodic3(in BenchContext c) => (c.NowMs / 100) % 3 == 0;

    /// <summary>A tree containing every node type, for the allocation benchmark.</summary>
    public static BehaviourTree<BenchContext> EveryNodeType()
    {
        var n = Bt.For<BenchContext>();

        var root = n.PrioritySelector(
            "root",
            n.Sequence(
                "seq",
                n.Condition("seq-cond", Periodic3),
                n.Cooldown("cooldown", TimeSpan.FromMilliseconds(300), n.Do("cooldown-do", Succeed))
            ),
            n.PrioritySequence(
                "pseq",
                n.Condition("pseq-cond", True),
                // Wait sits under a Sequence, not directly under the reactive parent: it re-arms on
                // success, so re-evaluation from index 0 every tick would restart it forever.
                n.Sequence(
                    "gated",
                    n.Wait("wait", TimeSpan.FromMilliseconds(250)),
                    n.SimpleParallel(
                        "parallel",
                        SimpleParallelPolicy.BothMustSucceed,
                        n.AutoReset(
                            "autoreset",
                            n.Repeat("repeat", 3, n.Condition("repeat-cond", Periodic3))
                        ),
                        n.TimeLimit(
                            "timelimit",
                            TimeSpan.FromMilliseconds(500),
                            n.UntilFailed(
                                "untilfailed",
                                n.Inverter("invert", n.Condition("invert-cond", False))
                            )
                        )
                    )
                )
            ),
            n.Selector(
                "sel",
                n.RateLimiter(
                    "ratelimiter",
                    TimeSpan.FromMilliseconds(200),
                    n.Do("rl-do", Periodic)
                ),
                n.AlwaysSucceed(
                    "always-succeed",
                    n.AlwaysFail("always-fail", n.Do("af-do", Succeed))
                ),
                n.UntilSuccess("untilsuccess", n.Do("us-do", Periodic)),
                n.Chance("chance", 0.5, n.Do("chance-do", Succeed))
            ),
            n.RandomSelector("randsel", n.Do("rs-do-1", Run), n.Do("rs-do-2", Succeed)),
            n.RandomSequence("randseq", n.Do("rq-do-1", Succeed), n.Do("rq-do-2", Succeed))
        );

        return n.Build(root);
    }

    /// <summary>
    /// The consumer-shaped tree: a reactive <c>PrioritySelector</c> root over 3 branches, ~25
    /// nodes, with several leaves parked in Running so every tick re-evaluates the guards, resumes
    /// running subtrees, and drives the reset cascade when a branch is preempted.
    /// </summary>
    public static BehaviourTree<BenchContext> DeepReactive()
    {
        var n = Bt.For<BenchContext>();

        var root = n.PrioritySelector(
            "root",
            n.Sequence(
                "emergency",
                n.Condition("threatened", Periodic3),
                n.Cooldown(
                    "evade-cd",
                    TimeSpan.FromMilliseconds(400),
                    n.Sequence(
                        "evade",
                        n.Do("pick-exit", Succeed),
                        n.Do("run", Run),
                        n.Do("regroup", Succeed)
                    )
                )
            ),
            n.Sequence(
                "engage",
                n.Condition("has-target", True),
                n.SimpleParallel(
                    "strafe-and-fire",
                    SimpleParallelPolicy.BothMustSucceed,
                    n.Sequence("strafe", n.Wait("reposition", TimeSpan.FromMilliseconds(300))),
                    n.Sequence(
                        "fire",
                        n.RateLimiter(
                            "burst-limit",
                            TimeSpan.FromMilliseconds(150),
                            n.Do("shoot", Periodic)
                        ),
                        n.Do("track", Run)
                    )
                ),
                n.Do("settle", Succeed)
            ),
            n.Sequence(
                "patrol",
                n.Condition("route-known", True),
                n.Forever(
                    "patrol-loop",
                    n.Sequence(
                        "leg",
                        n.Do("advance", Run),
                        n.Wait("dwell", TimeSpan.FromMilliseconds(200)),
                        n.Do("scan", Succeed)
                    )
                )
            )
        );

        return n.Build(root);
    }

    /// <summary>A compact, representative "boss" tree for the throughput benchmark.</summary>
    public static BehaviourTree<BenchContext> Boss()
    {
        var n = Bt.For<BenchContext>();

        var root = n.PrioritySelector(
            "root",
            n.Sequence(
                "enrage",
                n.Condition("hp-low", Periodic3),
                n.Cooldown("enrage-cd", TimeSpan.FromSeconds(30), n.Do("cast-enrage", Succeed))
            ),
            n.Sequence(
                "attack",
                n.Condition("has-target", True),
                n.Wait("wind-up", TimeSpan.FromMilliseconds(500)),
                n.Do("strike", Succeed)
            ),
            n.Do("idle", Succeed)
        );

        return n.Build(root);
    }
}
