using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class ModScrollerOneTranslationPatchTests
{
    [Test]
    public void OnActivate_TranslatesDisabledScriptsPopup_WhenPatched()
    {
        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyModScrollerOneTarget), nameof(DummyModScrollerOneTarget.OnActivate)),
                transpiler: new HarmonyMethod(RequireMethod(typeof(ModScrollerOneTranslationPatch), nameof(ModScrollerOneTranslationPatch.Transpiler))));

            var target = new DummyModScrollerOneTarget();
            target.OnActivate();

            Assert.That(
                target.LastPopupMessage,
                Is.EqualTo("Sample Mod にはスクリプトが含まれていますが、オプションで永続的に無効化されています。\n{{K|(オプション->Mod->スクリプトModを許可)}}"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static MethodInfo RequireMethod(Type type, string methodName)
    {
        return AccessTools.Method(type, methodName)
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }
}
