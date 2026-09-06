using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class GameObjectLoadBlankNameRepairPatchTests
{
    private Harmony harmony = null!;

    [OneTimeSetUp]
    public void PatchLoad()
    {
        harmony = new Harmony($"qudjp.tests.saved-blank-item-load.{Guid.NewGuid():N}");
        harmony.Patch(
            AccessTools.Method(typeof(DummySavedBlankItemLoadTarget), nameof(DummySavedBlankItemLoadTarget.Load)),
            postfix: new HarmonyMethod(typeof(GameObjectLoadBlankNameRepairPatch), nameof(GameObjectLoadBlankNameRepairPatch.Postfix)));
    }

    [OneTimeTearDown]
    public void UnpatchLoad()
    {
        harmony.UnpatchAll(harmony.Id);
    }

    [Test]
    public void Load_RepairsRestoredNameAfterOriginalAndOnlyInvalidatesItsCache()
    {
        var item = new DummySavedBlankItemLoadTarget();
        var reader = new DummySavedBlankItemReader();
        reader.Property!["unrelated"] = "preserved";
        reader.IntProperty!["unrelated"] = 42;

        item.Load(reader);

        Assert.Multiple(() =>
        {
            Assert.That(item.NameAtOriginalLoadEnd, Is.EqualTo("{{r|}}"));
            Assert.That(item.Render!.DisplayName, Is.EqualTo("{{r|労働者用セキュリティカード}}"));
            Assert.That(item.ResetNameCacheCallCount, Is.EqualTo(2), "One original reset and one repair reset.");
            Assert.That(item.CachedName, Is.Null);
            Assert.That(item.HasProperNameReadCount, Is.Zero);
            Assert.That(item.Render, Is.SameAs(reader.Render));
            Assert.That(item.Render.ColorString, Is.EqualTo("&y"));
            Assert.That(item.Weight, Is.EqualTo(7));
            Assert.That(item.Property, Is.SameAs(reader.Property));
            Assert.That(item.IntProperty, Is.SameAs(reader.IntProperty));
            Assert.That(item.Property!["unrelated"], Is.EqualTo("preserved"));
            Assert.That(item.IntProperty!["unrelated"], Is.EqualTo(42));
        });
    }

    [Test]
    public void Load_RepairsLaterObjectsAndDoesNotReapplyToAnAlreadyRepairedObject()
    {
        var first = new DummySavedBlankItemLoadTarget();
        first.Load(new DummySavedBlankItemReader());
        var later = new DummySavedBlankItemLoadTarget();
        later.Load(new DummySavedBlankItemReader { Blueprint = "SalthopperMandible", DisplayName = "{{G|}}" });

        GameObjectLoadBlankNameRepairPatch.Postfix(first);
        Assert.Multiple(() =>
        {
            Assert.That(first.Render!.DisplayName, Is.EqualTo("{{r|労働者用セキュリティカード}}"));
            Assert.That(first.ResetNameCacheCallCount, Is.EqualTo(2));
            Assert.That(later.Render!.DisplayName, Is.EqualTo("{{G|ソルトホッパーの大顎}}"));
            Assert.That(later.ResetNameCacheCallCount, Is.EqualTo(2));
        });

        first.Load(new DummySavedBlankItemReader { DisplayName = first.Render!.DisplayName });
        Assert.Multiple(() =>
        {
            Assert.That(first.Render!.DisplayName, Is.EqualTo("{{r|労働者用セキュリティカード}}"));
            Assert.That(first.ResetNameCacheCallCount, Is.EqualTo(3), "Reloading a repaired name adds only the original reset.");
        });
    }

    [Test]
    public void Load_LeavesUnknownNormalCustomAndMissingDataUnchanged()
    {
        DummySavedBlankItemReader[] readers =
        [
            new() { Blueprint = "Unknown" },
            new() { Blueprint = null },
            new() { Blueprint = "" },
            new() { DisplayName = "bones" },
            new() { DisplayName = "{{Y|私の宝物}}" },
            new() { DisplayName = "{{r|労働者用セキュリティカード}}" },
            new() { DisplayName = "{{G|}}" },
            new() { DisplayName = "\u0001{{r|}}" },
            new() { DisplayName = "" },
            new() { DisplayName = null },
            new() { Render = null },
            new() { Property = null },
            new() { IntProperty = null },
        ];

        Assert.Multiple(() =>
        {
            foreach (var reader in readers)
            {
                var item = new DummySavedBlankItemLoadTarget();
                Assert.DoesNotThrow(() => item.Load(reader));
                Assert.That(item.Render?.DisplayName, Is.EqualTo(reader.Render is null ? null : reader.DisplayName));
                Assert.That(item.ResetNameCacheCallCount, Is.EqualTo(1));
                Assert.That(item.HasProperNameReadCount, Is.Zero);
            }
        });
    }

    [Test]
    public void Load_PreservesEverySavedRenamedOrProperNounFlagRegardlessOfValue()
    {
        foreach (var key in new[] { "Renamed", "ProperNoun" })
        {
            foreach (var value in new[] { "", "true", "false", "custom" })
            {
                var reader = new DummySavedBlankItemReader();
                reader.Property![key] = value;
                AssertProtected(reader, $"Property {key}={value}");
            }

            foreach (var value in new[] { -1, 0, 1, 2 })
            {
                var reader = new DummySavedBlankItemReader();
                reader.IntProperty![key] = value;
                AssertProtected(reader, $"IntProperty {key}={value}");
            }
        }
    }

    [Test]
    public void Postfix_FailsClosedForMissingMembersAndNeverReadsUnknownBlueprintParts()
    {
        var render = new DummySavedBlankItemRender { DisplayName = "{{r|}}" };
        var properties = new Dictionary<string, string>();
        var intProperties = new Dictionary<string, int>();
        object?[] incomplete =
        [
            null,
            new object(),
            new { Blueprint = "Red Security Card", Render = render },
            new { Blueprint = "Red Security Card", Render = render, Property = properties },
            new { Blueprint = "Red Security Card", Render = render, IntProperty = intProperties },
            new { Blueprint = "Red Security Card", Render = render, Property = properties, IntProperty = intProperties },
            new { Blueprint = "Red Security Card", Render = new object(), Property = properties, IntProperty = intProperties },
            new ThrowingRenderObject("Red Security Card"),
            new ThrowingRenderObject("Unknown"),
        ];

        Assert.Multiple(() =>
        {
            foreach (var item in incomplete)
            {
                Assert.DoesNotThrow(() => GameObjectLoadBlankNameRepairPatch.Postfix(item));
            }

            Assert.That(render.DisplayName, Is.EqualTo("{{r|}}"), "Missing ResetNameCache must prevent mutation.");
            Assert.That(((ThrowingRenderObject)incomplete[^1]!).RenderReadCount, Is.Zero, "Unknown IDs exit before part lookup.");
        });
    }

    private static void AssertProtected(DummySavedBlankItemReader reader, string reason)
    {
        var item = new DummySavedBlankItemLoadTarget();
        item.Load(reader);
        Assert.Multiple(() =>
        {
            Assert.That(item.Render!.DisplayName, Is.EqualTo("{{r|}}"), reason);
            Assert.That(item.ResetNameCacheCallCount, Is.EqualTo(1), reason);
            Assert.That(item.HasProperNameReadCount, Is.Zero, reason);
        });
    }

    private sealed class ThrowingRenderObject(string blueprint)
    {
        public string Blueprint { get; } = blueprint;
        public Dictionary<string, string> Property { get; } = new();
        public Dictionary<string, int> IntProperty { get; } = new();
        public int RenderReadCount { get; private set; }

        public object Render
        {
            get
            {
                RenderReadCount++;
                throw new InvalidOperationException("Injected Render lookup failure.");
            }
        }
    }
}
