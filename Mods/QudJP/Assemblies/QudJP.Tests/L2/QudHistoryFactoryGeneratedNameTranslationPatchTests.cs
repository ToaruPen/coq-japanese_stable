using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class QudHistoryFactoryGeneratedNameTranslationPatchTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    private string tempDirectory = null!;
    private string dictionaryDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-history-factory-generated-name-l2", Guid.NewGuid().ToString("N"));
        dictionaryDirectory = Path.Combine(tempDirectory, "dict");
        Directory.CreateDirectory(dictionaryDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(dictionaryDirectory);
        DynamicTextObservability.ResetForTests();
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(true);
        DummyQudHistoryFactoryTarget.RuinsSiteNameResult = string.Empty;
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(null);
        DummyQudHistoryFactoryTarget.RuinsSiteNameResult = string.Empty;

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void NameRuinsSitePostfix_TranslatesGeneratedRuinsSiteName_WhenPatched()
    {
        WriteDictionaryFile("Scoped/historyspice-common.ja.json", ("red", "赤"), ("wastes", "荒野"));
        DummyQudHistoryFactoryTarget.RuinsSiteNameResult = "red wastes Ibul";
        var harmonyId = "qudjp-test-history-factory-ruins-" + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(
                    typeof(DummyQudHistoryFactoryTarget),
                    nameof(DummyQudHistoryFactoryTarget.NameRuinsSite),
                    typeof(object),
                    typeof(bool).MakeByRefType(),
                    typeof(string).MakeByRefType()),
                postfix: new HarmonyMethod(RequireMethod(
                    typeof(QudHistoryFactoryNameRuinsSiteTranslationPatch),
                    nameof(QudHistoryFactoryNameRuinsSiteTranslationPatch.Postfix),
                    typeof(string).MakeByRefType())));

            var result = DummyQudHistoryFactoryTarget.NameRuinsSite(new object(), out var proper, out var nameRoot);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("赤の荒野Ibul"));
                Assert.That(proper, Is.True);
                Assert.That(nameRoot, Is.EqualTo("Ibul"));
                Assert.That(RuinsRouteHitCount(), Is.EqualTo(1));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [TestCase("Ibul")]
    [TestCase("some forgotten ruins")]
    [TestCase("")]
    [TestCase("\u0001red wastes Ibul")]
    public void NameRuinsSitePostfix_LeavesProperAndFallbackNamesUnchanged_WhenPatched(string source)
    {
        WriteDictionaryFile("Scoped/historyspice-common.ja.json", ("red", "赤"), ("wastes", "荒野"));
        DummyQudHistoryFactoryTarget.RuinsSiteNameResult = source;
        var harmonyId = "qudjp-test-history-factory-ruins-" + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(
                    typeof(DummyQudHistoryFactoryTarget),
                    nameof(DummyQudHistoryFactoryTarget.NameRuinsSite),
                    typeof(object),
                    typeof(bool).MakeByRefType(),
                    typeof(string).MakeByRefType()),
                postfix: new HarmonyMethod(RequireMethod(
                    typeof(QudHistoryFactoryNameRuinsSiteTranslationPatch),
                    nameof(QudHistoryFactoryNameRuinsSiteTranslationPatch.Postfix),
                    typeof(string).MakeByRefType())));

            var result = DummyQudHistoryFactoryTarget.NameRuinsSite(new object(), out _, out var nameRoot);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(source));
                Assert.That(nameRoot, Is.EqualTo("Ibul"));
                Assert.That(RuinsRouteHitCount(), Is.Zero);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void NameRuinsSitePostfix_PreservesColorTags_WhenPatched()
    {
        WriteDictionaryFile("Scoped/historyspice-common.ja.json", ("red", "赤"), ("wastes", "荒野"));
        var source = "{{R|red wastes Ibul}}";
        var result = source;

        QudHistoryFactoryNameRuinsSiteTranslationPatch.Postfix(ref result);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo("{{R|赤の荒野Ibul}}"));
            Assert.That(RuinsRouteHitCount(), Is.EqualTo(1));
        });
    }

    [TestCase("Cult of the Gleaming Ghost", "煌めき幽鬼の教団")]
    [TestCase("Ibulian Cult", "Ibul派の教団")]
    [TestCase("Gleamingian Cult", "煌めき派の教団")]
    public void GenerateCultNamePostfix_TranslatesStoredCultName_WhenPatched(string source, string expected)
    {
        WriteDictionaryFile("Scoped/historyspice-common.ja.json", ("gleaming", "煌めき"), ("ghost", "幽鬼"));
        WriteDictionaryFile("world-gospels.ja.json", ("cult", "教団"));
        var entity = new DummyHistoricEntity();
        entity.SeedProperty("cultName", source);
        var harmonyId = "qudjp-test-history-factory-cult-" + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(
                    typeof(DummyQudHistoryFactoryTarget),
                    nameof(DummyQudHistoryFactoryTarget.GenerateCultName),
                    typeof(DummyHistoricEntity),
                    typeof(object)),
                postfix: new HarmonyMethod(RequireMethod(
                    typeof(QudHistoryFactoryGenerateCultNameTranslationPatch),
                    nameof(QudHistoryFactoryGenerateCultNameTranslationPatch.Postfix),
                    typeof(object))));

            DummyQudHistoryFactoryTarget.GenerateCultName(entity, new object());

            Assert.Multiple(() =>
            {
                Assert.That(entity.GetCurrentSnapshot().GetProperty("cultName"), Is.EqualTo(expected));
                Assert.That(CultRouteHitCount(), Is.EqualTo(1));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [TestCase("Mystery of the Unknown Ghost")]
    [TestCase("")]
    [TestCase("\u0001Cult of the Gleaming Ghost")]
    public void GenerateCultNamePostfix_LeavesUnknownCultNameUnchanged_WhenPatched(string source)
    {
        var entity = new DummyHistoricEntity();
        entity.SeedProperty("cultName", source);
        var harmonyId = "qudjp-test-history-factory-cult-" + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(
                    typeof(DummyQudHistoryFactoryTarget),
                    nameof(DummyQudHistoryFactoryTarget.GenerateCultName),
                    typeof(DummyHistoricEntity),
                    typeof(object)),
                postfix: new HarmonyMethod(RequireMethod(
                    typeof(QudHistoryFactoryGenerateCultNameTranslationPatch),
                    nameof(QudHistoryFactoryGenerateCultNameTranslationPatch.Postfix),
                    typeof(object))));

            DummyQudHistoryFactoryTarget.GenerateCultName(entity, new object());

            Assert.Multiple(() =>
            {
                Assert.That(entity.GetCurrentSnapshot().GetProperty("cultName"), Is.EqualTo(source));
                Assert.That(CultRouteHitCount(), Is.Zero);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void GenerateCultNamePostfix_PreservesColorTags_WhenPatched()
    {
        WriteDictionaryFile("Scoped/historyspice-common.ja.json", ("gleaming", "煌めき"), ("ghost", "幽鬼"));
        WriteDictionaryFile("world-gospels.ja.json", ("cult", "教団"));
        var entity = new DummyHistoricEntity();
        entity.SeedProperty("cultName", "{{R|Cult of the Gleaming Ghost}}");

        QudHistoryFactoryGenerateCultNameTranslationPatch.Postfix(entity);

        Assert.Multiple(() =>
        {
            Assert.That(entity.GetCurrentSnapshot().GetProperty("cultName"), Is.EqualTo("{{R|煌めき幽鬼の教団}}"));
            Assert.That(CultRouteHitCount(), Is.EqualTo(1));
        });
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

    private static int RuinsRouteHitCount() =>
        DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(QudHistoryFactoryNameRuinsSiteTranslationPatch),
            QudHistoryFactoryNameRuinsSiteTranslationPatch.Family);

    private static int CultRouteHitCount() =>
        DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(QudHistoryFactoryGenerateCultNameTranslationPatch),
            QudHistoryFactoryGenerateCultNameTranslationPatch.Family);

    private static MethodInfo RequireMethod(Type type, string name, params Type[] parameters) =>
        AccessTools.Method(type, name, parameters)
        ?? throw new InvalidOperationException("Missing method " + type.FullName + "." + name);

    private static string EscapeJson(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
}
