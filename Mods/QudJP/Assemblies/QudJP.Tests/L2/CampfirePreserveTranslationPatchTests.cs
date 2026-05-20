using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class CampfirePreserveTranslationPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-campfire-preserve-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        ScopedDictionaryLookup.ResetForTests();
        CampfirePreserveTranslationPatch.ResetForTests();
        DummyPopupShow.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        CampfirePreserveTranslationPatch.ResetForTests();
        ScopedDictionaryLookup.ResetForTests();
        Translator.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [TestCase(
        "You preserved:\n\nan apple into 1 serving of dried apple.",
        "保存した:\n\nan appleを1食分のdried appleに保存した。")]
    [TestCase(
        "You preserved:\n\nSome {{r|raw boar meat}} into 3 serving of boar jerky.\nSome {{r|raw worm meat}} into 3 servings of worm jerky.",
        "保存した:\n\n{{r|生の猪肉}}少々を3食分の猪肉ジャーキーに保存した。\n{{r|生のワーム肉}}少々を3食分のワームジャーキーに保存した。")]
    public void Preserve_TranslatesGeneratedPreservedPopup_WhenOwnerPatched(string source, string expected)
    {
        WriteDisplayNameDictionary(
            ("raw boar meat", "生の猪肉"),
            ("boar jerky", "猪肉ジャーキー"),
            ("raw worm meat", "生のワーム肉"),
            ("worm jerky", "ワームジャーキー"));

        AssertPopupMessage(
            RequireMethod(typeof(DummyCampfirePreserveTarget), nameof(DummyCampfirePreserveTarget.Preserve)),
            source,
            expected);
    }

    [Test]
    public void PreserveExotic_TranslatesGeneratedPreservedPopup_WhenOwnerPatched()
    {
        WriteDisplayNameDictionary(
            ("phase fruit", "フェーズ果実"),
            ("phase preserves", "フェーズ保存食"));

        AssertPopupMessage(
            RequireMethod(typeof(DummyCampfirePreserveTarget), nameof(DummyCampfirePreserveTarget.PreserveExotic)),
            "You preserved:\n\n{{M|phase fruit}} into 3 servings of {{C|phase preserves}}.",
            "保存した:\n\n{{M|フェーズ果実}}を3食分の{{C|フェーズ保存食}}に保存した。");
    }

    [Test]
    public void CampfirePreserve_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);

            DummyPopupShow.Show("You preserved:\n\nan apple into 1 serving of dried apple.");

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("You preserved:\n\nan apple into 1 serving of dried apple."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void CampfirePreserve_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        AssertPopupMessage(
            RequireMethod(typeof(DummyCampfirePreserveTarget), nameof(DummyCampfirePreserveTarget.Preserve)),
            MessageFrameTranslator.MarkDirectTranslation("You preserved:\n\nan apple into 1 serving of dried apple."),
            "You preserved:\n\nan apple into 1 serving of dried apple.");
    }

    [Test]
    public void TryTranslateMessageLogMessage_StripsDirectMarkerWithoutRetranslating()
    {
        var handled = CampfirePreserveTranslationPatch.TryTranslateMessageLogMessage(
            MessageFrameTranslator.MarkDirectTranslation("You preserved:\n\nan apple into 1 serving of dried apple."),
            nameof(MessageLogPatch),
            "MessageLog",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(handled, Is.True);
            Assert.That(translated, Is.EqualTo("You preserved:\n\nan apple into 1 serving of dried apple."));
        });
    }

    [Test]
    public void CampfirePreserve_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        AssertPopupMessage(
            RequireMethod(typeof(DummyCampfirePreserveTarget), nameof(DummyCampfirePreserveTarget.Preserve)),
            string.Empty,
            string.Empty);
    }

    private static void AssertPopupMessage(MethodInfo ownerMethod, string source, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);
            PatchOwner(harmony, ownerMethod);

            var target = new DummyCampfirePreserveTarget
            {
                PopupMessageToSend = source,
            };
            _ = ownerMethod.Invoke(target, Array.Empty<object>());

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void PatchPopupShow(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(
                typeof(DummyPopupShow),
                nameof(DummyPopupShow.Show),
                typeof(string),
                typeof(string),
                typeof(string),
                typeof(bool),
                typeof(bool),
                typeof(bool),
                typeof(bool)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));
    }

    private static void PatchOwner(Harmony harmony, MethodInfo original)
    {
        harmony.Patch(
            original: original,
            prefix: new HarmonyMethod(RequireMethod(typeof(CampfirePreserveTranslationPatch), nameof(CampfirePreserveTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(typeof(CampfirePreserveTranslationPatch), nameof(CampfirePreserveTranslationPatch.Finalizer), typeof(Exception))));
    }

    private static MethodInfo RequireMethod(Type type, string name, params Type[] parameters)
    {
        if (parameters.Length == 0)
        {
            var methodByName = type.GetMethod(
                name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
            Assert.That(methodByName, Is.Not.Null, $"{type.FullName}.{name} not found");
            return methodByName!;
        }

        var method = type.GetMethod(
            name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance,
            binder: null,
            types: parameters,
            modifiers: null);
        Assert.That(method, Is.Not.Null, $"{type.FullName}.{name} not found");
        return method!;
    }

    private static string CreateHarmonyId() => $"qudjp.tests.{Guid.NewGuid():N}";

    private void WriteDisplayNameDictionary(params (string key, string text)[] entries)
    {
        WriteDictionaryFile("ui-displayname-atomic.ja.json", entries);
    }

    private void WriteDictionaryFile(string fileName, params (string key, string text)[] entries)
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
            Path.Combine(tempDirectory, fileName),
            builder.ToString(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        ScopedDictionaryLookup.ResetForTests();
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
    }
}
