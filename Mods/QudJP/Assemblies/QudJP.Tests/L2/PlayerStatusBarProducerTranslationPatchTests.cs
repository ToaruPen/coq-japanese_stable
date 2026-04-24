using System.Reflection;
using System.Diagnostics;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class PlayerStatusBarProducerTranslationPatchTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-player-status-bar-patch-l2", Guid.NewGuid().ToString("N"));
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
    public void Postfix_TranslatesProducerStringDataAfterBeginEndTurn()
    {
        WriteDictionary(
            ("Sated", "満腹"),
            ("Quenched", "潤沢"),
            ("World Map", "ワールドマップ"),
            ("Harvest Dawn", "ハーヴェスト・ドーン"),
            ("Kisu Ux", "キス・ウクス"),
            ("Seriously Wounded", "重傷"));

        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPlayerStatusBarTarget), nameof(DummyPlayerStatusBarTarget.BeginEndTurn)),
                postfix: new HarmonyMethod(RequirePatchMethod("Postfix", typeof(object), typeof(MethodBase))));

            var instance = new DummyPlayerStatusBarTarget
            {
                NextFoodWater = "Sated Quenched",
                NextTime = "Harvest Dawn 16th of Kisu Ux",
                NextZone = "World Map",
                NextZoneOnly = "World Map",
                NextHpBar = "{{Y|HP: {{R|Seriously Wounded}}}}"
            };

            instance.BeginEndTurn(core: null);

            Assert.Multiple(() =>
            {
                Assert.That(instance.GetStringData("FoodWater"), Is.EqualTo("満腹 潤沢"));
                Assert.That(instance.GetStringData("Time"), Is.EqualTo("ハーヴェスト・ドーン キス・ウクス16日"));
                Assert.That(instance.GetStringData("Zone"), Is.EqualTo("ワールドマップ"));
                Assert.That(instance.GetStringData("ZoneOnly"), Is.EqualTo("ワールドマップ"));
                Assert.That(instance.GetStringData("HPBar"), Is.EqualTo("{{Y|HP: {{R|重傷}}}}"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void TranslatePlayerStringData_MarksPlayerStringsDirty_WhenTranslatedAfterUiFlush()
    {
        WriteDictionary(
            ("Sated", "満腹"),
            ("Wet", "濡れ"),
            ("Harvest Dawn", "ハーヴェスト・ドーン"),
            ("Tuum Ut", "トゥーム・ウト"));

        var translateMethod = RequirePatchMethod("TranslatePlayerStringData", typeof(object));
        var instance = new DummyPlayerStatusBarTarget
        {
            NextFoodWater = "{{g|Sated}} {{b|Wet}}",
            NextTime = "Harvest Dawn 30th of Tuum Ut",
        };

        instance.BeginEndTurn(core: null);
        instance.MarkPlayerStringsFlushedForTests();
        Assert.That(
            instance.PlayerStringsDirtyForTests,
            Is.False,
            "Precondition: UI flush marker should clear playerStringsDirty before translation.");

        translateMethod.Invoke(null, new object?[] { instance });

        Assert.Multiple(() =>
        {
            Assert.That(instance.GetStringData("FoodWater"), Is.EqualTo("{{g|満腹}} {{b|濡れ}}"));
            Assert.That(instance.GetStringData("Time"), Is.EqualTo("ハーヴェスト・ドーン トゥーム・ウト30日"));
            Assert.That(
                instance.PlayerStringsDirtyForTests,
                Is.True,
                "Translated playerStringData must force PlayerStatusBar.Update to flush the new Japanese values.");
        });
    }

    [Test]
    public void TranslatePlayerStringData_WarnsOnce_WhenDirtyFlagFieldIsMissing()
    {
        WriteDictionary(("World Map", "ワールドマップ"));
        ResetPatchField("playerStringDataField");
        ResetPatchField("playerStringsDirtyField");
        ResetPatchField("playerStringsDirtyMissingWarningLogged");

        var listener = new CapturingTraceListener();
        Trace.Listeners.Add(listener);

        try
        {
            var translateMethod = RequirePatchMethod("TranslatePlayerStringData", typeof(object));
            var instance = new DummyPlayerStatusBarTargetWithoutDirtyFlag();

            translateMethod.Invoke(null, new object?[] { instance });
            translateMethod.Invoke(null, new object?[] { instance });

            Assert.Multiple(() =>
            {
                Assert.That(instance.GetStringData("Zone"), Is.EqualTo("ワールドマップ"));
                Assert.That(
                    listener.WarningMessages.Count(message =>
                        message.Contains("playerStringsDirty", StringComparison.Ordinal)
                        && message.Contains(nameof(DummyPlayerStatusBarTargetWithoutDirtyFlag), StringComparison.Ordinal)),
                    Is.EqualTo(1));
            });
        }
        finally
        {
            Trace.Listeners.Remove(listener);
            listener.Dispose();
            ResetPatchField("playerStringDataField");
            ResetPatchField("playerStringsDirtyField");
            ResetPatchField("playerStringsDirtyMissingWarningLogged");
        }
    }

    [Test]
    public void Postfix_UsesLowerAsciiFallbackWithoutMissingKeyNoise_ForProducerStringData()
    {
        WriteDictionary(
            ("sated", "満腹"),
            ("quenched", "潤沢"),
            ("harvest dawn", "ハーヴェスト・ドーン"),
            ("kisu ux", "キス・ウクス"));

        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPlayerStatusBarTarget), nameof(DummyPlayerStatusBarTarget.BeginEndTurn)),
                postfix: new HarmonyMethod(RequirePatchMethod("Postfix", typeof(object), typeof(MethodBase))));

            var instance = new DummyPlayerStatusBarTarget
            {
                NextFoodWater = "Sated Quenched",
                NextTime = "Harvest Dawn 16th of Kisu Ux",
            };

            instance.BeginEndTurn(core: null);

            Assert.Multiple(() =>
            {
                Assert.That(instance.GetStringData("FoodWater"), Is.EqualTo("満腹 潤沢"));
                Assert.That(instance.GetStringData("Time"), Is.EqualTo("ハーヴェスト・ドーン キス・ウクス16日"));
                Assert.That(Translator.GetMissingKeyHitCountForTests("Sated"), Is.EqualTo(0));
                Assert.That(Translator.GetMissingKeyHitCountForTests("Quenched"), Is.EqualTo(0));
                Assert.That(Translator.GetMissingKeyHitCountForTests("Harvest Dawn"), Is.EqualTo(0));
                Assert.That(Translator.GetMissingKeyHitCountForTests("Kisu Ux"), Is.EqualTo(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_RecordsProducerStringDataOwnerRouteTransforms_WithoutUITextSkinSinkObservation()
    {
        WriteDictionary(
            ("Sated", "満腹"),
            ("Quenched", "潤沢"),
            ("World Map", "ワールドマップ"),
            ("Harvest Dawn", "ハーヴェスト・ドーン"),
            ("Kisu Ux", "キス・ウクス"),
            ("Seriously Wounded", "重傷"));

        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPlayerStatusBarTarget), nameof(DummyPlayerStatusBarTarget.BeginEndTurn)),
                postfix: new HarmonyMethod(RequirePatchMethod("Postfix", typeof(object), typeof(MethodBase))));

            var instance = new DummyPlayerStatusBarTarget
            {
                NextFoodWater = "Sated Quenched",
                NextTime = "Harvest Dawn 16th of Kisu Ux",
                NextZone = "World Map",
                NextZoneOnly = "World Map",
                NextHpBar = "HP: Seriously Wounded",
            };

            instance.BeginEndTurn(core: null);

            Assert.Multiple(() =>
            {
                Assert.That(instance.GetStringData("FoodWater"), Is.EqualTo("満腹 潤沢"));
                Assert.That(instance.GetStringData("Time"), Is.EqualTo("ハーヴェスト・ドーン キス・ウクス16日"));
                Assert.That(instance.GetStringData("Zone"), Is.EqualTo("ワールドマップ"));
                Assert.That(instance.GetStringData("ZoneOnly"), Is.EqualTo("ワールドマップ"));
                Assert.That(instance.GetStringData("HPBar"), Is.EqualTo("HP: 重傷"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(PlayerStatusBarProducerTranslationPatch) + ".FoodWater",
                        "PlayerStatusBar.FoodWater"),
                    Is.GreaterThan(0));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(PlayerStatusBarProducerTranslationPatch) + ".Time",
                        "PlayerStatusBar.Time"),
                    Is.GreaterThan(0));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(PlayerStatusBarProducerTranslationPatch) + ".Zone",
                        "PlayerStatusBar.Zone"),
                    Is.GreaterThan(0));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(PlayerStatusBarProducerTranslationPatch) + ".ZoneOnly",
                        "PlayerStatusBar.ZoneOnly"),
                    Is.GreaterThan(0));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(PlayerStatusBarProducerTranslationPatch) + ".HPBar",
                        "PlayerStatusBar.HPBar"),
                    Is.GreaterThan(0));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(UITextSkinTranslationPatch),
                        nameof(PlayerStatusBarProducerTranslationPatch) + ".FoodWater",
                        SinkObservation.ObservationOnlyDetail,
                        "Sated Quenched",
                        "Sated Quenched"),
                    Is.EqualTo(0));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(UITextSkinTranslationPatch),
                        nameof(PlayerStatusBarProducerTranslationPatch) + ".Time",
                        SinkObservation.ObservationOnlyDetail,
                        "Harvest Dawn 16th of Kisu Ux",
                        "Harvest Dawn 16th of Kisu Ux"),
                    Is.EqualTo(0));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(UITextSkinTranslationPatch),
                        nameof(PlayerStatusBarProducerTranslationPatch) + ".Zone",
                        SinkObservation.ObservationOnlyDetail,
                        "World Map",
                        "World Map"),
                    Is.EqualTo(0));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(UITextSkinTranslationPatch),
                        nameof(PlayerStatusBarProducerTranslationPatch) + ".ZoneOnly",
                        SinkObservation.ObservationOnlyDetail,
                        "World Map",
                        "World Map"),
                    Is.EqualTo(0));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(UITextSkinTranslationPatch),
                        nameof(PlayerStatusBarProducerTranslationPatch) + ".HPBar",
                        SinkObservation.ObservationOnlyDetail,
                        "HP: Seriously Wounded",
                        "HP: Seriously Wounded"),
                    Is.EqualTo(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_TranslatesXpBarTextAfterUpdate()
    {
        WriteDictionary(("LVL", "Lv"));

        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPlayerStatusBarTarget), nameof(DummyPlayerStatusBarTarget.Update)),
                postfix: new HarmonyMethod(RequirePatchMethod("Postfix", typeof(object), typeof(MethodBase))));

            var instance = new DummyPlayerStatusBarTarget
            {
                Level = 1,
                Experience = 0,
                NextLevelExperience = 220
            };

            instance.Update();

            Assert.That(instance.XPBar.text.text, Is.EqualTo("Lv: 1 Exp: 0 / 220"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_RecordsLevelExpOwnerRouteTransform_WithoutUITextSkinSinkObservation()
    {
        WriteDictionary(("LVL", "Lv"));

        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPlayerStatusBarTarget), nameof(DummyPlayerStatusBarTarget.Update)),
                postfix: new HarmonyMethod(RequirePatchMethod("Postfix", typeof(object), typeof(MethodBase))));

            var instance = new DummyPlayerStatusBarTarget
            {
                Level = 1,
                Experience = 0,
                NextLevelExperience = 220,
            };

            instance.Update();

            const string source = "LVL: 1 Exp: 0 / 220";
            Assert.Multiple(() =>
            {
                Assert.That(instance.XPBar.text.text, Is.EqualTo("Lv: 1 Exp: 0 / 220"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(PlayerStatusBarProducerTranslationPatch) + ".XPBar",
                        "PlayerStatusBar.LevelExp"),
                    Is.GreaterThan(0));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(UITextSkinTranslationPatch),
                        nameof(PlayerStatusBarProducerTranslationPatch) + ".XPBar",
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
    public void TranslatePlayerStringData_InitializesCacheOnFirstCall()
    {
        WriteDictionary(("World Map", "ワールドマップ"));

        var patchType = typeof(Translator).Assembly.GetType("QudJP.Patches.PlayerStatusBarProducerTranslationPatch", throwOnError: false);
        Assert.That(patchType, Is.Not.Null, "PlayerStatusBarProducerTranslationPatch type not found.");

        var cacheField = patchType!.GetField("playerStringDataField", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(cacheField, Is.Not.Null, "playerStringDataField cache not found.");
        cacheField!.SetValue(null, null);

        var translateMethod = RequirePatchMethod("TranslatePlayerStringData", typeof(object));
        var instance = new DummyPlayerStatusBarTarget
        {
            NextZone = "World Map",
        };

        instance.BeginEndTurn(core: null);
        translateMethod.Invoke(null, new object?[] { instance });

        Assert.Multiple(() =>
        {
            Assert.That(cacheField.GetValue(null), Is.AssignableTo<FieldInfo>());
            Assert.That(instance.GetStringData("Zone"), Is.EqualTo("ワールドマップ"));
        });
    }

    [Test]
    public void TranslateXpBar_InitializesReflectionCacheOnFirstCall()
    {
        WriteDictionary(("LVL", "Lv"));

        var patchType = typeof(Translator).Assembly.GetType("QudJP.Patches.PlayerStatusBarProducerTranslationPatch", throwOnError: false);
        Assert.That(patchType, Is.Not.Null, "PlayerStatusBarProducerTranslationPatch type not found.");

        var xpBarCacheField = patchType!.GetField("xpBarField", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(xpBarCacheField, Is.Not.Null, "xpBarField cache not found.");

        var textCacheField = patchType.GetField("xpBarTextField", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(textCacheField, Is.Not.Null, "xpBarTextField cache not found.");

        xpBarCacheField!.SetValue(null, null);
        textCacheField!.SetValue(null, null);

        var translateMethod = RequirePatchMethod("TranslateXpBar", typeof(object));
        var instance = new DummyPlayerStatusBarTarget
        {
            Level = 1,
            Experience = 0,
            NextLevelExperience = 220,
        };

        instance.Update();
        translateMethod.Invoke(null, new object?[] { instance });

        Assert.Multiple(() =>
        {
            Assert.That(xpBarCacheField.GetValue(null), Is.AssignableTo<FieldInfo>());
            Assert.That(textCacheField.GetValue(null), Is.AssignableTo<FieldInfo>());
            Assert.That(instance.XPBar.text.text, Is.EqualTo("Lv: 1 Exp: 0 / 220"));
        });
    }

    private static MethodInfo RequirePatchMethod(string methodName, params Type[] parameterTypes)
    {
        var patchType = typeof(Translator).Assembly.GetType("QudJP.Patches.PlayerStatusBarProducerTranslationPatch", throwOnError: false);
        Assert.That(patchType, Is.Not.Null, "PlayerStatusBarProducerTranslationPatch type not found.");

        var method = patchType!.GetMethod(
            methodName,
            BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: parameterTypes,
            modifiers: null);
        Assert.That(method, Is.Not.Null, $"Method not found: {patchType.FullName}.{methodName}");

        return method!;
    }

    private static void ResetPatchField(string fieldName)
    {
        var patchType = typeof(Translator).Assembly.GetType("QudJP.Patches.PlayerStatusBarProducerTranslationPatch", throwOnError: false);
        Assert.That(patchType, Is.Not.Null, "PlayerStatusBarProducerTranslationPatch type not found.");

        var field = patchType!.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(field, Is.Not.Null, $"Field not found: {patchType.FullName}.{fieldName}");
        field!.SetValue(null, field.FieldType == typeof(bool) ? false : null);
    }

    private static MethodInfo RequireMethod(Type type, string methodName)
    {
        var method = AccessTools.Method(type, methodName);
        Assert.That(method, Is.Not.Null, $"Method not found: {type.FullName}.{methodName}");
        return method!;
    }

    private void WriteDictionary(params (string key, string text)[] entries)
    {
        var path = Path.Combine(tempDirectory, "player-status-bar.ja.json");
        using var writer = new StreamWriter(path, append: false, Utf8WithoutBom);
        writer.Write("{\"entries\":[");

        for (var index = 0; index < entries.Length; index++)
        {
            if (index > 0)
            {
                writer.Write(',');
            }

            writer.Write("{\"key\":\"");
            writer.Write(EscapeJson(entries[index].key));
            writer.Write("\",\"text\":\"");
            writer.Write(EscapeJson(entries[index].text));
            writer.Write("\"}");
        }

        writer.WriteLine("]}");
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private sealed class CapturingTraceListener : TraceListener
    {
        internal readonly List<string> WarningMessages = new List<string>();

        public override void Write(string? message)
        {
        }

        public override void WriteLine(string? message)
        {
        }

        public override void TraceEvent(
            TraceEventCache? eventCache,
            string source,
            TraceEventType eventType,
            int id,
            string? message)
        {
            if (eventType == TraceEventType.Warning && message is not null)
            {
                WarningMessages.Add(message);
            }
        }
    }
}
