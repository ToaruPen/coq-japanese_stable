using System.Reflection;
using HarmonyLib;
using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class GolemQuestMoundDisplayOptionsTranslationPatchTests
{
    [Test]
    public void DisplayOptions_TranslatesBuildMenuItemChrome_WhenOwnerPatched()
    {
        using var patch = PatchDummyTarget();
        var target = new DummyGolemQuestMoundTarget();

        target.DisplayOptions();

        Assert.Multiple(() =>
        {
            Assert.That(target.ValidBuildText, Is.EqualTo("{{W|[Backspace]}} {{y|建造}}"));
            Assert.That(target.InvalidBuildText, Is.EqualTo("{{K|建造}}"));
        });
    }

    [Test]
    public void TranslateLiteral_LeavesUnknownEmptyAndMarkedValuesSafe()
    {
        Assert.Multiple(() =>
        {
            Assert.That(GolemQuestMoundDisplayOptionsTranslationPatch.TranslateLiteralForTests("option:-2"), Is.EqualTo("option:-2"));
            Assert.That(GolemQuestMoundDisplayOptionsTranslationPatch.TranslateLiteralForTests(string.Empty), Is.Empty);
            Assert.That(
                GolemQuestMoundDisplayOptionsTranslationPatch.TranslateLiteralForTests("\u0001{{K|Build}}"),
                Is.EqualTo("{{K|Build}}"));
        });
    }

    private static IDisposable PatchDummyTarget()
    {
        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);
        harmony.Patch(
            original: RequireMethod(typeof(DummyGolemQuestMoundTarget), nameof(DummyGolemQuestMoundTarget.DisplayOptions)),
            transpiler: new HarmonyMethod(RequireMethod(
                typeof(GolemQuestMoundDisplayOptionsTranslationPatch),
                nameof(GolemQuestMoundDisplayOptionsTranslationPatch.Transpiler))));
        return new HarmonyScope(harmony, harmonyId);
    }

    private static MethodInfo RequireMethod(Type type, string methodName)
    {
        return AccessTools.Method(type, methodName)
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private sealed class DummyGolemQuestMoundTarget
    {
        public string ValidBuildText { get; private set; } = string.Empty;

        public string InvalidBuildText { get; private set; } = string.Empty;

        public void DisplayOptions()
        {
            ValidBuildText = "{{W|[Backspace]}} {{y|Build}}";
            InvalidBuildText = "{{K|Build}}";
        }
    }

    private sealed class HarmonyScope : IDisposable
    {
        private readonly Harmony harmony;
        private readonly string harmonyId;

        public HarmonyScope(Harmony harmony, string harmonyId)
        {
            this.harmony = harmony;
            this.harmonyId = harmonyId;
        }

        public void Dispose()
        {
            harmony.UnpatchAll(harmonyId);
        }
    }
}
