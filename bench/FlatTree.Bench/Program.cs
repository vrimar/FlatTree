using System.Reflection;
using BenchmarkDotNet.Running;
using FlatTree.Bench;

if (args is ["--verify-naive", ..])
{
    return NaiveEquivalence.Verify(20_000, Console.Out) ? 0 : 1;
}

BenchmarkSwitcher.FromAssembly(Assembly.GetExecutingAssembly()).Run(args);
return 0;
