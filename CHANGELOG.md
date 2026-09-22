# Changelog

All notable changes to this project are documented here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project
adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.4.0] - 2026-09-22

### Breaking

- `n.Condition(name, null)` no longer compiles: `null` is ambiguous between the predicate and the new
  `state` overload. Cast it to `LeafPredicate<TContext>`.

### Added

- `Condition` and `Do` overloads taking a `state` the delegate receives by `in`, as `Catch` already
  did. Per-site data such as an expected value otherwise needed a custom leaf, since a delegate that
  closed over it is shared by every agent.
- `WaitUntil(predicate, timeout, onTimeout)`: waits for a predicate, with a handler that decides
  what a lapsed deadline reports; a `state` overload hands per-site data to both. `TimeLimit` over
  `UntilSuccess` cannot tell a timeout from a failure, and costs three nodes. A zero timeout waits
  forever, so it is rejected alongside a handler that could never run.
- `OnComplete(child, handler)`: hands the child's outcome to a handler on either arm, for work that
  must follow the child whether it succeeded or not. Completion only; abort cleanup stays in
  `DoReset`.
- `Act(effect)`: a leaf for a `void` effect that always succeeds. It is a separate name rather than
  a `Do` overload, because a `bool`-returning lambda would bind to the void delegate and silently
  succeed. A lambda whose value is a `TickResult` or `bool` does not compile against `Act`; a block
  body discards it on purpose.
- `BehaviourTree.TickOrRecover` and `BehaviourTreePool.TickOrRecover`: tick, and on a throw
  `ResetAll` before rethrowing. When cleanup fails as well, both failures are thrown as one
  `AggregateException`.
- `Repeat(count, child, exitWhen)`: ends at the first child success the predicate holds at, checked
  once the child is reset, as `Forever` does. A counted loop otherwise ignores a request to stop at
  an iteration boundary.
- `While(condition, body, maxIterationsPerTick)`: a loop checked between iterations. Instant
  iterations run back-to-back within a tick, so the loop fails once one tick spends the budget with
  the condition still true, rather than spin; iterations that span ticks are not capped.

### Changed

- `ForEach` derives from `LoopDecorator` and `Catch` from `OutcomeDecorator`, bases shared with
  `While` and `OnComplete` that hold the loop and handler logic once. Like `WaitUntilLeaf` under
  both `WaitUntil` leaves, they cannot be subclassed outside the library.
- A `Catch` handler that returns anything but Success or Failure, `default` included, throws; only
  Running was rejected before.

## [0.3.1] - 2026-09-21

### Fixed

- `PollingLeaf` calls `Cancel` when its deadline ends the leaf with the work still outstanding. It
  previously only called it on a reset, so a leaf that timed out had its started flag cleared by
  `OnTerminate` and never released what `Begin` acquired — neither a later `Reset` nor `ResetAll`
  could reach it, since both test the flag. A subclass that already released on expiry, in
  `OnTimeout` or in a Failure `OnTerminate`, now gets both calls; `Cancel` is documented as
  idempotent, so a correct one is unaffected.
- `ForEach` ticks an iteration that is already in flight to completion before a shrunken count ends
  the loop. It previously returned Success the moment the count dropped to or below the cursor,
  leaving the body `Running` with its scratch intact beneath a completed loop, so the next
  activation resumed the body mid-flight instead of starting item 0 from zero — and a
  `PollingLeaf` body was abandoned without a `Cancel`. No new iteration starts after the one in
  flight.

## [0.3.0] - 2026-08-19

### Breaking

- `Wait` stores its deadline in `Stamp` rather than its start timestamp, so it can carry a duration
  drawn per activation. Nothing reads `Stamp` on the tick path but a test or tool that inspected the
  slot directly will see `start + duration`.
- The `Chance`, `RandomSelector` and `RandomSequence` constructors take a `RandomSource<TContext>`
  instead of an `IRandomProvider`. They are `internal`, so this only affects code that reached them
  through `InternalsVisibleTo`; `Bt.For(IRandomProvider)` is unchanged and still wraps one provider.
- `Bt.For<TContext>(null)` no longer compiles: `null` is ambiguous between the `IRandomProvider?`
  and `RandomSource<TContext>` overloads. Write `Bt.For<TContext>()` for the default provider.

### Added

- `Retry(attempts, child)` — the failure-side mirror of `Repeat`: retries until the child succeeds
  or the attempts are spent, then Failure. `UntilSuccess` was the only prior option and never gives
  up, so a bounded retry had to be written as N hand-copied subtrees.
- `Catch(child, handler)` — runs the child and hands its Failure to a handler that decides what the
  decorator reports. `AlwaysSucceed` swallows a failure with nowhere to record why.
- `ForEach(count, body, onIteration)` — a counted loop whose count comes from the context at tick
  time, publishing the index through an optional hook. `Repeat` fixes its count at build time.
- `Forever(child, exitWhen)` — ends the loop with Success at the first child completion the
  predicate holds at, so a caller can stop a loop at an iteration boundary instead of tearing it
  down mid-flight.
- `PollingLeaf<TContext>` — base for the start-once-then-poll-until-it-lands leaf: it owns the
  started flag, the start timestamp, the deadline and the scratch clearing, and calls `Cancel` when
  a started node is reset. Subclasses implement `Begin`/`Poll` and get `Cursor` bits 1-31 through
  `Scratch`/`SetScratch`, which read and write those bits unsigned — bit 31 carries a value, so an
  arithmetic shift would read anything at or above 2^30 back as negative.
