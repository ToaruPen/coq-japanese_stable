using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class ActiveEffectMessageFrameOwnerTranslationPatchTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private string tempDirectory = null!;
    private string? lastMessage;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-active-effect-message-frame-owner-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        File.WriteAllText(Path.Combine(tempDirectory, "ui-test.ja.json"), "{\"entries\":[]}\n", Utf8WithoutBom);
        File.WriteAllText(
            Path.Combine(tempDirectory, "ui-popup.ja.json"),
            """
            {"entries":[
                {
                    "key":"{{G|Your heart restarts!}}",
                    "context":"XRL.UI.Popup.Show.Message",
                    "text":"{{G|心臓が再起動する！}}"
                },
                {
                    "key":"{{G|Your hearts restart!}}",
                    "context":"XRL.UI.Popup.Show.Message",
                    "text":"{{G|心臓たちが再起動する！}}"
                }
            ]}
            """,
            Utf8WithoutBom);
        MessagePatternTranslator.ResetForTests();
        MessagePatternTranslator.SetLeafFileForTests(Path.Combine(
            QudJP.Tests.L1.TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Localization",
            "Dictionaries",
            "ui-messagelog-leaf.ja.json"));
        MessageFrameTranslator.ResetForTests();
        MessageFrameTranslator.SetDictionaryPathForTests(Path.Combine(
            QudJP.Tests.L1.TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Localization",
            "MessageFrames",
            "verbs.ja.json"));
        XDidYTranslationPatch.SetMessageDispatcherForTests((_, message, _, _) => lastMessage = message);
        DynamicTextObservability.ResetForTests();
        DummyXDidYTarget.Reset();
        DummyMessageQueue.Reset();
        DummyPopupShow.Reset();
        lastMessage = null;
    }

    [TearDown]
    public void TearDown()
    {
        XDidYTranslationPatch.SetMessageDispatcherForTests(null);
        MessageFrameTranslator.ResetForTests();
        MessagePatternTranslator.ResetForTests();
        Translator.ResetForTests();
        DynamicTextObservability.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [TestCase(
        nameof(DummyActiveEffectMessageFrameOwner.ImmobilizedApply),
        "are",
        "immobilized",
        "!",
        "snapjawは動けなくなった！")]
    [TestCase(
        nameof(DummyActiveEffectMessageFrameOwner.StuckApply),
        "are",
        "stuck in adhesive foam",
        "!",
        "snapjawはadhesive foamにはまって動けなくなった！")]
    [TestCase(
        nameof(DummyActiveEffectMessageFrameOwner.StuckApply),
        "are",
        "grabbed by chrome pyramid",
        "!",
        "snapjawはchrome pyramidにつかまれた！")]
    [TestCase(
        nameof(DummyActiveEffectMessageFrameOwner.LatchedOntoBeginTakeAction),
        "break",
        "free from being latched onto",
        "!",
        "snapjawはbeing latched ontoから抜け出した！")]
    [TestCase(
        nameof(DummyActiveEffectMessageFrameOwner.CardiacArrestRemove),
        "look",
        "less stricken",
        ".",
        "snapjawは苦痛がやわらいだ。")]
    public void OwnerPatch_RecordsMessageFrameTranslation_WhenActiveEffectOwnerIsPatched(
        string ownerMethodName,
        string verb,
        string extra,
        string endMark,
        string expected)
    {
        WithPatchedOwnerAndXDidY(ownerMethodName, () =>
        {
            var target = new DummyActiveEffectMessageFrameOwner(verb, extra, endMark);

            InvokeOwner(target, ownerMethodName);

            Assert.Multiple(() =>
            {
                Assert.That(lastMessage, Is.EqualTo(MessageFrameTranslator.MarkDirectTranslation(expected)));
                Assert.That(OwnerHitCount(), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void OwnerPatch_DoesNotRecordMessageFrameTranslation_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchXDidY(harmony);

            var target = new DummyActiveEffectMessageFrameOwner("are", "immobilized", "!");
            target.ImmobilizedApply();

            Assert.Multiple(() =>
            {
                Assert.That(lastMessage, Is.EqualTo(MessageFrameTranslator.MarkDirectTranslation("snapjawは動けなくなった！")));
                Assert.That(OwnerHitCount(), Is.EqualTo(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [TestCase(
        nameof(DummyActiveEffectMessageFrameOwner.LovesickApply),
        "fall",
        "in love with",
        null,
        "chrome idol",
        "!",
        "snapjawはchrome idolに恋をした！")]
    [TestCase(
        nameof(DummyActiveEffectMessageFrameOwner.BeguiledApply),
        "ogle",
        null,
        "lovingly",
        "you",
        ".",
        "snapjawはあなたをうっとりと見つめた。")]
    [TestCase(
        nameof(DummyActiveEffectMessageFrameOwner.ProselytizedApply),
        "convince",
        null,
        "to join you",
        "snapjaw",
        "!",
        "snapjawはsnapjawを説得して仲間に加えた！")]
    [TestCase(
        nameof(DummyActiveEffectMessageFrameOwner.RebukedApply),
        "rebuke",
        null,
        "into submission",
        "clockwork beetle",
        ".",
        "snapjawはclockwork beetleを叱責して従わせた。")]
    public void OwnerPatch_RecordsXDidYToZMessageFrameTranslation_WhenSocialActiveEffectOwnerIsPatched(
        string ownerMethodName,
        string verb,
        string? preposition,
        string? extra,
        string objectText,
        string endMark,
        string expected)
    {
        WithPatchedOwnerAndXDidYToZ(ownerMethodName, () =>
        {
            var target = new DummyActiveEffectMessageFrameOwner(
                verb,
                preposition,
                extra,
                objectText,
                endMark);

            InvokeOwner(target, ownerMethodName);

            Assert.Multiple(() =>
            {
                Assert.That(lastMessage, Is.EqualTo(MessageFrameTranslator.MarkDirectTranslation(expected)));
                Assert.That(XDidYToZOwnerHitCount(), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void OwnerPatch_RecordsCardiacArrestRemovePlayerPopupTranslations_WhenOwnerIsPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);
            PatchOwner(harmony, nameof(DummyActiveEffectMessageFrameOwner.CardiacArrestRemovePlayerSideEffects));

            var target = new DummyActiveEffectMessageFrameOwner("look", "less stricken", ".");

            target.CardiacArrestRemovePlayerSideEffects();

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("あなたはふらつき、衰弱を感じる。"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(PopupShowTranslationPatch),
                        ActiveEffectMessageFrameOwnerTranslationPatch.Family + ".CardiacArrestRemove.Popup"),
                    Is.EqualTo(2));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(PopupShowTranslationPatch),
                        ActiveEffectMessageFrameOwnerTranslationPatch.Family + ".CardiacArrestRemove.IllApplyPopup"),
                    Is.EqualTo(1));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static int OwnerHitCount()
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            "Messaging.XDidY",
            ActiveEffectMessageFrameOwnerTranslationPatch.Family);
    }

    private static int XDidYToZOwnerHitCount()
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            "Messaging.XDidYToZ",
            ActiveEffectMessageFrameOwnerTranslationPatch.Family);
    }

    private static void WithPatchedOwnerAndXDidY(string ownerMethodName, TestDelegate action)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchXDidY(harmony);
            PatchOwner(harmony, ownerMethodName);

            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void WithPatchedOwnerAndXDidYToZ(string ownerMethodName, TestDelegate action)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchXDidYToZ(harmony);
            PatchOwner(harmony, ownerMethodName);

            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void PatchXDidY(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyXDidYTarget), nameof(DummyXDidYTarget.XDidY)),
            prefix: new HarmonyMethod(RequireMethod(typeof(XDidYTranslationPatch), nameof(XDidYTranslationPatch.PrefixXDidYForTests))));
    }

    private static void PatchXDidYToZ(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyXDidYTarget), nameof(DummyXDidYTarget.XDidYToZ)),
            prefix: new HarmonyMethod(RequireMethod(typeof(XDidYTranslationPatch), nameof(XDidYTranslationPatch.PrefixXDidYToZForTests))));
    }

    private static void PatchOwner(Harmony harmony, string ownerMethodName)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyActiveEffectMessageFrameOwner), ownerMethodName),
            prefix: new HarmonyMethod(RequireMethod(typeof(ActiveEffectMessageFrameOwnerTranslationPatch), nameof(ActiveEffectMessageFrameOwnerTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(typeof(ActiveEffectMessageFrameOwnerTranslationPatch), nameof(ActiveEffectMessageFrameOwnerTranslationPatch.Finalizer), typeof(Exception))));
    }

    private static void PatchPopupShow(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(
                typeof(DummyPopupShow),
                nameof(DummyPopupShow.Show),
                typeof(string),
                typeof(string),
                typeof(string),
                typeof(bool),
                typeof(bool),
                typeof(bool),
                typeof(bool)),
            prefix: new HarmonyMethod(RequireMethod(
                typeof(PopupShowTranslationPatch),
                nameof(PopupShowTranslationPatch.Prefix),
                typeof(string).MakeByRefType(),
                typeof(MethodBase))));
    }

    private static void InvokeOwner(DummyActiveEffectMessageFrameOwner target, string ownerMethodName)
    {
        _ = RequireMethod(typeof(DummyActiveEffectMessageFrameOwner), ownerMethodName).Invoke(target, null);
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

    private static string CreateHarmonyId()
    {
        return $"qudjp.tests.{Guid.NewGuid():N}";
    }

    private sealed class DummyActiveEffectMessageFrameOwner
    {
        private readonly string verb;
        private readonly string? preposition;
        private readonly string? extra;
        private readonly string? objectText;
        private readonly string endMark;

        public DummyActiveEffectMessageFrameOwner(string verb, string extra, string endMark)
            : this(verb, null, extra, null, endMark)
        {
        }

        public DummyActiveEffectMessageFrameOwner(
            string verb,
            string? preposition,
            string? extra,
            string? objectText,
            string endMark)
        {
            this.verb = verb;
            this.preposition = preposition;
            this.extra = extra;
            this.objectText = objectText;
            this.endMark = endMark;
        }

        public void ImmobilizedApply()
        {
            EmitDidX();
        }

        public void StuckApply()
        {
            EmitDidX();
        }

        public void LatchedOntoBeginTakeAction()
        {
            EmitDidX();
        }

        public void CardiacArrestRemove()
        {
            EmitDidX();
        }

        public void CardiacArrestRemovePlayerSideEffects()
        {
            _ = verb;
            DummyPopupShow.Show("{{G|Your heart restarts!}}");
            DummyPopupShow.Show("{{G|Your hearts restart!}}");
            DummyPopupShow.Show("You feel shaken and infirm.");
        }

        public void LovesickApply()
        {
            EmitXDidYToZ();
        }

        public void BeguiledApply()
        {
            EmitXDidYToZ();
        }

        public void ProselytizedApply()
        {
            EmitXDidYToZ();
        }

        public void RebukedApply()
        {
            EmitXDidYToZ();
        }

        private void EmitDidX()
        {
            DummyXDidYTarget.XDidY(
                Actor: null,
                Verb: verb,
                Extra: extra,
                EndMark: endMark,
                SubjectOverride: "snapjaw",
                AlwaysVisible: true);
        }

        private void EmitXDidYToZ()
        {
            DummyXDidYTarget.XDidYToZ(
                Actor: null,
                Verb: verb,
                Preposition: preposition,
                Object: objectText,
                Extra: extra,
                EndMark: endMark,
                SubjectOverride: "snapjaw",
                AlwaysVisible: true);
        }
    }
}
