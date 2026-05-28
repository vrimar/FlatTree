# FlatTree

A zero/low-allocation Behaviour Tree library for C# (`net10.0`), designed for game
servers running hundreds–thousands of AI agents.

## Why

The natural fit for AI decision-making (wandering bots, multi-phase bosses) is a
Behaviour Tree. Classic implementations heap-allocate **one node object per node per
agent**, use per-node `IDisposable`, and throw exceptions for control flow. At MMO scale
(one tree per agent) that is a huge per-agent object graph and constant GC churn.

**Core idea:** separate the *immutable, shared* tree definition from *per-agent mutable*
state.

- Build **one** tree per archetype (boss type / bot type) **once** at startup; share that
  single tree across every agent of that type.
- Per-agent state is a single compact `NodeState[]`, indexed by node id.
- Ticking touches only that array + a stack-only context. **Zero per-tick allocation.**

## Allocation profile

| Scope | Allocation |
|---|---|
| per-archetype | 1 tree, built once at startup, shared by ALL agents of that type |
| per-agent activation | 1 `NodeState[]` (`new NodeState[NodeCount]`, 16 B/node) |
| per-tick | **0 bytes** (no LINQ / closures / enumerators / boxing on the hot path; `RandomSelector` shuffles via `stackalloc`) |

This holds because (a) the nested-factory node construction runs at **build time** (once
per archetype), not per agent or per tick; and (b) leaf delegates **capture nothing**
(`static` lambdas / method groups) so the compiler caches one shared instance. Capturing is
also a **correctness** rule, not just an allocation one: one node is shared by every agent, so
a delegate closing over per-agent state would leak that agent's state to all others.

## Authoring — nested factory

```csharp
BtFactory<Ctx> n = Bt.For<Ctx>(/* optional IRandomProvider */);

BehaviourTree<Ctx> tree = n.Build(
    n.PrioritySelector("root",
        n.Sequence("enrage",
            n.Condition("hp<20%", static c => c.Hp < c.MaxHp / 5),
            n.Cooldown("enrage-cd", TimeSpan.FromSeconds(30),
                n.Do("cast-enrage", Actions.CastEnrage))),
        n.Sequence("attack",
            n.Condition("has-target", Actions.HasTarget),
            n.Wait("wind-up", TimeSpan.FromMilliseconds(500)),
            n.Do("strike", Actions.Strike)),
        n.Do("idle", Actions.Idle)));

// per agent:
NodeState[] state = tree.NewState();

// per tick (0 B):
TickResult result = tree.Tick(state, new Ctx(blackboard, nowMs));
```

`Ctx` is any `readonly struct` (or class) implementing `IClock` (`long NowMs { get; }`).
Reusable subtrees are just methods returning `BtNode<Ctx>`.

- **Names are optional.** Every node method has a name-less overload (`n.Selector(...)`);
  the name is for debugging/inspection only and defaults to the node type.
- **Durations are `TimeSpan`** (`Wait`, `Cooldown`, `RateLimiter`, `TimeLimit`).
- **`Do`/`Condition` delegates return `TickResult`** (`Running`/`Success`/`Failure`) and `bool`
  respectively, and must capture nothing. A `Do` overload taking
  `(in Ctx, ref int cursor, ref long stamp)` exposes the node's per-agent scratch for
  multi-tick actions.
- **Deterministic randomness:** pass `Bt.For<Ctx>(new SeededRandomProvider(seed))` to make
  `RandomSelector`/`RandomSequence` orderings and `Chance` rolls reproducible.
- **Custom nodes:** subclass `BtNode<Ctx>`/`LeafNode<Ctx>`/`CompositeNode<Ctx>`/
  `DecoratorNode<Ctx>`; override the `protected` child-traversal hooks and your children are
  wired up by `Build`.

## Nodes

- **Composites:** `Selector`, `Sequence`, `PrioritySelector`, `PrioritySequence`,
  `RandomSelector`, `RandomSequence`, `SimpleParallel` (runs up to 16 children in parallel).
- **Decorators:** `Inverter`, `AlwaysSucceed`, `AlwaysFail`, `AutoReset`, `UntilSuccess`,
  `UntilFailed`, `Repeat`, `Cooldown`, `RateLimiter`, `TimeLimit`, `Chance`.
- **Leaves:** `Do`, `Condition`, `Wait`.

## Design constraints

- `net10.0`, `Nullable`, `TreatWarningsAsErrors`. Zero project dependencies.
- Single-threaded — no locks, no thread-safety.
- No wall-clock — time comes only from the context (`IClock.NowMs`), which makes the tree
  deterministically testable.

## Layout

- `src/FlatTree` — the library (no references).
- `tests/FlatTree.Tests` — TUnit + Shouldly test suite.
- `bench/FlatTree.Bench` — BenchmarkDotNet allocation + throughput benchmarks.

## License

MIT.
