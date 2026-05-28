namespace FlatTree.Tests;

/// <summary>F3: the public seedable provider makes a tree's randomness reproducible.</summary>
public sealed class SeededRandomProviderTests
{
    [Test]
    public void SameSeed_ProducesIdenticalStreams()
    {
        var a = new SeededRandomProvider(12345);
        var b = new SeededRandomProvider(12345);

        for (int i = 0; i < 50; i++)
        {
            a.Next(1000).ShouldBe(b.Next(1000));
            a.NextDouble().ShouldBe(b.NextDouble());
        }
    }

    [Test]
    public void DifferentSeeds_DivergeStreams()
    {
        var a = new SeededRandomProvider(1);
        var b = new SeededRandomProvider(2);

        var anyDifferent = false;
        for (int i = 0; i < 50; i++)
        {
            if (a.Next(1_000_000) != b.Next(1_000_000))
            {
                anyDifferent = true;
            }
        }

        anyDifferent.ShouldBeTrue();
    }

    [Test]
    public void NextDouble_StaysInUnitInterval()
    {
        var rng = new SeededRandomProvider(99);

        for (int i = 0; i < 100; i++)
        {
            var value = rng.NextDouble();
            value.ShouldBeGreaterThanOrEqualTo(0.0);
            value.ShouldBeLessThan(1.0);
        }
    }

    [Test]
    public void SeededRandomSelector_VisitsSameFirstChildAcrossTrees()
    {
        // Two trees fed identically-seeded providers visit children in the same order.
        var na = Bt.For<FakeClock>(new SeededRandomProvider(777));
        var nb = Bt.For<FakeClock>(new SeededRandomProvider(777));

        var childrenA = Enumerable
            .Range(0, 6)
            .Select(_ => new MockNode { ReturnStatus = TickResult.Running })
            .ToArray();
        var childrenB = Enumerable
            .Range(0, 6)
            .Select(_ => new MockNode { ReturnStatus = TickResult.Running })
            .ToArray();

        var a = new Harness(na, na.RandomSelector("A", childrenA));
        var b = new Harness(nb, nb.RandomSelector("B", childrenB));

        a.Tick();
        b.Tick();

        var indexA = Array.FindIndex(childrenA, c => c.UpdateCallCount == 1);
        var indexB = Array.FindIndex(childrenB, c => c.UpdateCallCount == 1);
        indexA.ShouldBe(indexB);
    }
}
