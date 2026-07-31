# FlatTree

A zero/low-allocation Behaviour Tree library for C# (`net10.0`), designed for game
servers running hundreds–thousands of AI agents.

## Install

```sh
dotnet add package FlatTree
```

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
            n.Condition("hp<20%", static (in Ctx c) => c.Hp < c.MaxHp / 5),
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
  respectively, and must capture nothing. They take the context by `in`, so a lambda has to spell
  out the modifier and the type: `static (in Ctx c) => ...`. A `Do` overload taking
  `(in Ctx, ref int cursor, ref long stamp)` exposes the node's per-agent scratch for
  multi-tick actions; that scratch is cleared on reset, so an aborted action restarts rather than
  resuming mid-flight.
- **Deterministic randomness:** pass `Bt.For<Ctx>(new SeededRandomProvider(seed))` to make
  `RandomSelector`/`RandomSequence` orderings and `Chance` rolls reproducible.
- **Custom nodes:** subclass `BtNode<Ctx>`/`LeafNode<Ctx>`/`CompositeNode<Ctx>`/
  `DecoratorNode<Ctx>`; override the `protected` child-traversal hooks and your children are
  wired up by `Build`.

## Nodes

- **Composites:** `Selector`, `Sequence`, `PrioritySelector`, `PrioritySequence`,
  `RandomSelector`, `RandomSequence`, `SimpleParallel` (runs up to 16 children in parallel).
- **Decorators:** `Inverter`, `AlwaysSucceed`, `AlwaysFail`, `AutoReset`, `UntilSuccess`,
  `UntilFailed`, `Repeat`, `Forever`, `Cooldown`, `RateLimiter`, `TimeLimit`, `Chance`.
- **Leaves:** `Do`, `Condition`, `Wait`.

### Reactive vs resuming composites

This is the easiest thing to get wrong at authoring time:

| | while a child is `Running` |
|---|---|
| `Selector` / `Sequence` | resume at the stored `Cursor`. Earlier children are **not** re-ticked, so a higher-priority guard is **not** re-evaluated. |
| `PrioritySelector` / `PrioritySequence` | re-tick from child 0 every tick, and recursively `Reset` every lower-priority child once a higher-priority one takes over. |

Use a priority composite wherever a guard must be able to interrupt a running branch. Use the
plain ones where a started branch should be allowed to finish.

Because a priority composite re-ticks from child 0 every tick, a **self-re-arming leaf** (`Wait`, and
`Do` with the scratch overload) should not be a non-final child of one. It clears its progress on
success, so it reports Success on one tick and then restarts and reports Running on the next, which
resets everything behind it. Anything that finishes inside that single Success tick still runs, but
**multi-tick work behind it never accumulates the ticks it needs**. Put the tail under a `Sequence`,
which resumes from its cursor:

```csharp
// `channel` is reset every time it gets going, so it never completes
n.PrioritySequence("attack", n.Condition("has-target", ...), n.Wait("wind-up", ...), n.Do("channel", ...))

// correct: the guard stays reactive, the timed branch is allowed to progress
n.PrioritySequence("attack",
    n.Condition("has-target", ...),
    n.Sequence("swing", n.Wait("wind-up", ...), n.Do("channel", ...)))
```

### Aborting safely

`Reset` is the library's only abort notification, and it takes the context:

```csharp
protected override void DoReset(Span<NodeState> s, in Ctx ctx) { /* release what you acquired */ }
protected override void OnTerminate(Span<NodeState> s, TickResult status, in Ctx ctx) { }
```

A node preempted by a `PrioritySelector` learns of it through `DoReset` and can undo externally
visible work. `Reset` skips `Fresh` nodes, so an untouched branch costs nothing.

When a subtree must *never* be torn down mid-flight — an irreversible commit, a distributed
rendezvous — mark it and let `Build` enforce it:

```csharp
n.Uninterruptible(n.Sequence("commit", ...))
```

`Build` throws if such a node is reachable beneath anything that tears down a running child —
`PrioritySelector`, `PrioritySequence`, `SimpleParallel`, `TimeLimit`, or a custom node that
overrides `PreemptsRunningChildren`. The failure it prevents is silent, so it is a build-time error
rather than a convention.

### Recovering after an exception

`Tick` writes a node's status only once `Update` returns, so a node whose `Update` throws keeps the
status it had *before* that tick. Where that status was `Fresh` — the first tick, or a branch just
entered — the node reports `Fresh` over a subtree that is anything but, and `Reset` short-circuits
above the mess. (After a throw on a later tick the ancestors are still `Running`, so `Reset` does
cascade and recovers normally.) For the cases it cannot reach, `ResetAll` sweeps the flat node array
instead of cascading:

```csharp
try { tree.Tick(state, ctx); }
catch (Exception) { tree.ResetAll(state, ctx); }   // reaches the whole tree
```

It keeps state a node deliberately holds across a reset (see the table below). It differs from
`Reset` in two ways: no ancestor can hide a dirty subtree from it, **and it calls `DoReset` on every
node — including ones that never ticked**, since a node that acquired and then threw leaves a slot
that looks untouched. Cleanup hooks must therefore be idempotent and safe when nothing was acquired:

```csharp
protected override void DoReset(Span<NodeState> s, in Ctx ctx)
{
    if ((s[Id].Cursor & AcquiredFlag) == 0) { return; }   // nothing to release
    s[Id].Cursor &= ~AcquiredFlag;
    ctx.Release(...);
}
```

Because of that, `pool.Return(slot, ctx)` uses the ordinary `Reset`. Reach for
`pool.ResetAll(slot, ctx)` before returning a slot only when a tick actually threw.

### What `Reset` clears

`Reset` rewinds *traversal*, not the agent's history:

| State | On `Reset` |
|---|---|
| cursors, started flags, `Do` scratch, `Wait`/`TimeLimit` timers | cleared — they describe the activation being abandoned |
| `Cooldown` timer, `RateLimiter` interval and cached verdict | **kept** — they describe what the agent did, so preemption cannot refund them |

So a preempted branch cannot dodge its cooldown by being interrupted. For an agent that should start
with no history at all, take a fresh `NewState()` or a recycled `BehaviourTreePool` slot rather than
calling `Reset`.

With a pool, reset *before* returning a slot if a node may still hold a resource — `Rent` zeroes the
slot's memory without running any cleanup hook:

```csharp
pool.Return(slot, ctx);   // resets (cleanup runs), then frees
pool.Return(slot);        // frees only — for agents that hold nothing
```

## Design constraints

- `net10.0`, `Nullable`, `TreatWarningsAsErrors`. Zero project dependencies.
- Single-threaded — no locks, no thread-safety.
- No wall-clock — time comes only from the context (`IClock.NowMs`), which makes the tree
  deterministically testable.

## Layout

- `src/FlatTree` — the library (no references).
- `tests/FlatTree.Tests` — TUnit + Shouldly test suite.
- `bench/FlatTree.Bench` — BenchmarkDotNet allocation + throughput benchmarks.

## Releasing

Releases are cut by pushing a `v*` tag from `main`. The
[`release` workflow](.github/workflows/release.yml) derives the package version from
the tag (e.g. `v0.2.0` → `0.2.0`), runs the test suite, packs, and pushes to
nuget.org. Tags containing `-` (e.g. `v0.2.0-beta.1`) are published as prereleases.

```sh
git tag v0.2.0
git push origin v0.2.0
```

## License

MIT.
