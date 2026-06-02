using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class EmbarkBuilderValidationPopupTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DummyPopupMessageTarget.Reset();
        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(GetRepositoryDictionaryDirectory());
        DynamicTextObservability.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        DummyPopupMessageTarget.Reset();
        DynamicTextObservability.ResetForTests();
    }

    [Test]
    public void CheckState_TranslatesErrorTitle_WhenOwnerPatched()
    {
        AssertPopup(
            "Choose a genotype.",
            "{{r|Error!}}",
            "Choose a genotype.",
            "{{r|エラー！}}",
            expectedOwnerHitCount: 1);
    }

    [Test]
    public void CheckState_TranslatesWarningTitleAndContinueSuffix_WhenOwnerPatched()
    {
        AssertPopup(
            "You have unspent attribute points.\n\nContinue anyway?",
            "{{W|Warning!}}",
            "You have unspent attribute points.\n\n続行しますか？",
            "{{W|警告！}}",
            expectedOwnerHitCount: 2);
    }

    [Test]
    public void CheckState_DoesNotTranslateFixedValidationPopup_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupMessage(harmony);

            new DummyPopupMessageTarget().ShowPopup(
                "You have unspent attribute points.\n\nContinue anyway?",
                title: "{{W|Warning!}}");

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupMessageTarget.LastMessage, Is.EqualTo("You have unspent attribute points.\n\nContinue anyway?"));
                Assert.That(DummyPopupMessageTarget.LastTitle, Is.EqualTo("{{W|警告！}}"));
                Assert.That(OwnerHitCount(), Is.Zero);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void CheckState_LeavesUnknownAndEmptyValidationPopup_WhenOwnerPatched()
    {
        AssertPopup(
            "Unrecognized validation body.",
            "Unrecognized validation title",
            "Unrecognized validation body.",
            "Unrecognized validation title",
            expectedOwnerHitCount: 0);

        AssertPopup(string.Empty, string.Empty, string.Empty, string.Empty, expectedOwnerHitCount: 0);
    }

    [Test]
    public void CheckState_DoesNotRetranslateDirectMarkedValidationMessage_WhenOwnerPatched()
    {
        AssertPopup(
            MessageFrameTranslator.MarkDirectTranslation("未使用の能力値ポイントがあります。\n\n続行しますか？"),
            MessageFrameTranslator.MarkDirectTranslation("{{W|警告！}}"),
            "未使用の能力値ポイントがあります。\n\n続行しますか？",
            "{{W|警告！}}",
            expectedOwnerHitCount: 0);
    }

    private static void AssertPopup(
        string source,
        string sourceTitle,
        string expected,
        string expectedTitle,
        int expectedOwnerHitCount)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupMessage(harmony);
            PatchOwner(harmony);

            DummyEmbarkBuilderValidationOwnerTarget.ShowPopupAsOwner(source, sourceTitle);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupMessageTarget.LastMessage, Is.EqualTo(expected));
                Assert.That(DummyPopupMessageTarget.LastTitle, Is.EqualTo(expectedTitle));
                Assert.That(OwnerHitCount(), Is.EqualTo(expectedOwnerHitCount));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void PatchPopupMessage(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupMessageTarget), nameof(DummyPopupMessageTarget.ShowPopup)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupMessageTranslationPatch), nameof(PopupMessageTranslationPatch.Prefix))));
    }

    private static void PatchOwner(Harmony harmony)
    {
        var prefix = new HarmonyMethod(RequireMethod(typeof(EmbarkBuilderValidationPopupTranslationPatch), nameof(EmbarkBuilderValidationPopupTranslationPatch.Prefix)))
        {
            priority = Priority.First,
        };
        harmony.Patch(
            original: RequireMethod(typeof(DummyEmbarkBuilderValidationOwnerTarget), nameof(DummyEmbarkBuilderValidationOwnerTarget.ShowPopupAsOwner)),
            prefix: prefix,
            finalizer: new HarmonyMethod(RequireMethod(
                typeof(EmbarkBuilderValidationPopupTranslationPatch),
                nameof(EmbarkBuilderValidationPopupTranslationPatch.Finalizer),
                typeof(Exception))));
    }

    private static System.Reflection.MethodInfo RequireMethod(Type type, string methodName, params Type[] parameterTypes)
    {
        if (parameterTypes.Length == 0)
        {
            return type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
        }

        return AccessTools.Method(type, methodName, parameterTypes)
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private static string CreateHarmonyId()
    {
        return $"qudjp.tests.{Guid.NewGuid():N}";
    }

    private static int OwnerHitCount()
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(PopupMessageTranslationPatch),
            "Popup.ProducerText." + nameof(EmbarkBuilderValidationPopupTranslationPatch));
    }

    private static string GetRepositoryDictionaryDirectory() =>
        Path.Combine(
            QudJP.Tests.L1.TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Localization",
            "Dictionaries");

    private static class DummyEmbarkBuilderValidationOwnerTarget
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ShowPopupAsOwner(string message, string title)
        {
            new DummyPopupMessageTarget().ShowPopup(message, null, null, null, null, title);
        }
    }
}
