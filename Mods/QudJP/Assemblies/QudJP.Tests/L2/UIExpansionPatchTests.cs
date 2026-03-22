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
    private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

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

        RunWithPostfixPatch(
            targetType: typeof(DummyGetDisplayNameEvent),
            targetMethodName: nameof(DummyGetDisplayNameEvent.GetFor),
            patchType: typeof(GetDisplayNamePatch),
            patchMethodName: nameof(GetDisplayNamePatch.Postfix),
            assertion: () =>
        {
            var result = DummyGetDisplayNameEvent.GetFor("worn bronze sword", "bronze sword");

            Assert.That(result, Is.EqualTo("使い込まれた青銅の剣"));
        });
    }

    [Test]
    public void GetDisplayName_AcceptsJapaneseEntry_WhenPatched()
    {
        WriteDictionary(("螺旋角", "ねじれた角"));

        RunWithPostfixPatch(
            targetType: typeof(DummyGetDisplayNameEvent),
            targetMethodName: nameof(DummyGetDisplayNameEvent.GetFor),
            patchType: typeof(GetDisplayNamePatch),
            patchMethodName: nameof(GetDisplayNamePatch.Postfix),
            assertion: () =>
        {
            var result = DummyGetDisplayNameEvent.GetFor("螺旋角", "螺旋角");

            Assert.That(result, Is.EqualTo("ねじれた角"));
        });
    }

    [Test]
    public void GetDisplayName_PassesThroughUnknownDisplayName_WhenPatched()
    {
        WriteDictionary(("known sword", "既知の剣"));

        RunWithPostfixPatch(
            targetType: typeof(DummyGetDisplayNameEvent),
            targetMethodName: nameof(DummyGetDisplayNameEvent.GetFor),
            patchType: typeof(GetDisplayNamePatch),
            patchMethodName: nameof(GetDisplayNamePatch.Postfix),
            assertion: () =>
        {
            var result = DummyGetDisplayNameEvent.GetFor("unknown relic", "unknown relic");

            Assert.That(result, Is.EqualTo("unknown relic"));
        });
    }

    [Test]
    public void GetDisplayName_PreservesColorCodes_WhenPatched()
    {
        WriteDictionary(("worn bronze sword", "使い込まれた青銅の剣"));

        RunWithPostfixPatch(
            targetType: typeof(DummyGetDisplayNameEvent),
            targetMethodName: nameof(DummyGetDisplayNameEvent.GetFor),
            patchType: typeof(GetDisplayNamePatch),
            patchMethodName: nameof(GetDisplayNamePatch.Postfix),
            assertion: () =>
        {
            var result = DummyGetDisplayNameEvent.GetFor("{{C|worn bronze sword}}", "bronze sword");

            Assert.That(result, Is.EqualTo("{{C|使い込まれた青銅の剣}}"));
        });
    }

    [Test]
    public void GetDisplayName_TranslatesMixedModifierAndJapaneseBase_WhenPatched()
    {
        WriteDictionary(("lacquered", "漆塗り"));

        RunWithPostfixPatch(
            targetType: typeof(DummyGetDisplayNameEvent),
            targetMethodName: nameof(DummyGetDisplayNameEvent.GetFor),
            patchType: typeof(GetDisplayNamePatch),
            patchMethodName: nameof(GetDisplayNamePatch.Postfix),
            assertion: () =>
        {
            var result = DummyGetDisplayNameEvent.GetFor("lacquered サンダル", "サンダル");

            Assert.That(result, Is.EqualTo("漆塗りサンダル"));
        });
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

        RunWithPostfixPatch(
            targetType: typeof(DummyCharGenScreen),
            targetMethodName: nameof(DummyCharGenScreen.GetHeaderText),
            patchType: typeof(CharGenLocalizationPatch),
            patchMethodName: nameof(CharGenLocalizationPatch.Postfix),
            assertion: () =>
        {
            var result = new DummyCharGenScreen("Choose Genotype").GetHeaderText();

            Assert.That(result, Is.EqualTo("遺伝型を選択"));
        });
    }

    [Test]
    public void CharGen_PassesThroughUnknownText_WhenPatched()
    {
        WriteDictionary(("Choose Genotype", "遺伝型を選択"));

        RunWithPostfixPatch(
            targetType: typeof(DummyCharGenScreen),
            targetMethodName: nameof(DummyCharGenScreen.GetHeaderText),
            patchType: typeof(CharGenLocalizationPatch),
            patchMethodName: nameof(CharGenLocalizationPatch.Postfix),
            assertion: () =>
        {
            var result = new DummyCharGenScreen("Unknown Chargen Text").GetHeaderText();

            Assert.That(result, Is.EqualTo("Unknown Chargen Text"));
        });
    }

    [Test]
    public void CharGen_TargetCandidate_IncludesTextGetter_ButExcludesTypeGetter()
    {
        var candidateMethod = typeof(CharGenLocalizationPatch).GetMethod(
            "IsTextReturningMethodCandidate",
            BindingFlags.NonPublic | BindingFlags.Static);
        var typeFilterMethod = typeof(CharGenLocalizationPatch).GetMethod(
            "IsCharGenType",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.That(candidateMethod, Is.Not.Null);
        Assert.That(typeFilterMethod, Is.Not.Null);

        var headerGetter = RequireMethod(typeof(DummyCharGenProperties), nameof(DummyCharGenProperties.get_HeaderText));
        var typeGetter = RequireMethod(typeof(DummyCharGenProperties), nameof(DummyCharGenProperties.get_type));
        var embarkTypeResult = (bool)typeFilterMethod!.Invoke(null, new object[] { typeof(DummyEmbarkModuleRow) })!;
        var qudMutationTypeResult = (bool)typeFilterMethod.Invoke(null, new object[] { typeof(DummyQudMutationModuleDataRow) })!;

        var headerResult = (bool)candidateMethod!.Invoke(null, new object[] { headerGetter })!;
        var typeResult = (bool)candidateMethod.Invoke(null, new object[] { typeGetter })!;

        Assert.Multiple(() =>
        {
            Assert.That(headerResult, Is.True);
            Assert.That(typeResult, Is.False);
            Assert.That(embarkTypeResult, Is.True);
            Assert.That(qudMutationTypeResult, Is.False);
        });
    }

    [Test]
    public void Inventory_TranslatesKnownText_WhenPatched()
    {
        WriteDictionary(("Total weight", "総重量"));

        RunWithPostfixPatch(
            targetType: typeof(DummyInventoryScreen),
            targetMethodName: nameof(DummyInventoryScreen.GetCategoryLabel),
            patchType: typeof(InventoryLocalizationPatch),
            patchMethodName: nameof(InventoryLocalizationPatch.Postfix),
            assertion: () =>
        {
            var result = new DummyInventoryScreen("Total weight").GetCategoryLabel();

            Assert.That(result, Is.EqualTo("総重量"));
        });
    }

    [Test]
    public void Inventory_PassesThroughUnknownText_WhenPatched()
    {
        WriteDictionary(("Total weight", "総重量"));

        RunWithPostfixPatch(
            targetType: typeof(DummyInventoryScreen),
            targetMethodName: nameof(DummyInventoryScreen.GetCategoryLabel),
            patchType: typeof(InventoryLocalizationPatch),
            patchMethodName: nameof(InventoryLocalizationPatch.Postfix),
            assertion: () =>
        {
            var result = new DummyInventoryScreen("Unknown Inventory Text").GetCategoryLabel();

            Assert.That(result, Is.EqualTo("Unknown Inventory Text"));
        });
    }

    [Test]
    public void Inventory_SkipsAlreadyLocalizedBracketedDisplayName_WhenPatched()
    {
        RunWithPostfixPatch(
            targetType: typeof(DummyInventoryScreen),
            targetMethodName: nameof(DummyInventoryScreen.GetCategoryLabel),
            patchType: typeof(InventoryLocalizationPatch),
            patchMethodName: nameof(InventoryLocalizationPatch.Postfix),
            assertion: () =>
        {
            var result = new DummyInventoryScreen("水袋 [空]").GetCategoryLabel();

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("水袋 [空]"));
                Assert.That(Translator.GetMissingKeyHitCountForTests("水袋 [空]"), Is.EqualTo(0));
                Assert.That(Translator.GetMissingKeyHitCountForTests("[空]"), Is.EqualTo(0));
                Assert.That(Translator.GetMissingKeyHitCountForTests("空"), Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void Inventory_TranslatesStructuredDisplayName_WhenPatched()
    {
        WriteDictionary(
            ("water flask", "水袋"),
            ("[empty]", "[空]"));

        RunWithPostfixPatch(
            targetType: typeof(DummyInventoryScreen),
            targetMethodName: nameof(DummyInventoryScreen.GetCategoryLabel),
            patchType: typeof(InventoryLocalizationPatch),
            patchMethodName: nameof(InventoryLocalizationPatch.Postfix),
            assertion: () =>
        {
            var result = new DummyInventoryScreen("water flask [empty]").GetCategoryLabel();

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("水袋 [空]"));
                Assert.That(Translator.GetMissingKeyHitCountForTests("water flask [empty]"), Is.EqualTo(0));
            });
        });
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
        builder.Append("{\"entries\":[");
        AppendEntries(builder, entries);
        builder.AppendLine("]}");
        WriteDictionaryFile(builder.ToString());
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

    private static void AppendEntries(StringBuilder builder, IReadOnlyList<(string key, string text)> entries)
    {
        for (var index = 0; index < entries.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            var (key, text) = entries[index];
            builder.Append("{\"key\":\"");
            builder.Append(EscapeJson(key));
            builder.Append("\",\"text\":\"");
            builder.Append(EscapeJson(text));
            builder.Append("\"}");
        }
    }

    private static void RunWithPostfixPatch(
        Type targetType,
        string targetMethodName,
        Type patchType,
        string patchMethodName,
        Action assertion)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(targetType, targetMethodName),
                postfix: new HarmonyMethod(RequireMethod(patchType, patchMethodName)));
            assertion();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private void WriteDictionaryFile(string content)
    {
        var path = Path.Combine(tempDirectory, "ui-expansion-l2.ja.json");
        File.WriteAllText(path, content, Utf8WithoutBom);
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

    private static class DummyCharGenProperties
    {
        public static string get_HeaderText()
        {
            return "Header";
        }

        public static string get_type()
        {
            return "type";
        }
    }

#pragma warning disable S2094
    private static class DummyEmbarkModuleRow;

    private static class DummyQudMutationModuleDataRow;
#pragma warning restore S2094
}
