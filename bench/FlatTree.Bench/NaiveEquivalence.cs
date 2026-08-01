namespace FlatTree.Bench;

/// <summary>
/// Verifies that <see cref="NaiveTrees.Boss"/> is tick-for-tick equivalent to
/// <see cref="BenchTrees.Boss"/>. <see cref="LayoutComparisonBenchmark"/> is only meaningful while
/// this holds — if the two shapes take different paths, the two sides are not doing equal work.
/// </summary>
public static class NaiveEquivalence
{
    public static bool Verify(int ticks, TextWriter output)
    {
        var tree = BenchTrees.Boss();
        var state = tree.NewState();
        var naive = NaiveTrees.Boss();

        for (int i = 1; i <= ticks; i++)
        {
            var ctx = new BenchContext(i * 50L);
            var flatResult = tree.Tick(state, in ctx);
            var naiveResult = naive.Tick(in ctx);

            if (flatResult != naiveResult)
            {
                output.WriteLine(
                    $"Divergence at tick {i} (t={ctx.NowMs}ms): "
                        + $"flat={flatResult}, naive={naiveResult}"
                );
                return false;
            }
        }

        output.WriteLine($"Equivalent across {ticks} ticks.");
        return true;
    }
}
