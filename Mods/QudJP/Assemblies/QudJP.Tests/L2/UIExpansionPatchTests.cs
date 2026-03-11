using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class UIExpansionPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-ui-expansion-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void GetDisplayName_TranslatesKnownDisplayName_WhenPatched()
    {
        WriteDictionary(("worn bronze sword", "使い込まれた青銅の剣"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyGetDisplayNameEvent), nameof(DummyGetDisplayNameEvent.GetFor)),
                postfix: new HarmonyMethod(RequireMethod(typeof(GetDisplayNamePatch), nameof(GetDisplayNamePatch.Postfix))));

            var result = DummyGetDisplayNameEvent.GetFor("worn bronze sword", "bronze sword");

            Assert.That(result, Is.EqualTo("使い込まれた青銅の剣"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void GetDisplayName_PassesThroughUnknownDisplayName_WhenPatched()
    {
        WriteDictionary(("known sword", "既知の剣"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyGetDisplayNameEvent), nameof(DummyGetDisplayNameEvent.GetFor)),
                postfix: new HarmonyMethod(RequireMethod(typeof(GetDisplayNamePatch), nameof(GetDisplayNamePatch.Postfix))));

            var result = DummyGetDisplayNameEvent.GetFor("unknown relic", "unknown relic");

            Assert.That(result, Is.EqualTo("unknown relic"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void GetDisplayName_PreservesColorCodes_WhenPatched()
    {
        WriteDictionary(("worn bronze sword", "使い込まれた青銅の剣"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyGetDisplayNameEvent), nameof(DummyGetDisplayNameEvent.GetFor)),
                postfix: new HarmonyMethod(RequireMethod(typeof(GetDisplayNamePatch), nameof(GetDisplayNamePatch.Postfix))));

            var result = DummyGetDisplayNameEvent.GetFor("{{C|worn bronze sword}}", "bronze sword");

            Assert.That(result, Is.EqualTo("{{C|使い込まれた青銅の剣}}"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void GetDisplayName_HandlesNullAndEmptyResult()
    {
        WriteDictionary(("placeholder", "プレースホルダー"));

        var emptyResult = string.Empty;
        GetDisplayNamePatch.Postfix(ref emptyResult);

        string nullResult = null!;
        GetDisplayNamePatch.Postfix(ref nullResult);

        Assert.Multiple(() =>
        {
            Assert.That(emptyResult, Is.EqualTo(string.Empty));
            Assert.That(nullResult, Is.Null);
        });
    }

    [Test]
    public void CharGen_TranslatesKnownText_WhenPatched()
    {
        WriteDictionary(("Choose Genotype", "遺伝型を選択"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyCharGenScreen), nameof(DummyCharGenScreen.GetHeaderText)),
                postfix: new HarmonyMethod(RequireMethod(typeof(CharGenLocalizationPatch), nameof(CharGenLocalizationPatch.Postfix))));

            var result = new DummyCharGenScreen("Choose Genotype").GetHeaderText();

            Assert.That(result, Is.EqualTo("遺伝型を選択"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void CharGen_PassesThroughUnknownText_WhenPatched()
    {
        WriteDictionary(("Choose Genotype", "遺伝型を選択"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyCharGenScreen), nameof(DummyCharGenScreen.GetHeaderText)),
                postfix: new HarmonyMethod(RequireMethod(typeof(CharGenLocalizationPatch), nameof(CharGenLocalizationPatch.Postfix))));

            var result = new DummyCharGenScreen("Unknown Chargen Text").GetHeaderText();

            Assert.That(result, Is.EqualTo("Unknown Chargen Text"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Inventory_TranslatesKnownText_WhenPatched()
    {
        WriteDictionary(("Total weight", "総重量"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyInventoryScreen), nameof(DummyInventoryScreen.GetCategoryLabel)),
                postfix: new HarmonyMethod(RequireMethod(typeof(InventoryLocalizationPatch), nameof(InventoryLocalizationPatch.Postfix))));

            var result = new DummyInventoryScreen("Total weight").GetCategoryLabel();

            Assert.That(result, Is.EqualTo("総重量"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Inventory_PassesThroughUnknownText_WhenPatched()
    {
        WriteDictionary(("Total weight", "総重量"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyInventoryScreen), nameof(DummyInventoryScreen.GetCategoryLabel)),
                postfix: new HarmonyMethod(RequireMethod(typeof(InventoryLocalizationPatch), nameof(InventoryLocalizationPatch.Postfix))));

            var result = new DummyInventoryScreen("Unknown Inventory Text").GetCategoryLabel();

            Assert.That(result, Is.EqualTo("Unknown Inventory Text"));
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

    private static MethodInfo RequireMethod(Type type, string methodName)
    {
        return AccessTools.Method(type, methodName)
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

        var path = Path.Combine(tempDirectory, "ui-expansion-l2.ja.json");
        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private sealed class DummyCharGenScreen
    {
        private readonly string headerText;

        public DummyCharGenScreen(string headerText)
        {
            this.headerText = headerText;
        }

        public string GetHeaderText()
        {
            return headerText;
        }
    }

    private sealed class DummyInventoryScreen
    {
        private readonly string categoryLabel;

        public DummyInventoryScreen(string categoryLabel)
        {
            this.categoryLabel = categoryLabel;
        }

        public string GetCategoryLabel()
        {
            return categoryLabel;
        }
    }
}
