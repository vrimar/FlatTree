namespace FlatTree.Tests;

/// <summary>Result-query helpers on <see cref="TickResult"/> and <see cref="NodeStatus"/>.</summary>
public sealed class StatusExtensionsTests
{
    [Test]
    public void TickResult_Predicates()
    {
        TickResult.Running.IsRunning().ShouldBeTrue();
        TickResult.Running.IsComplete().ShouldBeFalse();
        TickResult.Running.IsSuccess().ShouldBeFalse();

        TickResult.Success.IsSuccess().ShouldBeTrue();
        TickResult.Success.IsComplete().ShouldBeTrue();
        TickResult.Success.IsFailure().ShouldBeFalse();

        TickResult.Failure.IsFailure().ShouldBeTrue();
        TickResult.Failure.IsComplete().ShouldBeTrue();
        TickResult.Failure.IsRunning().ShouldBeFalse();
    }

    [Test]
    public void NodeStatus_Predicates()
    {
        NodeStatus.Fresh.IsFresh().ShouldBeTrue();
        NodeStatus.Fresh.IsComplete().ShouldBeFalse();

        NodeStatus.Running.IsRunning().ShouldBeTrue();
        NodeStatus.Running.IsComplete().ShouldBeFalse();

        NodeStatus.Success.IsSuccess().ShouldBeTrue();
        NodeStatus.Success.IsComplete().ShouldBeTrue();

        NodeStatus.Failure.IsFailure().ShouldBeTrue();
        NodeStatus.Failure.IsComplete().ShouldBeTrue();
    }
}
