using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class ModManagerUITranslationPatchTests
{
    [Test]
    public void OnSelect_TranslatesAuthorPrefix_WhenPatched()
    {
        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyModManagerUITarget), nameof(DummyModManagerUITarget.OnSelect)),
                postfix: new HarmonyMethod(RequireMethod(typeof(ModManagerUITranslationPatch), nameof(ModManagerUITranslationPatch.Postfix))));

            var target = new DummyModManagerUITarget();
            target.OnSelect("Example Author");

            Assert.That(target.SelectedModAuthor.Text, Is.EqualTo("{{C|作者: Example Author}}"));
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
