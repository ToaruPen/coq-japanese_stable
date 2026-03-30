using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class ZoneDisplayNameTranslationPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-zone-display-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void Postfix_TranslatesPrimaryOverloadResult_WhenPatched()
    {
        WriteDictionary(
            ("Joppa", "ジョッパ"),
            ("surface", "地表"));

        var target = new DummyZoneDisplayNameTarget
        {
            Result = "Joppa, surface",
        };

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(
                    typeof(DummyZoneDisplayNameTarget),
                    nameof(DummyZoneDisplayNameTarget.GetZoneDisplayName),
                    typeof(string),
                    typeof(int),
                    typeof(object),
                    typeof(bool),
                    typeof(bool),
                    typeof(bool),
                    typeof(bool)),
                postfix: new HarmonyMethod(RequireMethod(typeof(ZoneDisplayNameTranslationPatch), nameof(ZoneDisplayNameTranslationPatch.Postfix), typeof(string).MakeByRefType())));

            var result = target.GetZoneDisplayName("JoppaWorld.11.22.1.1.10", 10, new object());

            Assert.That(result, Is.EqualTo("ジョッパ, 地表"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_TranslatesWorldCoordinateOverloadResult_WhenPatched()
    {
        WriteDictionary(
            ("a rusted archway", "錆びたアーチ道"),
            ("{0} strata deep", "地下{0}層"));

        var target = new DummyZoneDisplayNameTarget
        {
            Result = "a rusted archway, 2 strata deep",
        };

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(
                    typeof(DummyZoneDisplayNameTarget),
                    nameof(DummyZoneDisplayNameTarget.GetZoneDisplayName),
                    typeof(string),
                    typeof(string),
                    typeof(int),
                    typeof(int),
                    typeof(int),
                    typeof(int),
                    typeof(int),
                    typeof(bool),
                    typeof(bool),
                    typeof(bool),
                    typeof(bool)),
                postfix: new HarmonyMethod(RequireMethod(typeof(ZoneDisplayNameTranslationPatch), nameof(ZoneDisplayNameTranslationPatch.Postfix), typeof(string).MakeByRefType())));

            var result = target.GetZoneDisplayName("zone", "world", 1, 1, 1, 1, 8);

            Assert.That(result, Is.EqualTo("錆びたアーチ道, 地下2層"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_TranslatesDynamicZoneNamePatterns_WhenPatched()
    {
        WriteDictionary(
            ("Joppa", "ジョッパ"),
            ("slimy", "ぬめる"),
            ("slime bog", "ぬめり沼"),
            (", goatfolk village", "、ヤギ人の村"));

        var target = new DummyZoneDisplayNameTarget
        {
            Result = "Joppa and slime bog",
        };

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(
                    typeof(DummyZoneDisplayNameTarget),
                    nameof(DummyZoneDisplayNameTarget.GetZoneDisplayName),
                    typeof(string),
                    typeof(bool),
                    typeof(bool),
                    typeof(bool),
                    typeof(bool)),
                postfix: new HarmonyMethod(RequireMethod(typeof(ZoneDisplayNameTranslationPatch), nameof(ZoneDisplayNameTranslationPatch.Postfix), typeof(string).MakeByRefType())));

            var biomeResult = target.GetZoneDisplayName("zone");
            target.Result = "Joppa, goatfolk village";
            var goatfolkResult = target.GetZoneDisplayName("zone");

            Assert.Multiple(() =>
            {
                Assert.That(biomeResult, Is.EqualTo("ジョッパとぬめり沼"));
                Assert.That(goatfolkResult, Is.EqualTo("ジョッパ、ヤギ人の村"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_RecordsOwnerRouteTransforms_WithoutUITextSkinSinkObservation_WhenPatched()
    {
        WriteDictionary(("Joppa", "ジョッパ"));

        var target = new DummyZoneDisplayNameTarget
        {
            Result = "Joppa",
        };

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(
                    typeof(DummyZoneDisplayNameTarget),
                    nameof(DummyZoneDisplayNameTarget.GetZoneDisplayName),
                    typeof(string),
                    typeof(bool),
                    typeof(bool),
                    typeof(bool),
                    typeof(bool)),
                postfix: new HarmonyMethod(RequireMethod(typeof(ZoneDisplayNameTranslationPatch), nameof(ZoneDisplayNameTranslationPatch.Postfix), typeof(string).MakeByRefType())));

            const string source = "Joppa";
            var result = target.GetZoneDisplayName("zone");

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("ジョッパ"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(ZoneDisplayNameTranslationPatch),
                        "ZoneDisplayName"),
                    Is.GreaterThan(0));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(UITextSkinTranslationPatch),
                        nameof(ZoneDisplayNameTranslationPatch),
                        SinkObservation.ObservationOnlyDetail,
                        source,
                        source),
                    Is.EqualTo(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_PassesThroughUnknownSimpleOverloadResult_WhenPatched()
    {
        var target = new DummyZoneDisplayNameTarget
        {
            Result = "Unknown frontier",
        };

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(
                    typeof(DummyZoneDisplayNameTarget),
                    nameof(DummyZoneDisplayNameTarget.GetZoneDisplayName),
                    typeof(string),
                    typeof(bool),
                    typeof(bool),
                    typeof(bool),
                    typeof(bool)),
                postfix: new HarmonyMethod(RequireMethod(typeof(ZoneDisplayNameTranslationPatch), nameof(ZoneDisplayNameTranslationPatch.Postfix), typeof(string).MakeByRefType())));

            var result = target.GetZoneDisplayName("zone");

            Assert.That(result, Is.EqualTo("Unknown frontier"));
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
            Path.Combine(tempDirectory, "ui-zone-display-l2.ja.json"),
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
