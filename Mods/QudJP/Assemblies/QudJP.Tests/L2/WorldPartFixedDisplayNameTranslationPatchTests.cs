using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class WorldPartFixedDisplayNameTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
    }

    [Test]
    public void BeyLahReveal_TranslatesTerrainDisplayNameAndShortDescription_WhenPatched()
    {
        WithPatchedOwner(
            typeof(DummyBeyLahTerrain),
            nameof(DummyBeyLahTerrain.FireEvent),
            [typeof(string)],
            () =>
            {
                var owner = new DummyBeyLahTerrain();

                owner.FireEvent("BeyLahReveal");

                Assert.Multiple(() =>
                {
                    Assert.That(owner.ParentObject.Render.DisplayName, Is.EqualTo("ベイ・ラー"));
                    Assert.That(
                        owner.ParentObject.Description.Short,
                        Is.EqualTo("濃い林の中心で草木が開けている。花で飾られた小屋がその空き地に寄り集まり、整然としたウォーターヴァインの列とよく手入れされたラーに囲まれている。"));
                    Assert.That(HitCount(), Is.EqualTo(2));
                });
            });
    }

    [Test]
    public void HydroponReveal_TranslatesTerrainDisplayNameAndShortDescription_WhenPatched()
    {
        WithPatchedOwner(
            typeof(DummyHydroponTerrain),
            nameof(DummyHydroponTerrain.FireEvent),
            [typeof(string)],
            () =>
            {
                var owner = new DummyHydroponTerrain();

                owner.FireEvent("HydroponReveal");

                Assert.Multiple(() =>
                {
                    Assert.That(owner.ParentObject.Render.DisplayName, Is.EqualTo("ハイドロポン"));
                    Assert.That(owner.ParentObject.Description.Short, Is.EqualTo("ここがハイドロポンだ。"));
                    Assert.That(HitCount(), Is.EqualTo(2));
                });
            });
    }

    [TestCase(null, "脱皮中のバジリスクの抜け殻", "脱ぎ捨てられた皮は鈍い水晶のようで、彫像めいている。")]
    [TestCase("prey", "脱皮中のバジリスク", "水晶の鱗を持つトカゲが、芸術家の型の中にいるような静けさで横たわっている。獲物がその死んだような様子に油断して通り過ぎると、=pronouns.subjective=は=verb:quicken:afterpronoun=して雷鳴のように噛みつく。")]
    public void MoltingBasiliskSyncState_TranslatesDisplayNameAndShortDescription_WhenPatched(
        string? target,
        string expectedName,
        string expectedShort)
    {
        WithPatchedOwner(
            typeof(DummyMoltingBasilisk),
            nameof(DummyMoltingBasilisk.SyncState),
            Type.EmptyTypes,
            () =>
            {
                var owner = new DummyMoltingBasilisk();
                owner.ParentObject.Target = target;

                owner.SyncState();

                Assert.Multiple(() =>
                {
                    Assert.That(owner.ParentObject.DisplayName, Is.EqualTo(expectedName));
                    Assert.That(owner.ParentObject.Description.Short, Is.EqualTo(expectedShort));
                    Assert.That(HitCount(), Is.EqualTo(2));
                });
            });
    }

    [Test]
    public void SyncState_StripsDirectTranslationMarkerWithoutRecordingHit()
    {
        WithPatchedOwner(
            typeof(DummyDirectMarkedWorldPart),
            nameof(DummyDirectMarkedWorldPart.SyncState),
            Type.EmptyTypes,
            () =>
            {
                var owner = new DummyDirectMarkedWorldPart();

                owner.SyncState();

                Assert.Multiple(() =>
                {
                    Assert.That(owner.ParentObject.DisplayName, Is.EqualTo("molting basilisk"));
                    Assert.That(owner.ParentObject.Description.Short, Is.EqualTo("already routed"));
                    Assert.That(HitCount(), Is.Zero);
                });
            });
    }

    [Test]
    public void SyncState_LeavesUnknownFixedLeafUnchanged_WhenPatched()
    {
        WithPatchedOwner(
            typeof(DummyUnknownWorldPart),
            nameof(DummyUnknownWorldPart.SyncState),
            Type.EmptyTypes,
            () =>
            {
                var owner = new DummyUnknownWorldPart();

                owner.SyncState();

                Assert.Multiple(() =>
                {
                    Assert.That(owner.ParentObject.DisplayName, Is.EqualTo("unknown terrain"));
                    Assert.That(owner.ParentObject.Description.Short, Is.EqualTo("Unknown description."));
                    Assert.That(HitCount(), Is.Zero);
                });
            });
    }

    [Test]
    public void SyncState_HandlesEmptyDisplayNameAndDescription_WhenPatched()
    {
        WithPatchedOwner(
            typeof(DummyEmptyDisplayWorldPart),
            nameof(DummyEmptyDisplayWorldPart.SyncState),
            Type.EmptyTypes,
            () =>
            {
                var owner = new DummyEmptyDisplayWorldPart();

                owner.SyncState();

                Assert.Multiple(() =>
                {
                    Assert.That(owner.ParentObject.DisplayName, Is.Empty);
                    Assert.That(owner.ParentObject.Description.Short, Is.Empty);
                    Assert.That(HitCount(), Is.Zero);
                });
            });
    }

    [Test]
    public void SyncState_PreservesColorTagsInFixedDisplayName_WhenPatched()
    {
        WithPatchedOwner(
            typeof(DummyColorTaggedWorldPart),
            nameof(DummyColorTaggedWorldPart.SyncState),
            Type.EmptyTypes,
            () =>
            {
                var owner = new DummyColorTaggedWorldPart();

                owner.SyncState();

                Assert.Multiple(() =>
                {
                    Assert.That(owner.ParentObject.Render.DisplayName, Is.EqualTo("{{W|ベイ・ラー}}"));
                    Assert.That(
                        owner.ParentObject.Description.Short,
                        Is.EqualTo("{{Y|濃い林の中心で草木が開けている。花で飾られた小屋がその空き地に寄り集まり、整然としたウォーターヴァインの列とよく手入れされたラーに囲まれている。}}"));
                    Assert.That(HitCount(), Is.EqualTo(2));
                });
            });
    }

    private static void WithPatchedOwner(Type targetType, string targetMethodName, Type[] targetParameterTypes, Action action)
    {
        var harmonyId = "qudjp.tests.world-part-fixed-display-name." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(targetType, targetMethodName, targetParameterTypes),
                postfix: new HarmonyMethod(RequireMethod(
                    typeof(WorldPartFixedDisplayNameTranslationPatch),
                    nameof(WorldPartFixedDisplayNameTranslationPatch.Postfix),
                    typeof(object))));
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static int HitCount()
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            WorldPartFixedDisplayNameTranslationPatch.Context,
            WorldPartFixedDisplayNameTranslationPatch.Family);
    }

    private static MethodInfo RequireMethod(Type type, string name, params Type[] parameterTypes)
    {
        var method = AccessTools.Method(type, name, parameterTypes);
        Assert.That(method, Is.Not.Null, $"{type.FullName}.{name} not found");
        return method!;
    }
}

