using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class MerchantAdvertisementTextTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
        DummyMerchantRevealerTarget.Reset();
    }

    [Test]
    public void GenerateMerchantLocation_TranslatesBookText_WhenPatched()
    {
        WithPatchedGenerateMerchantLocation(() =>
        {
            DummyMerchantRevealerTarget.GenerateMerchantLocation();

            Assert.Multiple(() =>
            {
                Assert.That(
                    DummyMerchantRevealerTarget.BookText,
                    Is.EqualTo("{{|the chrome market}}へどうぞ。最高品質の商品を取りそろえています。\n\n所在地：5 parasangs north of Joppa。"));
                Assert.That(HitCount(), Is.EqualTo(1));
            });
        });

        var outsideOwner = DummyHistoricStringExpander.ExpandString("Come!\n\n5 parasangs north of Joppa.");
        Assert.That(outsideOwner, Is.EqualTo("Come!\n\n5 parasangs north of Joppa."));
    }

    [Test]
    public void GenerateMerchantLocation_StripsDirectMarkerWithoutObservabilityHit_WhenPatched()
    {
        WithPatchedGenerateMerchantLocation(() =>
        {
            DummyMerchantRevealerTarget.Template =
                MessageFrameTranslator.DirectTranslationMarker + "Come!\n\n5 parasangs north of Joppa.";

            DummyMerchantRevealerTarget.GenerateMerchantLocation();

            Assert.Multiple(() =>
            {
                Assert.That(DummyMerchantRevealerTarget.BookText, Is.EqualTo("Come!\n\n5 parasangs north of Joppa."));
                Assert.That(HitCount(), Is.Zero);
            });
        });
    }

    [Test]
    public void GenerateMerchantLocation_TranslatesBookTitle_WhenPostfixPatched()
    {
        var harmonyId = "qudjp.tests.merchant-advertisement-title." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(
                    typeof(DummyMerchantRevealerInstanceTarget),
                    nameof(DummyMerchantRevealerInstanceTarget.GenerateMerchantLocation)),
                postfix: new HarmonyMethod(RequireMethod(
                    typeof(MerchantAdvertisementTextTranslationPatch),
                    nameof(MerchantAdvertisementTextTranslationPatch.Postfix),
                    typeof(object))));

            var target = new DummyMerchantRevealerInstanceTarget();
            target.GenerateMerchantLocation();

            Assert.Multiple(() =>
            {
                Assert.That(
                    target.bookTitle,
                    Is.EqualTo("{{M|クユラミルの蒸留所, 伝説の樹液商}}の広告"));
                Assert.That(target.ParentObject.DisplayName, Is.EqualTo(target.bookTitle));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(MerchantAdvertisementTextTranslationPatch),
                        nameof(MerchantAdvertisementTextTranslationPatch) + ".BookTitle"),
                    Is.EqualTo(1));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void WithPatchedGenerateMerchantLocation(Action action)
    {
        var harmonyId = "qudjp.tests.merchant-advertisement-text." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(
                    typeof(DummyMerchantRevealerTarget),
                    nameof(DummyMerchantRevealerTarget.GenerateMerchantLocation)),
                transpiler: new HarmonyMethod(RequireMethod(
                    typeof(MerchantAdvertisementTextTranslationPatch),
                    nameof(MerchantAdvertisementTextTranslationPatch.Transpiler),
                    typeof(IEnumerable<CodeInstruction>))));
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
            nameof(MerchantAdvertisementTextTranslationPatch),
            nameof(MerchantAdvertisementTextTranslationPatch) + ".ExpandString");
    }

    private static MethodInfo RequireMethod(Type type, string name, params Type[] parameterTypes)
    {
        var method = AccessTools.Method(type, name, parameterTypes);
        Assert.That(method, Is.Not.Null, $"{type.FullName}.{name} not found");
        return method!;
    }
}

internal static class DummyMerchantRevealerTarget
{
    public static string Template { get; set; } =
        "Come to {{|the chrome market}} for the highest quality wares.\n\nLocated 5 parasangs north of Joppa.";

    public static string BookText { get; private set; } = string.Empty;

    public static void Reset()
    {
        Template = "Come to {{|the chrome market}} for the highest quality wares.\n\nLocated 5 parasangs north of Joppa.";
        BookText = string.Empty;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void GenerateMerchantLocation()
    {
        BookText = DummyHistoricStringExpander.ExpandString(Template);
    }
}

internal sealed class DummyMerchantRevealerInstanceTarget
{
    public DummyMerchantAdvertisementParent ParentObject { get; } = new();

    public string bookTitle = string.Empty;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void GenerateMerchantLocation()
    {
        bookTitle = "advertisement for "
            + MessageFrameTranslator.DirectTranslationMarker
            + "{{M|クユラミルの蒸留所, 伝説の樹液商}}";
        ParentObject.DisplayName = bookTitle;
    }
}

internal sealed class DummyMerchantAdvertisementParent
{
    public string DisplayName { get; set; } = string.Empty;
}
