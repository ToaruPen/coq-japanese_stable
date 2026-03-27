using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class DescriptionShortDescriptionPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-description-short-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        SinkObservation.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        SinkObservation.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void DescriptionShortDescriptionPatch_ObservationOnly_LeavesScopedWorldModsEntriesUnchanged_WhenPatched()
    {
        WriteScopedDictionary(
            ("Strength Bonus Cap: no limit\nWeapon Class: Long Blades (increased penetration on critical hit)", "筋力ボーナス上限: なし\n武器カテゴリ: 長剣（クリティカル時に貫通力上昇）"));
        WriteDictionary(
            ("Masterwork: This weapon scores critical hits {0} of the time instead of 5%.", "名工品: この武器のクリティカル発生率は{0}（通常は5%）。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyDescriptionShortDescriptionTarget), nameof(DummyDescriptionShortDescriptionTarget.GetShortDescription)),
                postfix: new HarmonyMethod(RequirePostfix(typeof(DescriptionShortDescriptionPatch), nameof(DescriptionShortDescriptionPatch.Postfix))));

            var compareTarget = new DummyDescriptionShortDescriptionTarget(
                "Strength Bonus Cap: no limit\nWeapon Class: Long Blades (increased penetration on critical hit)");
            var masterworkTarget = new DummyDescriptionShortDescriptionTarget(
                "{{rules|Masterwork: This weapon scores critical hits 15% of the time instead of 5%.}}");

            Assert.Multiple(() =>
            {
                Assert.That(
                    compareTarget.GetShortDescription(useShort: true, useLong: false, prefix: string.Empty),
                    Is.EqualTo("Strength Bonus Cap: no limit\nWeapon Class: Long Blades (increased penetration on critical hit)"));
                Assert.That(
                    masterworkTarget.GetShortDescription(useShort: true, useLong: false, prefix: string.Empty),
                    Is.EqualTo("{{rules|Masterwork: This weapon scores critical hits 15% of the time instead of 5%.}}"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void DescriptionShortDescriptionPatch_ObservationOnly_LogsUnclaimedShortDescription_WhenPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyDescriptionShortDescriptionTarget), nameof(DummyDescriptionShortDescriptionTarget.GetShortDescription)),
                postfix: new HarmonyMethod(RequirePostfix(typeof(DescriptionShortDescriptionPatch), nameof(DescriptionShortDescriptionPatch.Postfix))));

            const string source = "Observation-only short description";
            var target = new DummyDescriptionShortDescriptionTarget(source);
            var result = target.GetShortDescription(useShort: true, useLong: false, prefix: string.Empty);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(source));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(UITextSkinTranslationPatch),
                        nameof(DescriptionShortDescriptionPatch),
                        SinkObservation.ObservationOnlyDetail,
                        source,
                        source),
                    Is.GreaterThan(0));
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

    private static MethodInfo RequireMethod(Type type, string methodName)
    {
        return AccessTools.Method(type, methodName, new[] { typeof(bool), typeof(bool), typeof(string) })
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private static MethodInfo RequirePostfix(Type type, string methodName)
    {
        return AccessTools.Method(type, methodName)
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private void WriteDictionary(params (string key, string text)[] entries)
    {
        WriteDictionaryToFile("description-short-l2.ja.json", entries);
    }

    private void WriteScopedDictionary(params (string key, string text)[] entries)
    {
        WriteDictionaryToFile("world-mods.ja.json", entries);
    }

    private void WriteDictionaryToFile(string fileName, (string key, string text)[] entries)
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
}
