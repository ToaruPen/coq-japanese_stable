using System.Reflection;
using HarmonyLib;
using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class TextFilterSpeechStatusTranslationPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-text-filter-speech-status-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        DynamicTextObservability.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void AngryPostfix_TranslatesInsertedAngryLeaves()
    {
        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyTextFiltersTarget), nameof(DummyTextFiltersTarget.Angry), typeof(string)),
                postfix: new HarmonyMethod(RequireMethod(
                    typeof(TextFiltersAngryTranslationPatch),
                    nameof(TextFiltersAngryTranslationPatch.Postfix),
                    typeof(string).MakeByRefType())));

            var result = DummyTextFiltersTarget.Angry("Stop.");

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("いや！ Stop. ぐああ！"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(TextFiltersAngryTranslationPatch),
                        "TextFilters.Angry"),
                    Is.EqualTo(1));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void LallatedPostfix_TranslatesCarriedSpeechTextOnly()
    {
        WriteDictionary(("hello there", "こんにちは"));
        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(
                    typeof(DummyTextFiltersTarget),
                    nameof(DummyTextFiltersTarget.Lallated),
                    typeof(string),
                    typeof(string)),
                postfix: new HarmonyMethod(RequireMethod(
                    typeof(TextFiltersLallatedTranslationPatch),
                    nameof(TextFiltersLallatedTranslationPatch.Postfix),
                    typeof(string),
                    typeof(string).MakeByRefType())));

            var result = DummyTextFiltersTarget.Lallated("hello there", "nya");

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("nya こんにちは nya"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(TextFiltersLallatedTranslationPatch),
                        "TextFilters.Lallated"),
                    Is.EqualTo(1));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void LallatedPostfix_LeavesUnknownSpeechTextUntouched()
    {
        var result = TextFilterSpeechStatusTranslator.TranslateLallated("nya unknown words nya", "unknown words");
        Assert.That(result, Is.EqualTo("nya unknown words nya"));
    }

    private void WriteDictionary(params (string key, string text)[] entries)
    {
        var builder = new System.Text.StringBuilder();
        builder.Append("{\"entries\":[");
        for (var index = 0; index < entries.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append("{\"key\":\"");
            builder.Append(entries[index].key);
            builder.Append("\",\"text\":\"");
            builder.Append(entries[index].text);
            builder.Append("\"}");
        }

        builder.Append("]}\n");
        File.WriteAllText(Path.Combine(tempDirectory, "text-filter-speech-status-l2.ja.json"), builder.ToString());
    }

    private static MethodInfo RequireMethod(Type type, string name, params Type[] parameters) =>
        type.GetMethod(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, parameters)
        ?? throw new MissingMethodException(type.FullName, name);

    private static class DummyTextFiltersTarget
    {
        public static string Angry(string phrase)
        {
            return "NO! " + phrase + " ARGH!";
        }

        public static string Lallated(string Text, string Noise)
        {
            return Noise + " " + Text + " " + Noise;
        }
    }
}
