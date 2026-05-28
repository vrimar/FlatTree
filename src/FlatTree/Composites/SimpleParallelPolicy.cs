namespace FlatTree;

/// <summary>Completion policy for <see cref="SimpleParallel{TContext}"/>.</summary>
public enum SimpleParallelPolicy : byte
{
    /// <summary>Succeeds only when all children succeed; fails as soon as any child fails.</summary>
    BothMustSucceed,

    /// <summary>Succeeds as soon as any child succeeds; fails only when all children fail.</summary>
    OnlyOneMustSucceed,
}