internal sealed class DummyBeyLahTerrain
{
    public FixedDisplayDummyGameObject ParentObject { get; } = new();

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool FireEvent(string eventId)
    {
        if (eventId == "BeyLahReveal")
        {
            ParentObject.Render.DisplayName = "Bey Lah";
            ParentObject.Description.Short = "At the center of a particularly thick copse, the vegetation clears. Flower-bedecked huts huddle in the clearing within, surrounded by phalanxes of tidy watervine rows and carefully-tended lah.";
        }

        return true;
    }
}

internal sealed class DummyHydroponTerrain
{
    public FixedDisplayDummyGameObject ParentObject { get; } = new();

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool FireEvent(string eventId)
    {
        if (eventId == "HydroponReveal")
        {
            ParentObject.Render.DisplayName = "Hydropon";
            ParentObject.Description.Short = "It's the hydropon.";
        }

        return true;
    }
}

internal sealed class DummyMoltingBasilisk
{
    public FixedDisplayDummyGameObject ParentObject { get; } = new();

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void SyncState()
    {
        if (ParentObject.Target is null)
        {
            ParentObject.Description.Short = "The sloughed off skin is dull quartz and statuesque.";
            ParentObject.DisplayName = "molting basilisk husk";
        }
        else
        {
            ParentObject.Description.Short = "A lizard of quartz scales reposes in the stillness of an artist's mould. When prey gets too comfortable with =pronouns.possessive= lifelessness and traipeses by, =pronouns.subjective= =verb:quicken:afterpronoun= and snaps like a thunder clap.";
            ParentObject.DisplayName = "molting basilisk";
        }
    }
}

internal sealed class DummyDirectMarkedWorldPart
{
    public FixedDisplayDummyGameObject ParentObject { get; } = new();

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void SyncState()
    {
        ParentObject.DisplayName = MessageFrameTranslator.DirectTranslationMarker + "molting basilisk";
        ParentObject.Description.Short = MessageFrameTranslator.DirectTranslationMarker + "already routed";
    }
}

internal sealed class DummyUnknownWorldPart
{
    public FixedDisplayDummyGameObject ParentObject { get; } = new();

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void SyncState()
    {
        ParentObject.DisplayName = "unknown terrain";
        ParentObject.Description.Short = "Unknown description.";
    }
}

internal sealed class DummyEmptyDisplayWorldPart
{
    public FixedDisplayDummyGameObject ParentObject { get; } = new();

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void SyncState()
    {
        ParentObject.DisplayName = string.Empty;
        ParentObject.Description.Short = string.Empty;
    }
}

internal sealed class DummyColorTaggedWorldPart
{
    public FixedDisplayDummyGameObject ParentObject { get; } = new();

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void SyncState()
    {
        ParentObject.Render.DisplayName = "{{W|Bey Lah}}";
        ParentObject.Description.Short =
            "{{Y|At the center of a particularly thick copse, the vegetation clears. Flower-bedecked huts huddle in the clearing within, surrounded by phalanxes of tidy watervine rows and carefully-tended lah.}}";
    }
}

internal sealed class FixedDisplayDummyGameObject
{
    public FixedDisplayDummyRender Render { get; } = new();

    public FixedDisplayDummyDescription Description { get; } = new();

    public string DisplayName { get; set; } = string.Empty;

    public object? Target { get; set; }

    public object? GetPart(string name)
    {
        return string.Equals(name, "Description", StringComparison.Ordinal) ? Description : null;
    }
}

internal sealed class FixedDisplayDummyRender
{
    public string DisplayName { get; set; } = string.Empty;
}

internal sealed class FixedDisplayDummyDescription
{
    public string Short { get; set; } = string.Empty;
}
