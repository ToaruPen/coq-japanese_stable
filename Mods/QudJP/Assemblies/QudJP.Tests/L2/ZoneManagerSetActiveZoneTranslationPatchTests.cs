using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class ZoneManagerSetActiveZoneTranslationPatchTests
{
    private string tempDirectory = null!;
    private string patternFilePath = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-zone-banner-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        patternFilePath = Path.Combine(tempDirectory, "messages.ja.json");

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        MessagePatternTranslator.ResetForTests();
        MessagePatternTranslator.SetPatternFileForTests(patternFilePath);
        File.WriteAllText(patternFilePath, "{\"patterns\":[]}\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        DummyMessageQueue.Reset();
        DummyZoneWorldFactory.Reset();
        DummyZoneCalendar.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        MessagePatternTranslator.ResetForTests();
        DummyZoneWorldFactory.Reset();
        DummyZoneCalendar.Reset();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void PrefixAndPostfix_TranslateZoneBannerBeforeMessageLogSink_WhenPatched()
    {
        WriteDictionary(
            ("Joppa", "ジョッパ"),
            ("surface", "地表"));
        DummyZoneWorldFactory.WorldId = "JoppaWorld";
        DummyZoneWorldFactory.ZoneId = "JoppaWorld.11.22.1.1.10";
        DummyZoneWorldFactory.ZoneDisplayNameValue = "Joppa, surface";
        DummyZoneCalendar.TimeValue = "06:00";

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyZoneManagerSetActiveZoneTarget), nameof(DummyZoneManagerSetActiveZoneTarget.SetActiveZone)),
                prefix: new HarmonyMethod(RequireMethod(typeof(ZoneManagerSetActiveZoneTranslationPatch), nameof(ZoneManagerSetActiveZoneTranslationPatch.Prefix))),
                postfix: new HarmonyMethod(RequireMethod(typeof(ZoneManagerSetActiveZoneTranslationPatch), nameof(ZoneManagerSetActiveZoneTranslationPatch.Postfix))));
            harmony.Patch(
                original: RequireMethod(typeof(DummyMessageQueue), nameof(DummyMessageQueue.AddPlayerMessage), typeof(string), typeof(string), typeof(bool)),
                prefix: new HarmonyMethod(RequireMethod(typeof(ZoneManagerSetActiveZoneMessageQueuePatch), nameof(ZoneManagerSetActiveZoneMessageQueuePatch.Prefix), typeof(string).MakeByRefType(), typeof(string), typeof(bool))));
            harmony.Patch(
                original: RequireMethod(typeof(DummyMessageQueue), nameof(DummyMessageQueue.AddPlayerMessage), typeof(string), typeof(string), typeof(bool)),
                prefix: new HarmonyMethod(RequireMethod(typeof(MessageLogPatch), nameof(MessageLogPatch.Prefix), typeof(string).MakeByRefType(), typeof(string), typeof(bool))));

            var manager = new DummyZoneManagerSetActiveZoneTarget();
            manager.SetActiveZone();

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("ジョッパ, 地表, 06:00"));
                Assert.That(DummyMessageQueue.LastColor, Is.EqualTo("C"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void PrefixAndPostfix_MarkAlreadyLocalizedZoneBanner_WhenPatched()
    {
        DummyZoneWorldFactory.WorldId = "JoppaWorld";
        DummyZoneWorldFactory.ZoneId = "JoppaWorld.11.22.1.1.10";
        DummyZoneWorldFactory.ZoneDisplayNameValue = "ジョッパ, 地表";
        DummyZoneCalendar.TimeValue = "06:00";

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyZoneManagerSetActiveZoneTarget), nameof(DummyZoneManagerSetActiveZoneTarget.SetActiveZone)),
                prefix: new HarmonyMethod(RequireMethod(typeof(ZoneManagerSetActiveZoneTranslationPatch), nameof(ZoneManagerSetActiveZoneTranslationPatch.Prefix))),
                postfix: new HarmonyMethod(RequireMethod(typeof(ZoneManagerSetActiveZoneTranslationPatch), nameof(ZoneManagerSetActiveZoneTranslationPatch.Postfix))));
            harmony.Patch(
                original: RequireMethod(typeof(DummyMessageQueue), nameof(DummyMessageQueue.AddPlayerMessage), typeof(string), typeof(string), typeof(bool)),
                prefix: new HarmonyMethod(RequireMethod(typeof(ZoneManagerSetActiveZoneMessageQueuePatch), nameof(ZoneManagerSetActiveZoneMessageQueuePatch.Prefix), typeof(string).MakeByRefType(), typeof(string), typeof(bool))));
            harmony.Patch(
                original: RequireMethod(typeof(DummyMessageQueue), nameof(DummyMessageQueue.AddPlayerMessage), typeof(string), typeof(string), typeof(bool)),
                prefix: new HarmonyMethod(RequireMethod(typeof(MessageLogPatch), nameof(MessageLogPatch.Prefix), typeof(string).MakeByRefType(), typeof(string), typeof(bool))));

            var manager = new DummyZoneManagerSetActiveZoneTarget();
            manager.SetActiveZone();

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("ジョッパ, 地表, 06:00"));
                Assert.That(DummyMessageQueue.LastColor, Is.EqualTo("C"));
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

    private static MethodInfo RequireMethod(Type type, string methodName, params Type[] parameterTypes)
    {
        if (parameterTypes.Length == 0)
        {
            return type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
        }

        return AccessTools.Method(type, methodName, parameterTypes)
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

        File.WriteAllText(
            Path.Combine(tempDirectory, "ui-zone-banner-l2.ja.json"),
            builder.ToString(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
