namespace FlatTree;

internal static class VerdictGuard
{
    internal static TickResult RequireComplete(TickResult verdict, string handler, string node)
    {
        if (verdict is not (TickResult.Success or TickResult.Failure))
        {
            ThrowNotComplete(verdict, handler, node);
        }

        return verdict;
    }

    [DoesNotReturn]
    private static void ThrowNotComplete(TickResult verdict, string handler, string node) =>
        throw new InvalidOperationException(
            $"The {handler} handler on '{node}' returned {verdict}; it must return Success or Failure."
        );
}
