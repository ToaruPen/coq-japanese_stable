using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class MutationsApiTranslationPatchTests
{
    private string tempDirectory = null!;
    private string patternFilePath = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-mutations-api-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        patternFilePath = Path.Combine(tempDirectory, "messages.ja.json");

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        MessagePatternTranslator.ResetForTests();
        MessagePatternTranslator.SetPatternFileForTests(patternFilePath);
        File.WriteAllText(patternFilePath, "{\"patterns\":[]}\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
        DummyPopupShow.Reset();
        DummyMutationsApiTarget.Reset();

        WriteDictionary(("mutation", "突然変異"));
    }

    [TearDown]
    public void TearDown()
    {
        DummyMutationsApiTarget.Reset();
        Translator.ResetForTests();
        MessagePatternTranslator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void BuyRandomMutation_TranslatesInsufficientPointsMessage_WhenPatched()
    {
        WithPatchedBuyRandomMutation(nameof(DummyPopupShow.Show), () =>
        {
            DummyMutationsApiTarget.FailureMessageToShow = "You don't have 4 mutation points!";

            _ = DummyMutationsApiTarget.BuyRandomMutation(new DummyGameObject(), 4, MutationTerm: "mutation");

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("突然変異ポイントが4ポイント足りない！"));
        });
    }

    [Test]
    public void BuyRandomMutation_TranslatesConfirmationMessage_WhenPatched()
    {
        WithPatchedBuyRandomMutation(nameof(DummyPopupShow.ShowYesNo), () =>
        {
            DummyMutationsApiTarget.ConfirmMessageToShow =
                "Are you sure you want to spend 4 mutation points to buy a new mutation?";

            _ = DummyMutationsApiTarget.BuyRandomMutation(new DummyGameObject(), 4, MutationTerm: "mutation");

            Assert.That(DummyPopupShow.LastShowYesNoMessage, Is.EqualTo("本当に4ポイントを消費して新しい突然変異を購入しますか？"));
        });
    }

    [Test]
    public void BuyRandomMutation_PreservesColorTagsInConfirmationMessage_WhenPatched()
    {
        WithPatchedBuyRandomMutation(nameof(DummyPopupShow.ShowYesNo), () =>
        {
            DummyMutationsApiTarget.ConfirmMessageToShow =
                "Are you sure you want to spend 4 mutation points to buy a new {{G|mutation}}?";

            _ = DummyMutationsApiTarget.BuyRandomMutation(new DummyGameObject(), 4, MutationTerm: "mutation");

            Assert.That(DummyPopupShow.LastShowYesNoMessage, Is.EqualTo("本当に4ポイントを消費して新しい{{G|突然変異}}を購入しますか？"));
        });
    }

    [Test]
    public void TryTranslatePopupMessage_FallsBackToEnglishTerm_WhenDictionaryMisses()
    {
        var ok = TryTranslatePopupMessageDuringMutationPurchase(
            "Are you sure you want to spend 4 mutation points to buy a new mystery mutation?",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("本当に4ポイントを消費して新しいmystery mutationを購入しますか？"));
        });
    }

    [Test]
    public void TryTranslatePopupMessage_ReturnsFalse_ForEmptyInput()
    {
        var ok = TryTranslatePopupMessageDuringMutationPurchase(string.Empty, out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.False);
            Assert.That(translated, Is.EqualTo(string.Empty));
        });
    }

    [Test]
    public void TryTranslatePopupMessage_PreservesColorTags_OnExactFallback()
    {
        var ok = TryTranslatePopupMessageDuringMutationPurchase(
            "Are you sure you want to spend 4 mutation points to buy a new {{G|mutation}}?",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("本当に4ポイントを消費して新しい{{G|突然変異}}を購入しますか？"));
        });
    }

    [Test]
    public void TryTranslatePopupMessage_ReturnsFalse_ForDirectTranslationMarker()
    {
        var ok = TryTranslatePopupMessageDuringMutationPurchase(
            "\u0001Are you sure you want to spend 4 mutation points to buy a new mutation?",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.False);
            Assert.That(translated, Is.EqualTo("\u0001Are you sure you want to spend 4 mutation points to buy a new mutation?"));
        });
    }

    private void WriteDictionary(params (string key, string text)[] entries)
    {
        var builder = new StringBuilder();
        builder.Append("{\"entries\":[");

        for (var index = 0; index < entries.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append("{\"key\":\"");
            builder.Append(EscapeJson(entries[index].key));
            builder.Append("\",\"text\":\"");
            builder.Append(EscapeJson(entries[index].text));
            builder.Append("\"}");
        }

        builder.Append("]}");
        builder.AppendLine();

        File.WriteAllText(
            Path.Combine(tempDirectory, "mutations-api-l2.ja.json"),
            builder.ToString(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);
    }

    private static MethodInfo RequireMethod(Type type, string methodName, params Type[] parameterTypes)
    {
        return parameterTypes.Length == 0
            ? AccessTools.Method(type, methodName) ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}")
            : AccessTools.Method(type, methodName, parameterTypes) ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private static void WithPatchedBuyRandomMutation(string popupMethodName, Action assertion)
    {
        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupShow), popupMethodName),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));
            harmony.Patch(
                original: RequireMethod(typeof(DummyMutationsApiTarget), nameof(DummyMutationsApiTarget.BuyRandomMutation)),
                prefix: new HarmonyMethod(RequireMethod(typeof(MutationsApiTranslationPatch), nameof(MutationsApiTranslationPatch.Prefix))),
                finalizer: new HarmonyMethod(RequireMethod(typeof(MutationsApiTranslationPatch), nameof(MutationsApiTranslationPatch.Finalizer), typeof(Exception))));

            assertion();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static bool TryTranslatePopupMessageDuringMutationPurchase(string source, out string translated)
    {
        MutationsApiTranslationPatch.Prefix();
        try
        {
            return MutationsApiTranslationPatch.TryTranslatePopupMessage(
                source,
                nameof(MutationsApiTranslationPatchTests),
                "Popup.ShowYesNo",
                out translated);
        }
        finally
        {
            _ = MutationsApiTranslationPatch.Finalizer(null);
        }
    }
}
