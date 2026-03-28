using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class PopupShowSpaceTranslationPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-popup-show-space-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        MessagePatternTranslator.ResetForTests();
        DummyPopupShow.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        MessagePatternTranslator.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void Prefix_TranslatesLowHealthMessage()
    {
        WriteDictionary(("Your health has dropped below 40%!", "体力が40%を下回った！"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.ShowSpace)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowSpaceTranslationPatch), nameof(PopupShowSpaceTranslationPatch.Prefix), typeof(string).MakeByRefType(), typeof(string).MakeByRefType())));

            DummyPopupShow.ShowSpace("Your health has dropped below 40%!", "Warning");

            Assert.That(DummyPopupShow.LastShowSpaceMessage, Is.EqualTo("体力が40%を下回った！"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesBareDeathReasonAndTitle()
    {
        WriteDictionary(
            ("You died.", "あなたは死んだ。"),
            ("QudJP.DeathWrapper.KilledBy.Bare", "{killer}に殺された。"),
            ("snapjaw", "スナップジョー"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.ShowSpace)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowSpaceTranslationPatch), nameof(PopupShowSpaceTranslationPatch.Prefix), typeof(string).MakeByRefType(), typeof(string).MakeByRefType())));

            DummyPopupShow.ShowSpace("\n\nYou were killed by snapjaw.", "You died.");

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowSpaceMessage, Is.EqualTo("\n\nスナップジョーに殺された。"));
                Assert.That(DummyPopupShow.LastShowSpaceTitle, Is.EqualTo("あなたは死んだ。"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_LeavesUnknownShowSpaceMessageUnchanged()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.ShowSpace)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowSpaceTranslationPatch), nameof(PopupShowSpaceTranslationPatch.Prefix), typeof(string).MakeByRefType(), typeof(string).MakeByRefType())));

            const string source = "Untranslated show-space message";
            DummyPopupShow.ShowSpace(source, "Untranslated title");

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowSpaceMessage, Is.EqualTo(source));
                Assert.That(DummyPopupShow.LastShowSpaceTitle, Is.EqualTo("Untranslated title"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_LeavesEmptyShowSpaceMessageUnchanged()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.ShowSpace)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowSpaceTranslationPatch), nameof(PopupShowSpaceTranslationPatch.Prefix), typeof(string).MakeByRefType(), typeof(string).MakeByRefType())));

            DummyPopupShow.ShowSpace(string.Empty, string.Empty);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowSpaceMessage, Is.EqualTo(string.Empty));
                Assert.That(DummyPopupShow.LastShowSpaceTitle, Is.EqualTo(string.Empty));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_DirectMarker_StillStripped()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.ShowSpace)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowSpaceTranslationPatch), nameof(PopupShowSpaceTranslationPatch.Prefix), typeof(string).MakeByRefType(), typeof(string).MakeByRefType())));

            DummyPopupShow.ShowSpace("\u0001既に翻訳済みの本文", "\u0001既に翻訳済みのタイトル");

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowSpaceMessage, Is.EqualTo("既に翻訳済みの本文"));
                Assert.That(DummyPopupShow.LastShowSpaceTitle, Is.EqualTo("既に翻訳済みのタイトル"));
            });
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

    private static MethodInfo RequireMethod(Type type, string methodName, params Type[] parameterTypes)
    {
        if (parameterTypes.Length == 0)
        {
            return AccessTools.Method(type, methodName)
                ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
        }

        return AccessTools.Method(type, methodName, parameterTypes)
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private void WriteDictionary(params (string key, string text)[] entries)
    {
        var builder = new StringBuilder();
        builder.Append('{');
        builder.Append("\"entries\":[");

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
            Path.Combine(tempDirectory, "popup-show-space.ja.json"),
            builder.ToString(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
