using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using HarmonyLib;
using QudJP.Patches;

#pragma warning disable S4144 // Helper aliases intentionally share implementation to keep test cases readable.

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class GameObjectPossessiveDisplayNameTranslationPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "qudjp-possessive-display-name-l2",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        ScopedDictionaryLookup.ResetForTests();
        LocalizationAssetResolver.SetLocalizationRootForTests(tempDirectory);
        DynamicTextObservability.ResetForTests();
        WriteDictionaryFile(
            "ui-displayname-atomic.ja.json",
            ("laser rifle", "レーザーライフル"),
            ("snapjaw", "スナップジョー"));
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        ScopedDictionaryLookup.ResetForTests();
        LocalizationAssetResolver.SetLocalizationRootForTests(null);
        DynamicTextObservability.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [TestCase(nameof(DummyPossessiveDisplayNameTarget.Poss), "Your レーザーライフル", "あなたのレーザーライフル")]
    [TestCase(nameof(DummyPossessiveDisplayNameTarget.poss), "your レーザーライフル", "あなたのレーザーライフル")]
    [TestCase(nameof(DummyPossessiveDisplayNameTarget.Poss), "Your laser rifle", "あなたのレーザーライフル")]
    [TestCase(nameof(DummyPossessiveDisplayNameTarget.Poss), "Your \x01レーザーライフル", "あなたのレーザーライフル")]
    [TestCase(nameof(DummyPossessiveDisplayNameTarget.Poss), "{{Y|Your laser rifle}}", "{{Y|あなたのレーザーライフル}}")]
    [TestCase(nameof(DummyPossessiveDisplayNameTarget.Poss), "スナップジョー's 鋼の盾", "スナップジョーの鋼の盾")]
    [TestCase(nameof(DummyPossessiveDisplayNameTarget.Poss), "snapjaw's laser rifle", "スナップジョーのレーザーライフル")]
    [TestCase(nameof(DummyPossessiveDisplayNameTarget.Poss), "{{R|snapjaw}}'s {{Y|laser rifle}}", "{{R|スナップジョー}}の{{Y|レーザーライフル}}")]
    [TestCase(nameof(DummyPossessiveDisplayNameTarget.poss), "機械仕掛けのドア' 鍵", "機械仕掛けのドアの鍵")]
    public void Postfix_TranslatesPossessiveDisplayName_WhenPatched(
        string methodName,
        string source,
        string expected)
    {
        var harmonyId = "qudjp.tests.gameobject-possessive-display-name." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPossessiveDisplayNameTarget), methodName),
                postfix: new HarmonyMethod(RequireMethod(
                    typeof(GameObjectPossessiveDisplayNameTranslationPatch),
                    nameof(GameObjectPossessiveDisplayNameTranslationPatch.Postfix),
                    typeof(string).MakeByRefType())));

            DummyPossessiveDisplayNameTarget.NextResult = source;
            var result = (string)RequireMethod(typeof(DummyPossessiveDisplayNameTarget), methodName)
                .Invoke(null, new object?[] { new object(), true, null })!;

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(expected));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        GameObjectPossessiveDisplayNameTranslationPatch.Context,
                        GameObjectPossessiveDisplayNameTranslationPatch.Family),
                    Is.EqualTo(1));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_LeavesNonPossessiveDisplayNameUnchanged()
    {
        var result = InvokePatchedPossResult("レーザーライフル");

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo("レーザーライフル"));
            Assert.That(
                DynamicTextObservability.GetRouteFamilyHitCountForTests(
                    GameObjectPossessiveDisplayNameTranslationPatch.Context,
                    GameObjectPossessiveDisplayNameTranslationPatch.Family),
                Is.Zero);
        });
    }

    [Test]
    public void Postfix_StripsDirectMarkerFromUnknownDisplayName()
    {
        var result = InvokePatchedPossResult(MessageFrameTranslator.MarkDirectTranslation("レーザーライフル"));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo("レーザーライフル"));
        });
    }

    [Test]
    public void Postfix_LeavesEmptyDisplayNameUnchangedWithoutRecordingTransform()
    {
        var result = InvokePatchedPossResult(string.Empty);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Empty);
            Assert.That(
                DynamicTextObservability.GetRouteFamilyHitCountForTests(
                    GameObjectPossessiveDisplayNameTranslationPatch.Context,
                    GameObjectPossessiveDisplayNameTranslationPatch.Family),
                Is.Zero);
        });
    }

    private static string InvokePatchedPossResult(string source)
    {
        var harmonyId = "qudjp.tests.gameobject-possessive-display-name." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPossessiveDisplayNameTarget), nameof(DummyPossessiveDisplayNameTarget.Poss)),
                postfix: new HarmonyMethod(RequireMethod(
                    typeof(GameObjectPossessiveDisplayNameTranslationPatch),
                    nameof(GameObjectPossessiveDisplayNameTranslationPatch.Postfix),
                    typeof(string).MakeByRefType())));

            DummyPossessiveDisplayNameTarget.NextResult = source;
            return (string)RequireMethod(typeof(DummyPossessiveDisplayNameTarget), nameof(DummyPossessiveDisplayNameTarget.Poss))
                .Invoke(null, new object?[] { new object(), true, null })!;
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static MethodInfo RequireMethod(Type type, string methodName, params Type[] parameterTypes)
    {
        var method = parameterTypes.Length == 0
            ? AccessTools.Method(type, methodName)
            : AccessTools.Method(type, methodName, parameterTypes);
        return method
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private void WriteDictionaryFile(string relativePath, params (string key, string text)[] entries)
    {
        var path = Path.Combine(tempDirectory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var builder = new StringBuilder();
        builder.Append("{\"entries\":[");
        for (var i = 0; i < entries.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            builder.Append("{\"key\":\"");
            builder.Append(EscapeJson(entries[i].key));
            builder.Append("\",\"text\":\"");
            builder.Append(EscapeJson(entries[i].text));
            builder.Append("\"}");
        }

        builder.Append("]}\n");
        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        ScopedDictionaryLookup.ResetForTests();
    }

    private static string EscapeJson(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static class DummyPossessiveDisplayNameTarget
    {
        public static string NextResult { get; set; } = string.Empty;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string Poss(object Object, bool Definite = true, bool? IncludeAdjunctNoun = null)
        {
            _ = Object;
            _ = Definite;
            _ = IncludeAdjunctNoun;
            return NextResult;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string poss(object Object, bool Definite = true, bool? IncludeAdjunctNoun = null)
        {
            _ = Object;
            _ = Definite;
            _ = IncludeAdjunctNoun;
            return NextResult;
        }
    }
}
