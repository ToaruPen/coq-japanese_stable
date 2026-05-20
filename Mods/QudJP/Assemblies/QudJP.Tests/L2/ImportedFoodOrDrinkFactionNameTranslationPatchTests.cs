using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class ImportedFoodOrDrinkFactionNameTranslationPatchTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    private string tempDirectory = null!;
    private string dictionaryDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-imported-food-drink-faction-name-l2", Guid.NewGuid().ToString("N"));
        dictionaryDirectory = Path.Combine(tempDirectory, "dict");
        Directory.CreateDirectory(dictionaryDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(dictionaryDirectory);
        ScopedDictionaryLookup.ResetForTests();
        DynamicTextObservability.ResetForTests();
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(true);
        DummyImportedFoodOrDrinkTarget.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        ScopedDictionaryLookup.ResetForTests();
        DynamicTextObservability.ResetForTests();
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(null);
        DummyImportedFoodOrDrinkTarget.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void Postfix_TranslatesGeneratedFactionName_WhenPatched()
    {
        WriteDictionaryFile(
            "Scoped/historyspice-common.ja.json",
            ("honeyed", "ハチミツ風味の"),
            ("bread", "パン"));
        WriteDictionaryFile("world-gospels.ja.json", ("cult", "教団"));
        DummyImportedFoodOrDrinkTarget.FactionNameResult = "Cult of the Honeyed Bread";
        var harmonyId = "qudjp-test-imported-food-drink-faction-name-" + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyImportedFoodOrDrinkTarget), nameof(DummyImportedFoodOrDrinkTarget.generateFactionName), typeof(string)),
                postfix: new HarmonyMethod(RequireMethod(typeof(ImportedFoodOrDrinkFactionNameTranslationPatch), nameof(ImportedFoodOrDrinkFactionNameTranslationPatch.Postfix), typeof(string).MakeByRefType())));

            var result = DummyImportedFoodOrDrinkTarget.generateFactionName("Honeyed Bread");

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("ハチミツ風味のパンの教団"));
                Assert.That(RouteHitCount(), Is.EqualTo(1));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_LeavesNonMatchingFactionNameUnchanged_WhenPatched()
    {
        WriteDictionaryFile("Scoped/historyspice-common.ja.json", ("honeyed", "ハチミツ風味の"));
        WriteDictionaryFile("world-gospels.ja.json", ("cult", "教団"));
        DummyImportedFoodOrDrinkTarget.FactionNameResult = "Honeyed Bread";
        var harmonyId = "qudjp-test-imported-food-drink-faction-name-" + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyImportedFoodOrDrinkTarget), nameof(DummyImportedFoodOrDrinkTarget.generateFactionName), typeof(string)),
                postfix: new HarmonyMethod(RequireMethod(typeof(ImportedFoodOrDrinkFactionNameTranslationPatch), nameof(ImportedFoodOrDrinkFactionNameTranslationPatch.Postfix), typeof(string).MakeByRefType())));

            var result = DummyImportedFoodOrDrinkTarget.generateFactionName("Honeyed Bread");

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("Honeyed Bread"));
                Assert.That(RouteHitCount(), Is.Zero);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_HandlesFactionNameEdgeCases_WhenPatched()
    {
        var harmonyId = "qudjp-test-imported-food-drink-faction-name-" + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyImportedFoodOrDrinkTarget), nameof(DummyImportedFoodOrDrinkTarget.generateFactionName), typeof(string)),
                postfix: new HarmonyMethod(RequireMethod(typeof(ImportedFoodOrDrinkFactionNameTranslationPatch), nameof(ImportedFoodOrDrinkFactionNameTranslationPatch.Postfix), typeof(string).MakeByRefType())));

            DummyImportedFoodOrDrinkTarget.FactionNameResult = string.Empty;
            var empty = DummyImportedFoodOrDrinkTarget.generateFactionName("Honeyed Bread");
            var emptyHits = RouteHitCount();

            DummyImportedFoodOrDrinkTarget.FactionNameResult = "<color=red>Honeyed Bread</color>";
            var colored = DummyImportedFoodOrDrinkTarget.generateFactionName("Honeyed Bread");
            var coloredHits = RouteHitCount();

            DummyImportedFoodOrDrinkTarget.FactionNameResult = MessageFrameTranslator.DirectTranslationMarker + "Honeyed Bread";
            var marked = DummyImportedFoodOrDrinkTarget.generateFactionName("Honeyed Bread");
            var markedHits = RouteHitCount();

            Assert.Multiple(() =>
            {
                Assert.That(empty, Is.EqualTo(string.Empty));
                Assert.That(emptyHits, Is.Zero);
                Assert.That(colored, Is.EqualTo("<color=red>Honeyed Bread</color>"));
                Assert.That(coloredHits, Is.Zero);
                Assert.That(marked, Is.EqualTo("Honeyed Bread"));
                Assert.That(markedHits, Is.EqualTo(1));
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
        ScopedDictionaryLookup.ResetForTests();
    }

    private static int RouteHitCount() =>
        DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(ImportedFoodOrDrinkFactionNameTranslationPatch),
            ImportedFoodOrDrinkFactionNameTranslationPatch.Family);

    private static MethodInfo RequireMethod(Type type, string name, params Type[] parameters) =>
        AccessTools.Method(type, name, parameters)
        ?? throw new InvalidOperationException("Missing method " + type.FullName + "." + name);

    private static string EscapeJson(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
}
