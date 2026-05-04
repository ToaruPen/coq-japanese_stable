using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class AbilityBarButtonTextTranslationPatchTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-abilitybar-button-text-l2", Guid.NewGuid().ToString("N"));
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
    public void Postfix_TranslatesAbilityButtonTextAndRecordsOwnerRoute()
    {
        WriteDictionary(
            ("Sprint", "ダッシュ"),
            ("Freezing Ray", "凍結線"),
            ("Toggle", "切替"),
            ("Sense", "感知"),
            ("Discharge", "放電"),
            ("Lase", "レーザー照射"),
            ("Recoil", "帰還"),
            ("Joppa", "ジョッパ"),
            ("[disabled]", "[無効]"),
            ("on", "オン"),
            ("off", "オフ"));

        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyAbilityBarButtonTextTarget), nameof(DummyAbilityBarButtonTextTarget.Update)),
                postfix: new HarmonyMethod(RequirePatchMethod("Postfix", typeof(object))));

            var target = new DummyAbilityBarButtonTextTarget();
            var sprint = new DummyAbilityBarButton("&CSprint {{K|[disabled]}} {{Y|<{{w|S}}>}}");
            var freezingRay = new DummyAbilityBarButton("&CFreezing Ray {{C|[5]}} {{K|[{{g|on}}]}}");
            var toggle = new DummyAbilityBarButton("&CToggle {{K|[{{r|off}}]}} {{K|[offhand]}}");
            var sense = new DummyAbilityBarButton("&CSense {{K|[condition]}} {{K|[{{g|on}}]}}");
            var discharge = new DummyAbilityBarButton("&CDischarge [3 charge]");
            var lase = new DummyAbilityBarButton("&CLase (4 charges)");
            var recoil = new DummyAbilityBarButton("Recoil");
            var recoilToJoppa = new DummyAbilityBarButton("Recoil to Joppa");
            var coloredRecoilToJoppa = new DummyAbilityBarButton("&CRecoil to Joppa {{K|[disabled]}}");
            var englishFallback = new DummyAbilityBarButton("UnregisteredText");
            var empty = new DummyAbilityBarButton(string.Empty);
            var marker = new DummyAbilityBarButton("\u0001SomeText");
            target.AbilityButtons.Add(sprint);
            target.AbilityButtons.Add(freezingRay);
            target.AbilityButtons.Add(toggle);
            target.AbilityButtons.Add(sense);
            target.AbilityButtons.Add(discharge);
            target.AbilityButtons.Add(lase);
            target.AbilityButtons.Add(recoil);
            target.AbilityButtons.Add(recoilToJoppa);
            target.AbilityButtons.Add(coloredRecoilToJoppa);
            target.AbilityButtons.Add(englishFallback);
            target.AbilityButtons.Add(empty);
            target.AbilityButtons.Add(marker);

            target.Update();

            Assert.Multiple(() =>
            {
                Assert.That(sprint.Text.text, Is.EqualTo("&Cダッシュ {{K|[無効]}} {{Y|<{{w|S}}>}}"));
                Assert.That(freezingRay.Text.text, Is.EqualTo("&C凍結線 {{C|[5]}} {{K|[{{g|オン}}]}}"));
                Assert.That(toggle.Text.text, Is.EqualTo("&C切替 {{K|[{{r|オフ}}]}} {{K|[offhand]}}"));
                Assert.That(sense.Text.text, Is.EqualTo("&C感知 {{K|[condition]}} {{K|[{{g|オン}}]}}"));
                Assert.That(discharge.Text.text, Is.EqualTo("&C放電 [3チャージ]"));
                Assert.That(lase.Text.text, Is.EqualTo("&Cレーザー照射 (4チャージ)"));
                Assert.That(recoil.Text.text, Is.EqualTo("帰還"));
                Assert.That(recoilToJoppa.Text.text, Is.EqualTo("ジョッパへ帰還"));
                Assert.That(coloredRecoilToJoppa.Text.text, Is.EqualTo("&Cジョッパへ帰還 {{K|[無効]}}"));
                Assert.That(englishFallback.Text.text, Is.EqualTo("UnregisteredText"));
                Assert.That(empty.Text.text, Is.EqualTo(string.Empty));
                Assert.That(marker.Text.text, Is.EqualTo("\u0001SomeText"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(AbilityBarButtonTextTranslationPatch),
                        "AbilityBar.ButtonText"),
                    Is.EqualTo(9));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(UITextSkinTranslationPatch),
                        nameof(AbilityBarButtonTextTranslationPatch),
                        SinkObservation.ObservationOnlyDetail,
                        "&CSprint {{K|[disabled]}} {{Y|<{{w|S}}>}}",
                        "&CSprint {{K|[disabled]}} {{Y|<{{w|S}}>}}"),
                    Is.EqualTo(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_RegistersRuntimeCompletedLabelsToSuppressFlatTranslatorMisses()
    {
        WriteDictionary(
            ("Sprint", "スプリント"),
            ("Make Camp", "野営"),
            ("[disabled]", "[無効]"),
            ("on", "オン"),
            ("off", "オフ"));

        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyAbilityBarButtonTextTarget), nameof(DummyAbilityBarButtonTextTarget.Update)),
                postfix: new HarmonyMethod(RequirePatchMethod("Postfix", typeof(object))));

            var target = new DummyAbilityBarButtonTextTarget();
            var sprint = new DummyAbilityBarButton("Sprint [off] <1>");
            var camp = new DummyAbilityBarButton("Make Camp <2>");
            target.AbilityButtons.Add(sprint);
            target.AbilityButtons.Add(camp);

            target.Update();

            var rawSprint = Translator.Translate("Sprint [off] <1>");
            var translatedSprint = Translator.Translate("スプリント [オフ] <1>");
            var rawCamp = Translator.Translate("Make Camp <2>");
            var translatedCamp = Translator.Translate("野営 <2>");

            Assert.Multiple(() =>
            {
                Assert.That(sprint.Text.text, Is.EqualTo("スプリント [オフ] <1>"));
                Assert.That(camp.Text.text, Is.EqualTo("野営 <2>"));
                Assert.That(rawSprint, Is.EqualTo("スプリント [オフ] <1>"));
                Assert.That(translatedSprint, Is.EqualTo("スプリント [オフ] <1>"));
                Assert.That(rawCamp, Is.EqualTo("野営 <2>"));
                Assert.That(translatedCamp, Is.EqualTo("野営 <2>"));
                Assert.That(Translator.GetMissingKeyHitCountForTests("Sprint [off] <1>"), Is.EqualTo(0));
                Assert.That(Translator.GetMissingKeyHitCountForTests("スプリント [オフ] <1>"), Is.EqualTo(0));
                Assert.That(Translator.GetMissingKeyHitCountForTests("Make Camp <2>"), Is.EqualTo(0));
                Assert.That(Translator.GetMissingKeyHitCountForTests("野営 <2>"), Is.EqualTo(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_UsesSharedAbilityManagerLeavesForDuplicateAbilityBarNames()
    {
        WriteDictionary(
            ("Dominate Creature", "支配"),
            ("Power Devices", "発電"));

        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyAbilityBarButtonTextTarget), nameof(DummyAbilityBarButtonTextTarget.Update)),
                postfix: new HarmonyMethod(RequirePatchMethod("Postfix", typeof(object))));

            var target = new DummyAbilityBarButtonTextTarget();
            var dominate = new DummyAbilityBarButton("Dominate Creature");
            var powerDevices = new DummyAbilityBarButton("Power Devices");
            target.AbilityButtons.Add(dominate);
            target.AbilityButtons.Add(powerDevices);

            target.Update();

            Assert.Multiple(() =>
            {
                Assert.That(dominate.Text.text, Is.EqualTo("支配"));
                Assert.That(powerDevices.Text.text, Is.EqualTo("発電"));
                Assert.That(Translator.GetMissingKeyHitCountForTests("Dominate Creature"), Is.EqualTo(0));
                Assert.That(Translator.GetMissingKeyHitCountForTests("Power Devices"), Is.EqualTo(0));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(AbilityBarButtonTextTranslationPatch),
                        "AbilityBar.ButtonText"),
                    Is.EqualTo(2));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_DoesNotLogMissingKeysForUnregisteredSuffixTokens()
    {
        WriteDictionary(("Sprint", "スプリント"));

        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyAbilityBarButtonTextTarget), nameof(DummyAbilityBarButtonTextTarget.Update)),
                postfix: new HarmonyMethod(RequirePatchMethod("Postfix", typeof(object))));

            var target = new DummyAbilityBarButtonTextTarget();
            var sprint = new DummyAbilityBarButton("Sprint [off] <1>");
            target.AbilityButtons.Add(sprint);

            target.Update();

            Assert.Multiple(() =>
            {
                Assert.That(sprint.Text.text, Is.EqualTo("スプリント [off] <1>"));
                Assert.That(Translator.GetMissingKeyHitCountForTests("[disabled]"), Is.EqualTo(0));
                Assert.That(Translator.GetMissingKeyHitCountForTests("on"), Is.EqualTo(0));
                Assert.That(Translator.GetMissingKeyHitCountForTests("off"), Is.EqualTo(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_LeavesDynamicAbilityButtonTextEnglish_WhenBaseLeafIsMissing()
    {
        WriteDictionary(("Joppa", "ジョッパ"));

        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyAbilityBarButtonTextTarget), nameof(DummyAbilityBarButtonTextTarget.Update)),
                postfix: new HarmonyMethod(RequirePatchMethod("Postfix", typeof(object))));

            var target = new DummyAbilityBarButtonTextTarget();
            var discharge = new DummyAbilityBarButton("&CDischarge [3 charge]");
            var lase = new DummyAbilityBarButton("&CLase (4 charges)");
            var recoilToJoppa = new DummyAbilityBarButton("Recoil to Joppa");
            target.AbilityButtons.Add(discharge);
            target.AbilityButtons.Add(lase);
            target.AbilityButtons.Add(recoilToJoppa);

            target.Update();

            Assert.Multiple(() =>
            {
                Assert.That(discharge.Text.text, Is.EqualTo("&CDischarge [3 charge]"));
                Assert.That(lase.Text.text, Is.EqualTo("&CLase (4 charges)"));
                Assert.That(recoilToJoppa.Text.text, Is.EqualTo("Recoil to Joppa"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(AbilityBarButtonTextTranslationPatch),
                        "AbilityBar.ButtonText"),
                    Is.EqualTo(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static MethodInfo RequireMethod(Type type, string methodName)
    {
        var method = AccessTools.Method(type, methodName);
        Assert.That(method, Is.Not.Null, $"Method not found: {type.FullName}.{methodName}");
        return method!;
    }

    private static MethodInfo RequirePatchMethod(string methodName, params Type[] parameterTypes)
    {
        var patchType = typeof(Translator).Assembly.GetType("QudJP.Patches.AbilityBarButtonTextTranslationPatch", throwOnError: false);
        Assert.That(patchType, Is.Not.Null, "AbilityBarButtonTextTranslationPatch type not found.");

        var method = patchType!.GetMethod(
            methodName,
            BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: parameterTypes,
            modifiers: null);
        Assert.That(method, Is.Not.Null, $"Method not found: {patchType.FullName}.{methodName}");
        return method!;
    }

    private void WriteDictionary(params (string key, string text)[] entries)
    {
        var path = Path.Combine(tempDirectory, "abilitybar-button-text-l2.ja.json");
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
}
