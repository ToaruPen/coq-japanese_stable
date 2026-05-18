using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class CookbookDisplayNameTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        Translator.ResetForTests();
        ScopedDictionaryLookup.ResetForTests();
        DynamicTextObservability.ResetForTests();
        DummyCookbookTarget.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        ScopedDictionaryLookup.ResetForTests();
    }

    [Test]
    public void GenerateCookbook_TranslatesDisplayName_WhenPatched()
    {
        WithPatchedGenerateCookbook(() =>
        {
            DummyCookbookTarget.DisplayNameToGenerate = "&gThe Garden Of Cooking";
            var target = new DummyCookbookTarget();

            target.GenerateCookbook();

            Assert.Multiple(() =>
            {
                Assert.That(target.ParentObject.Render.DisplayName, Is.EqualTo("&g料理の庭園"));
                Assert.That(HitCount(), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void GenerateCookbook_DoesNotRecordHit_ForDirectMarker()
    {
        WithPatchedGenerateCookbook(() =>
        {
            DummyCookbookTarget.DisplayNameToGenerate =
                "&g" + MessageFrameTranslator.DirectTranslationMarker + "The Garden Of Cooking";
            var target = new DummyCookbookTarget();

            target.GenerateCookbook();

            Assert.Multiple(() =>
            {
                Assert.That(target.ParentObject.Render.DisplayName, Is.EqualTo("&gThe Garden Of Cooking"));
                Assert.That(HitCount(), Is.Zero);
            });
        });
    }

    private static void WithPatchedGenerateCookbook(Action action)
    {
        var harmonyId = "qudjp.tests.cookbook-display-name." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(
                    typeof(DummyCookbookTarget),
                    nameof(DummyCookbookTarget.GenerateCookbook)),
                postfix: new HarmonyMethod(RequireMethod(
                    typeof(CookbookDisplayNameTranslationPatch),
                    nameof(CookbookDisplayNameTranslationPatch.Postfix),
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
            nameof(CookbookDisplayNameTranslationPatch),
            nameof(CookbookDisplayNameTranslationPatch) + ".RenderDisplayName");
    }

    private static MethodInfo RequireMethod(Type type, string name, params Type[] parameterTypes)
    {
        var method = AccessTools.Method(type, name, parameterTypes);
        Assert.That(method, Is.Not.Null, $"{type.FullName}.{name} not found");
        return method!;
    }
}

internal sealed class DummyCookbookTarget
{
    public static string DisplayNameToGenerate { get; set; } = "&gThe Garden Of Cooking";

    public CookbookDummyGameObject ParentObject { get; } = new();

    public static void Reset()
    {
        DisplayNameToGenerate = "&gThe Garden Of Cooking";
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void GenerateCookbook()
    {
        ParentObject.Render.DisplayName = DisplayNameToGenerate;
    }
}

internal sealed class CookbookDummyGameObject
{
    public CookbookDummyRender Render { get; } = new();
}

internal sealed class CookbookDummyRender
{
    public string DisplayName { get; set; } = string.Empty;
}
