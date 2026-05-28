namespace FlatTree;

/// <summary>
/// A tiny deterministic SplitMix64 PRNG, re-seeded from a stored seed each tick so that the
/// SAME permutation is reproduced (used by <c>RandomSelector</c>/<c>RandomSequence</c> to
/// replay a per-agent visitation order without storing the whole order). This is NOT an
/// <see cref="IRandomProvider"/> — that interface is only used to DRAW the seed and for the
/// <c>Random</c> decorator's roll.
/// </summary>
internal struct DeterministicRng
{
    private ulong _state;

    public DeterministicRng(long seed)
    {
        // Force non-zero state. SplitMix64 tolerates 0 but a fixed non-zero floor keeps
        // seeds and streams well-defined regardless of caller input.
        _state = unchecked((ulong)seed);
        if (_state == 0)
        {
            _state = 0x9E3779B97F4A7C15UL;
        }
    }

    private ulong NextUInt64()
    {
        unchecked
        {
            _state += 0x9E3779B97F4A7C15UL;
            var z = _state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }

    public int Next(int maxExclusive)
    {
        Debug.Assert(maxExclusive > 0, "maxExclusive must be positive");
        // Unbiased enough for shuffling small child sets; avoids modulo-by-zero.
        return (int)(NextUInt64() % (ulong)maxExclusive);
    }

    public double NextDouble()
    {
        // Top 53 bits → a double in [0, 1).
        return (NextUInt64() >> 11) * (1.0 / (1UL << 53));
    }

    /// <summary>
    /// Draws a guaranteed-non-zero shuffle seed from an external <see cref="IRandomProvider"/>.
    /// A non-zero seed lets the per-tick consumer treat <c>Stamp == 0</c> as "unseeded"
    /// (lazy first shuffle).
    /// </summary>
    public static long DrawNonZeroSeed(IRandomProvider rng)
    {
        var seed = ((long)rng.Next(int.MaxValue) << 32) | (long)(uint)rng.Next(int.MaxValue);
        return seed != 0 ? seed : unchecked((long)0x9E3779B97F4A7C15UL);
    }

    /// <summary>
    /// Fills <paramref name="permutation"/> with the identity 0..N-1 and applies an
    /// in-place Fisher-Yates shuffle using this PRNG. Pass a <c>stackalloc</c> span to keep
    /// the operation allocation-free.
    /// </summary>
    public void FillShuffled(Span<int> permutation)
    {
        var n = permutation.Length;
        for (var i = 0; i < n; i++)
        {
            permutation[i] = i;
        }

        // Fisher-Yates shuffle.
        while (n > 1)
        {
            n--;
            var k = Next(n + 1);
            var value = permutation[k];
            permutation[k] = permutation[n];
            permutation[n] = value;
        }
    }
}
