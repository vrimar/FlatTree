# Changelog

All notable changes to this project are documented here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project
adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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
