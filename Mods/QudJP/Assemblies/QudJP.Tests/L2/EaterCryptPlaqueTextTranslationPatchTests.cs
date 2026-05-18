using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class EaterCryptPlaqueTextTranslationPatchTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    private string tempDirectory = null!;
    private string dictionaryDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-eater-crypt-plaque-l2", Guid.NewGuid().ToString("N"));
        dictionaryDirectory = Path.Combine(tempDirectory, "dict");
        Directory.CreateDirectory(Path.Combine(dictionaryDirectory, "Scoped"));

        Translator.ResetForTests();
        ScopedDictionaryLookup.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(dictionaryDirectory);
        DynamicTextObservability.ResetForTests();
        DummyEaterCryptPlaqueTarget.Reset();
        WriteHistorySpiceDictionary(
            ("family", "家"));
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        ScopedDictionaryLookup.ResetForTests();
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void GeneratePlaque_TranslatesExpandedFragments_WhenPatched()
    {
        WithPatchedGeneratePlaque(() =>
        {
            var text = DummyEaterCryptPlaqueTarget.GeneratePlaque();

            Assert.Multiple(() =>
            {
                Assert.That(text, Is.EqualTo("ここに眠る *familyCognomen*\n*familyName*の家\n知恵、*shortMarkov*"));
                Assert.That(HitCount(), Is.EqualTo(3));
            });
        });
    }

    [Test]
    public void GeneratePlaque_LeavesUnknownExpandedFragments_WhenPatched()
    {
        WithPatchedGeneratePlaque(() =>
        {
            DummyEaterCryptPlaqueTarget.Intro = "Plain plaque text.";
            DummyEaterCryptPlaqueTarget.Title = "Another unknown.";
            DummyEaterCryptPlaqueTarget.Words = "Still unknown.";

            var text = DummyEaterCryptPlaqueTarget.GeneratePlaque();

            Assert.Multiple(() =>
            {
                Assert.That(text, Is.EqualTo("Plain plaque text.\nAnother unknown.\nStill unknown."));
                Assert.That(HitCount(), Is.Zero);
            });
        });
    }

    private static void WithPatchedGeneratePlaque(Action action)
    {
        var harmonyId = "qudjp.tests.eater-crypt-plaque." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyEaterCryptPlaqueTarget), nameof(DummyEaterCryptPlaqueTarget.GeneratePlaque)),
                transpiler: new HarmonyMethod(RequireMethod(
                    typeof(EaterCryptPlaqueTextTranslationPatch),
                    nameof(EaterCryptPlaqueTextTranslationPatch.Transpiler),
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
            nameof(EaterCryptPlaqueTextTranslationPatch),
            nameof(EaterCryptPlaqueTextTranslationPatch) + ".ExpandString");
    }

    private void WriteHistorySpiceDictionary(params (string key, string text)[] entries)
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
            Path.Combine(dictionaryDirectory, "Scoped", "historyspice-common.ja.json"),
            builder.ToString(),
            Utf8WithoutBom);
    }

    private static MethodInfo RequireMethod(Type type, string name, params Type[] parameterTypes)
    {
        var method = AccessTools.Method(type, name, parameterTypes);
        Assert.That(method, Is.Not.Null, $"{type.FullName}.{name} not found");
        return method!;
    }

    private static string EscapeJson(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}

internal static class DummyEaterCryptPlaqueTarget
{
    public static string Intro { get; set; } = "Here Rests *familyCognomen*";

    public static string Title { get; set; } = "The Family of *familyName*";

    public static string Words { get; set; } = "Wisdom *markovSeed:is*";

    public static void Reset()
    {
        Intro = "Here Rests *familyCognomen*";
        Title = "The Family of *familyName*";
        Words = "Wisdom *markovSeed:is*";
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static string GeneratePlaque()
    {
        return DummyHistoricStringExpander.ExpandString(Intro) + "\n"
            + DummyHistoricStringExpander.ExpandString(Title) + "\n"
            + DummyHistoricStringExpander.ExpandString(Words);
    }
}
