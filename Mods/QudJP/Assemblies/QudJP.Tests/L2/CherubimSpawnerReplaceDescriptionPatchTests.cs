using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class CherubimSpawnerReplaceDescriptionPatchTests
{
    [Test]
    public void Prefix_GuardsNoSpaceDisplayName_WhenPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyCherubimSpawnerTarget), nameof(DummyCherubimSpawnerTarget.ReplaceDescription)),
                prefix: new HarmonyMethod(RequireMethod(typeof(CherubimSpawnerReplaceDescriptionPatch), nameof(CherubimSpawnerReplaceDescriptionPatch.Prefix))));

            var gameObject = new DummyCherubimGameObject();
            gameObject.Render.DisplayName = "cherub";
            gameObject.SetxTag("TextFragments", "Skin", "gleaming");

            Assert.DoesNotThrow(() => DummyCherubimSpawnerTarget.ReplaceDescription(
                gameObject,
                "A *skin* *creatureType* with *features*.",
                "wings"));
            Assert.That(gameObject.DescriptionPart._Short, Is.EqualTo("A gleaming cherub with wings."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_PreservesOriginalBehavior_WhenDisplayNameContainsSpace()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyCherubimSpawnerTarget), nameof(DummyCherubimSpawnerTarget.ReplaceDescription)),
                prefix: new HarmonyMethod(RequireMethod(typeof(CherubimSpawnerReplaceDescriptionPatch), nameof(CherubimSpawnerReplaceDescriptionPatch.Prefix))));

            var gameObject = new DummyCherubimGameObject();
            gameObject.Render.DisplayName = "ape cherub";
            gameObject.SetxTag("TextFragments", "Skin", "gleaming");

            Assert.DoesNotThrow(() => DummyCherubimSpawnerTarget.ReplaceDescription(
                gameObject,
                "A *skin* *creatureType* with *features*.",
                "wings"));
            Assert.That(gameObject.DescriptionPart._Short, Is.EqualTo("A gleaming ape with wings."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_SkipsOriginalCrashPath_WhenDisplayNameIsEmpty()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyCherubimSpawnerTarget), nameof(DummyCherubimSpawnerTarget.ReplaceDescription)),
                prefix: new HarmonyMethod(RequireMethod(typeof(CherubimSpawnerReplaceDescriptionPatch), nameof(CherubimSpawnerReplaceDescriptionPatch.Prefix))));

            var gameObject = new DummyCherubimGameObject();
            gameObject.Render.DisplayName = string.Empty;
            gameObject.SetxTag("TextFragments", "Skin", "gleaming");

            Assert.DoesNotThrow(() => DummyCherubimSpawnerTarget.ReplaceDescription(
                gameObject,
                "A *skin* *creatureType* with *features*.",
                "wings"));
            Assert.That(gameObject.DescriptionPart._Short, Is.EqualTo(string.Empty));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_DoesNotReenterOriginal_WhenGuardedNoSpaceReplacementFails()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyCherubimSpawnerTarget), nameof(DummyCherubimSpawnerTarget.ReplaceDescription)),
                prefix: new HarmonyMethod(RequireMethod(typeof(CherubimSpawnerReplaceDescriptionPatch), nameof(CherubimSpawnerReplaceDescriptionPatch.Prefix))));

            var gameObject = new DummyCherubimGameObjectWithNullSkin();
            gameObject.Render.DisplayName = "cherub";

            Assert.DoesNotThrow(() => DummyCherubimSpawnerTarget.ReplaceDescription(
                gameObject,
                "A *skin* *creatureType* with *features*.",
                "wings"));
            Assert.That(gameObject.DescriptionPart._Short, Is.EqualTo(string.Empty));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static string CreateHarmonyId()
    {
        return $"qudjp.tests.{Guid.NewGuid():N}";
    }

    private static MethodInfo RequireMethod(Type type, string methodName)
    {
        return AccessTools.Method(type, methodName)
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }
}
