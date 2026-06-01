using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

#pragma warning disable S4144 // NUnit setup/teardown intentionally reset the same static fixtures.

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class CrayonsPopupTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DummyPopupShow.Reset();
        DummyPopupGenericTarget.Reset();
        DummyCrayonsPopupTarget.Reset();
        DynamicTextObservability.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        DummyPopupShow.Reset();
        DummyPopupGenericTarget.Reset();
        DummyCrayonsPopupTarget.Reset();
        DynamicTextObservability.ResetForTests();
    }

    [Test]
    public void HandleEvent_TranslatesCrayonsPrompts_WhenOwnerPatched()
    {
        using var patch = PatchDummyCrayonsTarget();
        var target = new DummyCrayonsPopupTarget();

        target.HandleEvent();

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("絵が3次元に広がり、実体化する。"));
            Assert.That(DummyCrayonsPopupTarget.LastWorldMapFailure, Is.EqualTo("ワールドマップではそれはできない。"));
            Assert.That(DummyPopupGenericTarget.LastAskStringMessage, Is.EqualTo("何を描きますか？"));
            Assert.That(DummyCrayonsPopupTarget.LastColorPickerPrompt, Is.EqualTo("何色で描きますか？"));
            Assert.That(DummyCrayonsPopupTarget.LastPickDirectionTitle, Is.EqualTo("色"));
            Assert.That(DummyCrayonsPopupTarget.LastNanocrayonFailure, Is.EqualTo("それを描けるほどの才能はない。"));
            Assert.That(DummyCrayonsPopupTarget.LastPrettyPicture, Is.EqualTo("きれいな絵を描いた。"));
        });
    }

    [Test]
    public void TranslateLiteral_LeavesUnknownEmptyAndMarkedValuesSafe()
    {
        Assert.Multiple(() =>
        {
            Assert.That(CrayonsPopupTranslationPatch.TranslateLiteralForTests("unknown prompt"), Is.EqualTo("unknown prompt"));
            Assert.That(CrayonsPopupTranslationPatch.TranslateLiteralForTests(string.Empty), Is.Empty);
            Assert.That(CrayonsPopupTranslationPatch.TranslateLiteralForTests("\u0001You draw a pretty picture."), Is.EqualTo("You draw a pretty picture."));
            Assert.That(
                CrayonsPopupTranslationPatch.TranslateLiteralForTests("{{Y|You draw a pretty picture.}}"),
                Is.EqualTo("{{Y|きれいな絵を描いた。}}"));
        });
    }

    private static IDisposable PatchDummyCrayonsTarget()
    {
        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);
        harmony.Patch(
            original: RequireMethod(typeof(DummyCrayonsPopupTarget), nameof(DummyCrayonsPopupTarget.HandleEvent)),
            transpiler: new HarmonyMethod(RequireMethod(typeof(CrayonsPopupTranslationPatch), nameof(CrayonsPopupTranslationPatch.Transpiler))));
        return new HarmonyScope(harmony, harmonyId);
    }

    private static MethodInfo RequireMethod(Type type, string methodName)
    {
        return AccessTools.Method(type, methodName)
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
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
