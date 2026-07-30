using System.Runtime.CompilerServices;

namespace FlatTree.Tests;

/// <summary>
/// The per-agent cost is one <c>NodeState</c> per node, so its size is part of the library's
/// contract rather than an implementation detail.
/// </summary>
public sealed class NodeStateLayoutTests
{
    [Test]
    public void NodeStateIs16Bytes()
    {
        Unsafe.SizeOf<NodeState>().ShouldBe(16);
    }

    [Test]
    public void NewStateAllocatesOneSlotPerNode()
    {
        BehaviourTree<FakeClock> tree = SampleTrees.EveryNodeType(out int nodeCount);

        tree.NewState().Length.ShouldBe(nodeCount);
        tree.NodeCount.ShouldBe(nodeCount);
    }

    [Test]
    public void AFreshSlotIsAllZero()
    {
        NodeState slot = default;

        slot.Status.ShouldBe(NodeStatus.Fresh);
        slot.Cursor.ShouldBe(0);
        slot.Stamp.ShouldBe(0L);
    }
}
