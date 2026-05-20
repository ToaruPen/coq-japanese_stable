using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class FriendOrFoeReasonTranslationPatchTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    private string tempDirectory = null!;
    private string dictionaryDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-friend-foe-reason-l2", Guid.NewGuid().ToString("N"));
        dictionaryDirectory = Path.Combine(tempDirectory, "dict");
        Directory.CreateDirectory(dictionaryDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(dictionaryDirectory);
        ScopedDictionaryLookup.ResetForTests();
        DynamicTextObservability.ResetForTests();
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(true);
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        ScopedDictionaryLookup.ResetForTests();
        DynamicTextObservability.ResetForTests();
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(null);

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [TestCase("insulting their suns", "太陽を侮辱した")]
    [TestCase("destroying the shining numbers", "輝く数を破壊した")]
    public void Postfix_TranslatesGeneratedFriendOrFoeReasons_WhenPatched(string source, string expected)
    {
        WriteDictionaryFile(
            "Scoped/historyspice-common.ja.json",
            ("suns", "太陽"),
            ("shining", "輝く"));

        var harmonyId = "qudjp-test-friend-foe-reason-" + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyFriendOrFoeReasonTarget), nameof(DummyFriendOrFoeReasonTarget.replacePlaceholders), typeof(string)),
                postfix: new HarmonyMethod(RequireMethod(typeof(FriendOrFoeReasonTranslationPatch), nameof(FriendOrFoeReasonTranslationPatch.Postfix), typeof(string).MakeByRefType())));

            var result = DummyFriendOrFoeReasonTarget.replacePlaceholders(source);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(expected));
                Assert.That(RouteHitCount(), Is.EqualTo(1));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_LeavesUnknownReasonsUnchanged_WhenPatched()
    {
        const string source = "unstructured dislike";
        WriteDictionaryFile("world-parts.ja.json", ("stealing a cherished heirloom", "大切な家宝を盗んだ"));
        var harmonyId = "qudjp-test-friend-foe-reason-" + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyFriendOrFoeReasonTarget), nameof(DummyFriendOrFoeReasonTarget.replacePlaceholders), typeof(string)),
                postfix: new HarmonyMethod(RequireMethod(typeof(FriendOrFoeReasonTranslationPatch), nameof(FriendOrFoeReasonTranslationPatch.Postfix), typeof(string).MakeByRefType())));

            var result = DummyFriendOrFoeReasonTarget.replacePlaceholders(source);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(source));
                Assert.That(RouteHitCount(), Is.Zero);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_LeavesEmptyReasonUnchanged_WhenPatched()
    {
        const string source = "";
        var harmonyId = "qudjp-test-friend-foe-reason-" + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyFriendOrFoeReasonTarget), nameof(DummyFriendOrFoeReasonTarget.replacePlaceholders), typeof(string)),
                postfix: new HarmonyMethod(RequireMethod(typeof(FriendOrFoeReasonTranslationPatch), nameof(FriendOrFoeReasonTranslationPatch.Postfix), typeof(string).MakeByRefType())));

            var result = DummyFriendOrFoeReasonTarget.replacePlaceholders(source);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(source));
                Assert.That(RouteHitCount(), Is.Zero);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_PreservesCaptureColorTags_WhenPatched()
    {
        WriteDictionaryFile("Scoped/historyspice-common.ja.json", ("suns", "太陽"));
        var harmonyId = "qudjp-test-friend-foe-reason-" + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyFriendOrFoeReasonTarget), nameof(DummyFriendOrFoeReasonTarget.replacePlaceholders), typeof(string)),
                postfix: new HarmonyMethod(RequireMethod(typeof(FriendOrFoeReasonTranslationPatch), nameof(FriendOrFoeReasonTranslationPatch.Postfix), typeof(string).MakeByRefType())));

            var result = DummyFriendOrFoeReasonTarget.replacePlaceholders("insulting their {{W|suns}}");

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("{{W|太陽}}を侮辱した"));
                Assert.That(RouteHitCount(), Is.EqualTo(1));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_StripsDirectMarkerWithoutRetranslating_WhenPatched()
    {
        WriteDictionaryFile("Scoped/historyspice-common.ja.json", ("suns", "太陽"));
        var harmonyId = "qudjp-test-friend-foe-reason-" + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyFriendOrFoeReasonTarget), nameof(DummyFriendOrFoeReasonTarget.replacePlaceholders), typeof(string)),
                postfix: new HarmonyMethod(RequireMethod(typeof(FriendOrFoeReasonTranslationPatch), nameof(FriendOrFoeReasonTranslationPatch.Postfix), typeof(string).MakeByRefType())));

            var result = DummyFriendOrFoeReasonTarget.replacePlaceholders(
                MessageFrameTranslator.DirectTranslationMarker + "insulting their suns");

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("insulting their suns"));
                Assert.That(RouteHitCount(), Is.EqualTo(1));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private void WriteDictionaryFile(string fileName, params (string Key, string Text)[] entries)
    {
        var path = Path.Combine(dictionaryDirectory, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var builder = new StringBuilder();
        builder.Append("{\"entries\":[");
        for (var index = 0; index < entries.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append("{\"key\":\"");
            builder.Append(EscapeJson(entries[index].Key));
            builder.Append("\",\"text\":\"");
            builder.Append(EscapeJson(entries[index].Text));
            builder.Append("\"}");
        }

        builder.Append("]}");
        builder.AppendLine();
        File.WriteAllText(path, builder.ToString(), Utf8WithoutBom);
    }

    private static MethodInfo RequireMethod(Type type, string name, params Type[] parameters)
    {
        return type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, parameters, null)
            ?? throw new MissingMethodException(type.FullName, name);
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\n")
            .Replace("\r", "\\r");
    }

    private static int RouteHitCount()
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(FriendOrFoeReasonTranslationPatch),
            nameof(FriendOrFoeReasonTranslationPatch) + ".replacePlaceholders");
    }
}
