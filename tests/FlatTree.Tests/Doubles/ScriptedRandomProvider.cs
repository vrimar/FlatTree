namespace FlatTree.Tests.Doubles;

/// <summary>
/// Deterministic <see cref="IRandomProvider"/> for tests. <see cref="NextDouble"/> returns a
/// fixed, settable value; <see cref="Next"/> dequeues scripted values, falling back to a fixed
/// default once the queue is empty.
/// </summary>
public sealed class ScriptedRandomProvider : IRandomProvider
{
    private readonly Queue<int> _nextInts = new Queue<int>();
    private double _nextDouble;
    private int _defaultInt;

    public void SetNextDouble(double value) => _nextDouble = value;

    public void SetDefaultNext(int value) => _defaultInt = value;

    public void EnqueueNext(params int[] values)
    {
        foreach (int value in values)
        {
            _nextInts.Enqueue(value);
        }
    }

    public double NextDouble() => _nextDouble;

    public int Next(int maxExclusive)
    {
        int value = _nextInts.Count > 0 ? _nextInts.Dequeue() : _defaultInt;
        return value % maxExclusive;
    }
}
