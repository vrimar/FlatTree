namespace FlatTree.Tests;

/// <summary>F4: name-less factory overloads, and F7: the stateful <c>Do</c> leaf.</summary>
public sealed class ApiErgonomicsTests
{
    [Test]
    public void NamelessOverloads_BuildAndTick_WithTypeNameAsDefaultName()
    {
        var n = Bt.For<FakeClock>();

        var root = n.Selector(
            n.Do(static _ => TickResult.Failure),
            n.Cooldown(TimeSpan.FromMilliseconds(100), n.Do(static _ => TickResult.Success))
        );
        var tree = n.Build(root);

        root.Name.ShouldBe("Selector");
        tree.Tick(tree.NewState(), new FakeClock()).ShouldBe(TickResult.Success);
    }

    [Test]
    public void NamelessAndNamedOverloads_ResolveWithoutAmbiguity()
    {
        var n = Bt.For<FakeClock>();

        var named = n.Do("explicit", static _ => TickResult.Success);
        var nameless = n.Do(static _ => TickResult.Success);

        named.Name.ShouldBe("explicit");
        nameless.Name.ShouldBe("Do");
    }

    [Test]
    public void StatefulDo_PersistsProgressInCursorAcrossTicks()
    {
        var n = Bt.For<FakeClock>();
        var sut = n.Do(
            "count",
            static (in FakeClock c, ref int cursor, ref long stamp) =>
            {
                cursor++;
                return cursor >= 3 ? TickResult.Success : TickResult.Running;
            }
        );
        var h = new Harness(n, sut);

        h.Tick().ShouldBe(TickResult.Running);
        h.Tick().ShouldBe(TickResult.Running);
        h.Tick().ShouldBe(TickResult.Success);
        h.CursorOf(sut).ShouldBe(3);
    }

    [Test]
    public void StatefulDo_CanRecordATimestampInStamp()
    {
        var n = Bt.For<FakeClock>();
        var clock = new FakeClock();
        var sut = n.Do(
            "stamp-on-first",
            static (in FakeClock c, ref int cursor, ref long stamp) =>
            {
                if (cursor == 0)
                {
                    cursor = 1;
                    stamp = c.NowMs;
                }

                return TickResult.Success;
            }
        );
        var h = new Harness(n, sut, clock);

        clock.Advance(1234);
        h.Tick();

        h.StampOf(sut).ShouldBe(1234L);
    }
}
