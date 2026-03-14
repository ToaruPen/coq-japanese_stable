using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class FactionsStatusScreenTranslationPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-factionsstatus-l2", Guid.NewGuid().ToString("N"));
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
    public void Postfix_TranslatesVillageReputationLines_WhenPatched()
    {
        WriteDictionary(
            ("The villagers of {0}", "{0}の村人たち"),
            ("Reputation: {0}", "評判: {0}"),
            ("The villagers of {0} don't care about you, but aggressive ones will attack you.", "{0}の村人たちはあなたを特に気に掛けていないが、攻撃的な者は襲ってくる。"),
            ("The villagers of {0} are interested in hearing gossip that's about them.", "{0}の村人たちは、自分たちに関するうわさ話に興味を示す。"),
            ("You aren't welcome in their holy places.", "あなたは彼らの聖地では歓迎されていない。"),
            ("You are welcome in their holy places.", "あなたは彼らの聖地で歓迎されている。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyFactionsStatusScreen), nameof(DummyFactionsStatusScreen.UpdateViewFromData)),
                postfix: new HarmonyMethod(RequireMethod(typeof(FactionsStatusScreenTranslationPatch), nameof(FactionsStatusScreenTranslationPatch.Postfix))));

            var screen = new DummyFactionsStatusScreen();
            screen.UpdateViewFromData();

            Assert.Multiple(() =>
            {
                Assert.That(screen.rawData[0].label, Is.EqualTo("Abalの村人たち"));
                Assert.That(screen.rawData[1].label, Is.EqualTo("評判: 0"));
                Assert.That(screen.rawData[2].label, Is.EqualTo("Abalの村人たちはあなたを特に気に掛けていないが、攻撃的な者は襲ってくる。"));
                Assert.That(screen.rawData[3].label, Is.EqualTo("Abalの村人たちは、自分たちに関するうわさ話に興味を示す。"));
                Assert.That(screen.rawData[4].label, Is.EqualTo("あなたは彼らの聖地では歓迎されていない。"));
                Assert.That(screen.rawData[5].label, Is.EqualTo("あなたは彼らの聖地で歓迎されている。"));
                Assert.That(screen.sortedData[0].label, Is.EqualTo("Biwarの村人たち"));
                Assert.That(screen.sortedData[1].label, Is.EqualTo("評判: -475"));
                Assert.That(screen.sortedData[2]._searchText, Is.EqualTo("The villagers of Biwar don't care about you, but aggressive ones will attack you."));
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

        var path = Path.Combine(tempDirectory, "factions-status-l2.ja.json");
        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
