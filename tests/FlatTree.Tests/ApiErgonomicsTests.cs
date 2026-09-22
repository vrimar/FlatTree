namespace FlatTree.Tests;

/// <summary>F4: name-less factory overloads, and F7: the stateful <c>Do</c> leaf.</summary>
public sealed class ApiErgonomicsTests
{
    [Test]
    public void NamelessOverloads_BuildAndTick_WithTypeNameAsDefaultName()
    {
        var n = Bt.For<FakeClock>();

        var root = n.Selector(
            n.Do(static (in FakeClock _) => TickResult.Failure),
            n.Cooldown(TimeSpan.FromMilliseconds(100), n.Do(static (in FakeClock _) => TickResult.Success))
        );
        var tree = n.Build(root);

        root.Name.ShouldBe("Selector");
        tree.Tick(tree.NewState(), new FakeClock()).ShouldBe(TickResult.Success);
    }

    [Test]
    public void NamelessAndNamedOverloads_ResolveWithoutAmbiguity()
    {
        var n = Bt.For<FakeClock>();

        var named = n.Do("explicit", static (in FakeClock _) => TickResult.Success);
        var nameless = n.Do(static (in FakeClock _) => TickResult.Success);

        named.Name.ShouldBe("explicit");
        nameless.Name.ShouldBe("Do");
    }

    [Test]
    public void LeafLambdas_NeedTheModifierButNotTheType()
    {
        var n = Bt.For<FakeClock>();
        var clock = new FakeClock();
        var h = new Harness(
            n,
            n.Sequence(
                n.Condition(static (in c) => c.NowMs >= 0),
                n.Condition(1L, static (in c, in at) => c.NowMs >= at),
                n.Act(static (in c) => c.Advance(0)),
                n.Do(static (in c) => TickResult.Success)
            ),
            clock
        );

        h.Tick().ShouldBe(TickResult.Failure);
        clock.Advance(1);
        h.Tick().ShouldBe(TickResult.Success);
    }

    [Test]
    public void NamelessOverloadsOfTheNewNodes_DefaultToTheTypeName()
    {
        var n = Bt.For<FakeClock>();
        var leaf = n.Do(static (in FakeClock _) => TickResult.Success);

        n.Act(static (in FakeClock _) => { }).Name.ShouldBe("Act");
        n.Condition(0, static (in FakeClock _, in int _) => true).Name.ShouldBe("Condition");
        n.WaitUntil(static (in FakeClock _) => true, TimeSpan.Zero).Name.ShouldBe("WaitUntil");
        n.OnComplete(leaf, static (in FakeClock _, TickResult r) => r)
            .Name.ShouldBe("OnComplete");
        n.While(static (in FakeClock _) => false, leaf, 1).Name.ShouldBe("While");
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
        h.CursorOf(sut).ShouldBe(1);
        h.Tick().ShouldBe(TickResult.Running);
        h.CursorOf(sut).ShouldBe(2);

        h.Tick().ShouldBe(TickResult.Success);

        // Completing re-arms the leaf, so the next run starts over rather than resuming at 3.
        h.CursorOf(sut).ShouldBe(0);
        h.Tick().ShouldBe(TickResult.Running);
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

                return TickResult.Running;
            }
        );
        var h = new Harness(n, sut, clock);

        clock.Advance(1234);
        h.Tick();

        h.StampOf(sut).ShouldBe(1234L);
    }
}
