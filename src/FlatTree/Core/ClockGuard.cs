namespace FlatTree;

internal static class ClockGuard
{
    internal static ClockSelector<TContext> Resolve<TContext>(ClockSelector<TContext>? clock)
        where TContext : IClock
    {
        if (clock is null)
        {
            return DefaultClock<TContext>.Selector;
        }

        BtGuard.RequireNoCapture(clock, nameof(clock));
        return clock;
    }
}