- `ClockSelector<TContext>` on `Wait`, `TimeLimit`, `Cooldown`, `RateLimiter` and `PollingLeaf` —
  picks which clock the node paces on, for a context that carries more than one (a simulation clock
  and a wall clock). Defaults to `IClock.NowMs`, so existing call sites are unaffected.
- `Wait` accepts a `min`/`max` range, drawn per activation, and a `DurationOf<TContext>` accessor
  for a duration only the context knows.
- `Bt.For<TContext>(RandomSource<TContext>)` — sources randomness from each agent's own context
  rather than one tree-wide provider. A shared tree could not give its agents per-agent streams that
  replay, which is what a fleet needs.
- `BtGuard.RequireNoCapture` — the capture check `BtFactory` runs, now public so a custom node
  enforces the same rule without forking it. Its reflection is annotated with
  `[DynamicallyAccessedMembers]`; unannotated, a trimmed build could strip the fields the check
  inspects and pass a capturing delegate silently.
- `NodeScratch` — extension methods over a node's own `NodeState` slot for the started-flag and
  start-timestamp idiom that every timed node hand-rolled.
- `NodeSlab<T>`, `BehaviourTree.NewSlab<T>` and `BehaviourTree.NewSidecar<T>` — per-agent, per-node
  state of an arbitrary type, parallel to `NodeState[]` and indexed by the same node id and pool
  slot. `BehaviourTreePool` owned only half of the state of any agent whose custom nodes need more
  than the `Cursor`/`Stamp` scratch.
- `BtNode.Tag`, `BtFactory.Tagged` and `BehaviourTree.NodesWith(tag)` — find a node by role instead
  of by matching its name against a prefix convention.

## [0.2.0] - 2026-07-31

### Breaking

- `BehaviourTree.Nodes` returns `ReadOnlySpan<BtNode<TContext>>` instead of
  `IReadOnlyList<BtNode<TContext>>`. It exposed the array the tree indexes by `Id`, which a caller
  could cast back and mutate, desynchronising a node from its own state slot. Callers using LINQ
  over `Nodes`, passing it as `IEnumerable<>`, storing it in a field, or enumerating it inside an
  `async`/iterator method must copy it first. `.Count` becomes `.Length`.
- `CompositeNode.Children` returns `ReadOnlySpan<BtNode<TContext>>` instead of
  `BtNode<TContext>[]`, for the same reason and with the same migration.
- `RateLimiter` keeps its cached verdict across a reset. Previously the verdict was dropped while
  the interval timer was kept, so a gated node reported `Failure` for the rest of the interval.
  Because a composite resets its children whenever it completes, the cache was destroyed one tick
  after being filled and the documented caching never took effect beneath a composite.
- `Wait` rejects a positive sub-millisecond duration with `ArgumentOutOfRangeException`. It
  truncated to `0` ms, producing a leaf that succeeded on every tick. `Wait(TimeSpan.Zero)` is still
  accepted and still succeeds immediately.
- Leaf delegate validation rejects two cases it previously accepted: multicast delegates (only the
  last entry's target was inspected, hiding a capturing entry) and method groups on objects whose
  only instance state is private to a base type (`GetFields` does not return those).
- `BtFactory.Uninterruptible` throws `InvalidOperationException` when applied to a node already
  built into a tree. `Build` is what enforces the marker, so setting it afterwards protected
  nothing.
- `SimpleParallel` rejects a `SimpleParallelPolicy` outside the declared enum values. Such a value
  was silently treated as `OnlyOneMustSucceed`.

### Added

- `BehaviourTree.ResetAll` and `BehaviourTreePool.ResetAll` — reset that reaches nodes an ordinary
  `Reset` cannot. A node whose `Update` throws never has its status written, so where that status
  was `Fresh` it reports `Fresh` over a dirty subtree and the cascade short-circuits above it.
  `ResetAll` sweeps the flat node array instead, visiting children before parents so each node's
  cleanup runs once. It calls `DoReset` on **every** node, including ones that never ticked, so
  cleanup hooks must be idempotent and safe when nothing was acquired. A hook that throws no longer
  strands the remaining nodes: the sweep completes and failures are rethrown as an
  `AggregateException`.

### Fixed

- `BtNode.DoReset` now resets the node's children by default. A custom composite that did not
  override it left children non-`Fresh` beneath a `Fresh` parent, which every later `Reset`
  short-circuited past permanently.
- `CompositeNode` copies the children array it is given. It previously stored the caller's array, so
  a caller who retained theirs could null an element or swap in a node after `Build` — giving two
  nodes the same per-agent state slot — and the constructor's own validation only ever held for a
  snapshot it did not own.
- `BehaviourTreePool.Return(int, in TContext)` releases the slot even when a node's cleanup throws.
  It previously leaked the slot for the pool's lifetime, and after `Capacity` such failures `Rent`
  threw permanently.
- `BtFactory.Build` reports a null child as `ArgumentNullException` rather than failing with
  `NullReferenceException` mid-walk.
- `Wait` and `TimeLimit` clear their start timestamp on reset, matching the documented behaviour
  that their timers are cleared.

### Documentation

- `SeededRandomProvider`: one provider is shared by every tree and agent built from the same
  factory, and it is a single advancing stream, so replaying one agent or tree alone diverges. Use a
  separate factory and provider per unit you intend to replay.
- Corrected the exception-recovery section of the README: a throwing `Update` leaves the node's
  *previous* status in place, so `Reset` fails to reach the subtree only where that status was
  `Fresh` (the first tick, or a newly entered branch), not on every throw.

## [0.1.0] - 2026-06-04

Initial release.
