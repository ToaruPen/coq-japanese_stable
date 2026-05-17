using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using QudJP;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class FloatingYellTextTranslationPatchTests
{
    private string tempDirectory = null!;
    private string patternFilePath = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-floating-yell-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        patternFilePath = Path.Combine(tempDirectory, "messages.ja.json");

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        MessagePatternTranslator.ResetForTests();
        MessagePatternTranslator.SetPatternFileForTests(patternFilePath);
        WritePatternDictionary(("^E-Ros yells, 'I'm coming, (.+?)!'$", "E-Rosは「今行くよ、{0}！」と叫んだ"));
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(true);
        DynamicTextObservability.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        DummyMessageQueue.Reset();
        DummyParticleTextTarget.Reset();
        DummyCombatJuiceRenderer.Reset();
        CombatJuiceFloatingTextRenderer.SetRendererForTests(DummyCombatJuiceRenderer.Render);
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        MessagePatternTranslator.ResetForTests();
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(null);
        DynamicTextObservability.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        CombatJuiceFloatingTextRenderer.SetRendererForTests(null);
        DummyMessageQueue.Reset();
        DummyParticleTextTarget.Reset();
        DummyCombatJuiceRenderer.Reset();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [TestCase(
        "Who ventures into the Great Salt Desert, and nearer the Six Day Stilt?",
        "狂信者は{{W|「大塩砂漠へ足を踏み入れ、六日のスティルトに近づく者は誰だ？」}}と叫んだ",
        "大塩砂漠へ足を踏み入れ、六日のスティルトに近づく者は誰だ？")]
    [TestCase(
        "Hmm, what of your artifacts? Make an offering of them to Shekhinah at the Sacred Well.",
        "狂信者は{{W|「ふむ、お前のアーティファクトはどうした？それらを聖なる井戸のシェキーナへ捧げよ。」}}と叫んだ",
        "ふむ、お前のアーティファクトはどうした？それらを聖なる井戸のシェキーナへ捧げよ。")]
    [TestCase(
        "The beauty! My stomach is in stirs.",
        "狂信者は{{W|「なんという美しさだ！腹の底がかき乱される。」}}と叫んだ",
        "なんという美しさだ！腹の底がかき乱される。")]
    [TestCase(
        "Is it a dybbuk that possesses the robot? It should be sacred and still.",
        "狂信者は{{W|「ロボットに憑いているのはディブクか？それは神聖で静止しているべきものだ。」}}と叫んだ",
        "ロボットに憑いているのはディブクか？それは神聖で静止しているべきものだ。")]
    public void JoppaZealot_TranslatesMessageLogAndFloatingText_WhenOwnerPatched(
        string line,
        string expectedMessage,
        string expectedParticle)
    {
        WithPatchedOwnerQueueEmitAndParticle(
            typeof(JoppaZealotTranslationPatch),
            RequireMethod(typeof(DummyJoppaZealotProducer), nameof(DummyJoppaZealotProducer.ZealotDeclaim), typeof(DummyGameObject), typeof(bool)),
            () =>
            {
                new DummyJoppaZealotProducer(line).ZealotDeclaim(new DummyGameObject(), Dialog: false);

                Assert.Multiple(() =>
                {
                    Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expectedMessage));
                    Assert.That(DummyParticleTextTarget.LastText, Is.Empty);
                    Assert.That(DummyCombatJuiceRenderer.LastText, Is.EqualTo(expectedParticle));
                    Assert.That(QueueHitCount(typeof(JoppaZealotTranslationPatch), "Yell"), Is.EqualTo(1));
                    Assert.That(ParticleHitCount(typeof(JoppaZealotTranslationPatch), "FloatingSpeech"), Is.EqualTo(1));
                });
            });
    }

    [TestCase(
        "Make an offering at the Argent Well! Pay homage to your Fathers!",
        "狂信者が{{W|「白銀の泉に捧げものをせよ！父祖を称えよ！」}}と叫んだ",
        "白銀の泉に捧げものをせよ！父祖を称えよ！")]
    [TestCase(
        "Cast down your artifacts! You are not worthy of their make!",
        "狂信者が{{W|「アーティファクトを打ち捨てよ！貴様にそれを持つ資格はない！」}}と叫んだ",
        "アーティファクトを打ち捨てよ！貴様にそれを持つ資格はない！")]
    [TestCase(
        "Piety compels you to deliver your sacred relics to the priests in the cathedral! Cleanse them of your filth!",
        "狂信者が{{W|「信仰心があるなら聖遺物を大聖堂の司祭に届けよ！貴様の穢れを清めるのだ！」}}と叫んだ",
        "信仰心があるなら聖遺物を大聖堂の司祭に届けよ！貴様の穢れを清めるのだ！")]
    [TestCase(
        "The Machine commands that you exorcise robots and bring their sacred husks here!",
        "狂信者が{{W|「機械の御意志により、ロボットを祓い清め、聖なる殻をここへ持って来い！」}}と叫んだ",
        "機械の御意志により、ロボットを祓い清め、聖なる殻をここへ持って来い！")]
    public void SixDayZealot_TranslatesMessageLogAndFloatingText_WhenOwnerPatched(
        string line,
        string expectedMessage,
        string expectedParticle)
    {
        WithPatchedOwnerQueueEmitAndParticle(
            typeof(SixDayZealotTranslationPatch),
            RequireMethod(typeof(DummySixDayZealotProducer), nameof(DummySixDayZealotProducer.ZealotDeclaim), typeof(DummyGameObject), typeof(bool)),
            () =>
            {
                new DummySixDayZealotProducer(line).ZealotDeclaim(new DummyGameObject(), Dialog: false);

                Assert.Multiple(() =>
                {
                    Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expectedMessage));
                    Assert.That(DummyParticleTextTarget.LastText, Is.Empty);
                    Assert.That(DummyCombatJuiceRenderer.LastText, Is.EqualTo(expectedParticle));
                    Assert.That(QueueHitCount(typeof(SixDayZealotTranslationPatch), "Yell"), Is.EqualTo(1));
                    Assert.That(ParticleHitCount(typeof(SixDayZealotTranslationPatch), "FloatingSpeech"), Is.EqualTo(1));
                });
            });
    }

    [Test]
    public void ErosTeleportation_TranslatesMessageLogAndFloatingText_WhenOwnerPatched()
    {
        WithPatchedOwnerQueueEmitAndParticle(
            typeof(ErosTeleportationTranslationPatch),
            RequireMethod(typeof(DummyErosTeleportationProducer), nameof(DummyErosTeleportationProducer.Cast)),
            () =>
            {
                new DummyErosTeleportationProducer("リーダー").Cast();

                Assert.Multiple(() =>
                {
                    Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("E-Rosは{{W|「今行くよ、リーダー！」}}と叫んだ"));
                    Assert.That(DummyParticleTextTarget.LastText, Is.Empty);
                    Assert.That(DummyCombatJuiceRenderer.LastText, Is.EqualTo("今行くよ、リーダー！"));
                    Assert.That(DummyCombatJuiceRenderer.LastColor.r, Is.EqualTo(1f));
                    Assert.That(DummyCombatJuiceRenderer.LastColor.g, Is.EqualTo(1f));
                    Assert.That(DummyCombatJuiceRenderer.LastColor.b, Is.EqualTo(0f));
                    Assert.That(QueueHitCount(typeof(ErosTeleportationTranslationPatch), "Yell"), Is.EqualTo(1));
                    Assert.That(EmitMessageHitCount("EmitMessage"), Is.Zero);
                    Assert.That(ParticleHitCount(typeof(ErosTeleportationTranslationPatch), "FloatingSpeech"), Is.EqualTo(1));
                });
            });
    }

    [Test]
    public void LongBladesCore_TranslatesFloatingEnGarde_WhenOwnerPatched()
    {
        WithPatchedOwnerAndParticle(
            typeof(LongBladesCoreTranslationPatch),
            RequireMethod(typeof(DummyLongBladesCoreParticleProducer), nameof(DummyLongBladesCoreParticleProducer.FireEvent), typeof(DummyEvent)),
            () =>
            {
                new DummyLongBladesCoreParticleProducer().FireEvent(new DummyEvent());

                Assert.Multiple(() =>
                {
                    Assert.That(DummyParticleTextTarget.LastText, Is.Empty);
                    Assert.That(DummyCombatJuiceRenderer.LastText, Is.EqualTo("構えよ！"));
                    Assert.That(DummyCombatJuiceRenderer.LastFloatLength, Is.EqualTo(-8f));
                    Assert.That(ParticleHitCount(typeof(LongBladesCoreTranslationPatch), "EnGarde"), Is.EqualTo(1));
                });
            });
    }

    [Test]
    public void PreacherHomily_NormalizesMessageLogAndFloatingQuoteFrame_WhenOwnerPatched()
    {
        WithPatchedOwnerQueueEmitAndParticle(
            typeof(PreacherHomilyTranslationPatch),
            RequireMethod(typeof(DummyPreacherHomilyProducer), nameof(DummyPreacherHomilyProducer.PreacherHomily), typeof(DummyGameObject), typeof(bool)),
            () =>
            {
                new DummyPreacherHomilyProducer("これは日本語の一節").PreacherHomily(new DummyGameObject(), Dialog: false);

                Assert.Multiple(() =>
                {
                    Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("説教者は言う、{{W|「これは日本語の一節」}}"));
                    Assert.That(DummyParticleTextTarget.LastText, Is.Empty);
                    Assert.That(DummyCombatJuiceRenderer.LastText, Is.EqualTo("「これは日本語の一節」"));
                    Assert.That(QueueHitCount(typeof(PreacherHomilyTranslationPatch), "QuotedHomilyFrame"), Is.EqualTo(1));
                    Assert.That(ParticleHitCount(typeof(PreacherHomilyTranslationPatch), "FloatingHomilyFrame"), Is.EqualTo(1));
                });
            });
    }

    [Test]
    public void OwnerTranslationScope_RestoresOuterParticleTranslator_WhenNestedOwnerExits()
    {
        WithPatchedOwnerAndParticle(
            typeof(JoppaZealotTranslationPatch),
            RequireMethod(typeof(DummyNestedJoppaScopeProducer), nameof(DummyNestedJoppaScopeProducer.ZealotDeclaim), typeof(DummyGameObject), typeof(bool)),
            () =>
            {
                var producer = new DummyNestedJoppaScopeProducer(() =>
                    WithPatchedOwnerQueueEmitOnly(
                        typeof(PreacherHomilyTranslationPatch),
                        RequireMethod(typeof(DummyPreacherHomilyProducer), nameof(DummyPreacherHomilyProducer.PreacherHomily), typeof(DummyGameObject), typeof(bool)),
                        () =>
                        {
                            new DummyPreacherHomilyProducer("内側の説教").PreacherHomily(new DummyGameObject(), Dialog: false);

                            Assert.Multiple(() =>
                            {
                                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("説教者は言う、{{W|「内側の説教」}}"));
                                Assert.That(DummyParticleTextTarget.LastText, Is.Empty);
                                Assert.That(DummyCombatJuiceRenderer.LastText, Is.EqualTo("「内側の説教」"));
                                Assert.That(QueueHitCount(typeof(PreacherHomilyTranslationPatch), "QuotedHomilyFrame"), Is.EqualTo(1));
                                Assert.That(ParticleHitCount(typeof(PreacherHomilyTranslationPatch), "FloatingHomilyFrame"), Is.EqualTo(1));
                                Assert.That(ParticleHitCount(typeof(JoppaZealotTranslationPatch), "FloatingSpeech"), Is.EqualTo(1));
                            });
                        }));

                producer.ZealotDeclaim(new DummyGameObject(), Dialog: false);

                Assert.Multiple(() =>
                {
                    Assert.That(DummyParticleTextTarget.LastText, Is.Empty);
                    Assert.That(DummyCombatJuiceRenderer.LastText, Is.EqualTo("なんという美しさだ！腹の底がかき乱される。"));
                    Assert.That(ParticleHitCount(typeof(JoppaZealotTranslationPatch), "FloatingSpeech"), Is.EqualTo(2));
                    Assert.That(ParticleHitCount(typeof(PreacherHomilyTranslationPatch), "FloatingHomilyFrame"), Is.EqualTo(1));
                });
            });
    }

    [Test]
    public void CanticlesChromaic_NormalizesFloatingQuoteFrame_WhenOwnerPatched()
    {
        WithPatchedOwnerAndParticle(
            typeof(CanticlesChromaicParticleTextTranslationPatch),
            RequireMethod(typeof(DummyCanticlesChromaicProducer), nameof(DummyCanticlesChromaicProducer.UseToken)),
            () =>
            {
                new DummyCanticlesChromaicProducer("銀の父祖を讃えよ").UseToken();

                Assert.Multiple(() =>
                {
                    Assert.That(DummyParticleTextTarget.LastText, Is.Empty);
                    Assert.That(DummyCombatJuiceRenderer.LastText, Is.EqualTo("「銀の父祖を讃えよ」"));
                    Assert.That(ParticleHitCount(typeof(CanticlesChromaicParticleTextTranslationPatch), "FloatingCanticleFrame"), Is.EqualTo(1));
                });
            });
    }

    [Test]
    public void ParticleText_DoesNotTranslateSupportedText_WhenOwnerAbsent()
    {
        WithPatchedParticleOnly(() =>
            DummyParticleTextTarget.Current.ParticleText("{{W|Who ventures into the Great Salt Desert, and nearer the Six Day Stilt?}}", 0.4f, 0.2f, ' ', IgnoreVisibility: true));

        Assert.Multiple(() =>
        {
            Assert.That(DummyParticleTextTarget.LastText, Is.EqualTo("{{W|Who ventures into the Great Salt Desert, and nearer the Six Day Stilt?}}"));
            Assert.That(ParticleHitCount(typeof(JoppaZealotTranslationPatch), "FloatingSpeech"), Is.Zero);
        });
    }

    [Test]
    public void ParticleText_DoesNotTranslateFloatLifeOverload_WhenOwnerAbsent()
    {
        WithPatchedParticleOnly(() =>
            DummyParticleTextTarget.Current.ParticleText("{{W|Who ventures into the Great Salt Desert, and nearer the Six Day Stilt?}}", 1.5f, 24));

        Assert.Multiple(() =>
        {
            Assert.That(DummyParticleTextTarget.LastText, Is.EqualTo("{{W|Who ventures into the Great Salt Desert, and nearer the Six Day Stilt?}}"));
            Assert.That(ParticleHitCount(typeof(JoppaZealotTranslationPatch), "FloatingSpeech"), Is.Zero);
        });
    }

    [Test]
    public void OwnerRoute_DoesNotClaimUnsupportedParticleText()
    {
        WithPatchedOwnerAndParticle(
            typeof(JoppaZealotTranslationPatch),
            RequireMethod(typeof(DummyUnsupportedParticleOwner), nameof(DummyUnsupportedParticleOwner.ZealotDeclaim), typeof(DummyGameObject), typeof(bool)),
            () => new DummyUnsupportedParticleOwner().ZealotDeclaim(new DummyGameObject(), Dialog: false));

        Assert.Multiple(() =>
        {
            Assert.That(DummyParticleTextTarget.LastText, Is.EqualTo("{{W|unknown shout}}"));
            Assert.That(ParticleHitCount(typeof(JoppaZealotTranslationPatch), "FloatingSpeech"), Is.Zero);
        });
    }

    [TestCase("")]
    [TestCase("\u0001Some text")]
    [TestCase("I'm coming, X!")]
    [TestCase("En garde!")]
    public void ParticleText_DoesNotTranslateUnsupportedText_WhenOwnerAbsent(string text)
    {
        WithPatchedParticleOnly(() =>
            DummyParticleTextTarget.Current.ParticleText(text, IgnoreVisibility: true));

        Assert.Multiple(() =>
        {
            Assert.That(DummyParticleTextTarget.LastText, Is.EqualTo(text));
            Assert.That(ParticleHitCount(typeof(JoppaZealotTranslationPatch), "FloatingSpeech"), Is.Zero);
        });
    }

    [TestCase("")]
    [TestCase("\u0001Some text")]
    [TestCase("I'm coming, X!")]
    [TestCase("En garde!")]
    public void OwnerRoute_DoesNotClaimUnsupportedParticleTextValue(string text)
    {
        WithPatchedOwnerAndParticle(
            typeof(JoppaZealotTranslationPatch),
            RequireMethod(typeof(DummyUnsupportedParticleOwner), nameof(DummyUnsupportedParticleOwner.ZealotDeclaim), typeof(DummyGameObject), typeof(bool)),
            () => new DummyUnsupportedParticleOwner(text).ZealotDeclaim(new DummyGameObject(), Dialog: false));

        Assert.Multiple(() =>
        {
            Assert.That(DummyParticleTextTarget.LastText, Is.EqualTo(text));
            Assert.That(ParticleHitCount(typeof(JoppaZealotTranslationPatch), "FloatingSpeech"), Is.Zero);
        });
    }

    private static void WithPatchedOwnerQueueEmitAndParticle(Type ownerPatchType, MethodInfo ownerMethod, Action action)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchQueue(harmony);
            PatchMessagingEmit(harmony);
            PatchParticleText(harmony);
            PatchOwner(harmony, ownerMethod, ownerPatchType);
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void WithPatchedOwnerAndParticle(Type ownerPatchType, MethodInfo ownerMethod, Action action)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchParticleText(harmony);
            PatchOwner(harmony, ownerMethod, ownerPatchType);
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void WithPatchedOwnerQueueEmitOnly(Type ownerPatchType, MethodInfo ownerMethod, Action action)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchQueue(harmony);
            PatchMessagingEmit(harmony);
            PatchOwner(harmony, ownerMethod, ownerPatchType);
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void WithPatchedParticleOnly(Action action)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchParticleText(harmony);
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void PatchQueue(Harmony harmony)
    {
        var target = RequireMethod(
            typeof(DummyMessageQueue),
            nameof(DummyMessageQueue.AddPlayerMessage),
            typeof(string),
            typeof(string),
            typeof(bool));
        harmony.Patch(
            original: target,
            prefix: new HarmonyMethod(RequireMethod(
                typeof(CombatAndLogMessageQueuePatch),
                nameof(CombatAndLogMessageQueuePatch.Prefix),
                typeof(string).MakeByRefType(),
                typeof(string),
                typeof(bool))));
        harmony.Patch(
            original: target,
            prefix: new HarmonyMethod(RequireMethod(
                typeof(MessageLogPatch),
                nameof(MessageLogPatch.Prefix),
                typeof(string).MakeByRefType(),
                typeof(string),
                typeof(bool))));
    }

    private static void PatchMessagingEmit(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(
                typeof(DummyMessagingEmitMessageTarget),
                nameof(DummyMessagingEmitMessageTarget.EmitMessage),
                typeof(DummyGameObject),
                typeof(string),
                typeof(char),
                typeof(bool),
                typeof(bool),
                typeof(bool),
                typeof(DummyGameObject),
                typeof(DummyGameObject)),
            prefix: new HarmonyMethod(RequireMethod(typeof(GameObjectEmitMessageTranslationPatch), nameof(GameObjectEmitMessageTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(typeof(GameObjectEmitMessageTranslationPatch), nameof(GameObjectEmitMessageTranslationPatch.Finalizer), typeof(Exception))));
    }

    private static void PatchParticleText(Harmony harmony)
    {
        var prefix = new HarmonyMethod(RequireMethod(
            typeof(GameObjectParticleTextTranslationPatch),
            nameof(GameObjectParticleTextTranslationPatch.Prefix),
            typeof(object),
            typeof(string).MakeByRefType(),
            typeof(object[])));

        harmony.Patch(
            original: RequireMethod(typeof(DummyParticleTextTarget), nameof(DummyParticleTextTarget.ParticleText), typeof(string), typeof(bool)),
            prefix: prefix);
        harmony.Patch(
            original: RequireMethod(typeof(DummyParticleTextTarget), nameof(DummyParticleTextTarget.ParticleText), typeof(string), typeof(float), typeof(int)),
            prefix: prefix);
        harmony.Patch(
            original: RequireMethod(typeof(DummyParticleTextTarget), nameof(DummyParticleTextTarget.ParticleText), typeof(string), typeof(float), typeof(float), typeof(char), typeof(bool)),
            prefix: prefix);
        harmony.Patch(
            original: RequireMethod(typeof(DummyParticleTextTarget), nameof(DummyParticleTextTarget.ParticleText), typeof(string), typeof(char), typeof(bool), typeof(float), typeof(float)),
            prefix: prefix);
    }

    private static void PatchOwner(Harmony harmony, MethodInfo original, Type patchType)
    {
        harmony.Patch(
            original: original,
            prefix: new HarmonyMethod(RequireMethod(patchType, "Prefix")),
            finalizer: new HarmonyMethod(RequireMethod(patchType, "Finalizer", typeof(Exception))));
    }

    private static MethodInfo RequireMethod(Type type, string methodName, params Type[] parameterTypes)
    {
        return type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance, null, parameterTypes, null)
            ?? throw new MissingMethodException(type.FullName, methodName);
    }

    private static int QueueHitCount(Type ownerPatchType, string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            "MessageQueue.AddPlayerMessage",
            ownerPatchType.Name + "." + detail);
    }

    private static int ParticleHitCount(Type ownerPatchType, string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            "GameObject.ParticleText",
            ownerPatchType.Name + "." + detail);
    }

    private static int EmitMessageHitCount(string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(GameObjectEmitMessageTranslationPatch),
            detail);
    }

    private static string CreateHarmonyId()
    {
        return "qudjp.floating-yell-text-l2." + Guid.NewGuid().ToString("N");
    }

    private void WritePatternDictionary(params (string pattern, string template)[] entries)
    {
        var builder = new StringBuilder();
        builder.Append('{');
        builder.Append("\"patterns\":[");

        for (var index = 0; index < entries.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append("{\"pattern\":\"");
            builder.Append(EscapeJson(entries[index].pattern));
            builder.Append("\",\"template\":\"");
            builder.Append(EscapeJson(entries[index].template));
            builder.Append("\"}");
        }

        builder.Append("]}");
        builder.AppendLine();

        File.WriteAllText(patternFilePath, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        MessagePatternTranslator.InvalidatePatternFileCacheForTests(patternFilePath);
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private sealed class DummyJoppaZealotProducer(string line)
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void ZealotDeclaim(DummyGameObject who, bool Dialog)
        {
            _ = who;
            _ = Dialog;
            DummyMessagingEmitMessageTarget.MessageToSend = "The zealot yells, {{W|'" + line + "'}}";
            DummyMessagingEmitMessageTarget.EmitMessage(new DummyGameObject(), "unused", ' ', false, true, false);
            DummyParticleTextTarget.Current.ParticleText("{{W|" + line + "}}", 0.4f, 0.2f, ' ', IgnoreVisibility: true);
        }
    }

    private sealed class DummySixDayZealotProducer(string line)
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void ZealotDeclaim(DummyGameObject who, bool Dialog)
        {
            _ = who;
            _ = Dialog;
            DummyMessagingEmitMessageTarget.MessageToSend = "The zealot yells {{W|'" + line + "'}}";
            DummyMessagingEmitMessageTarget.EmitMessage(new DummyGameObject(), "unused", ' ', false, true, false);
            DummyParticleTextTarget.Current.ParticleText("{{W|" + line + "}}", IgnoreVisibility: true);
        }
    }

    private sealed class DummyErosTeleportationProducer(string leader)
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Cast()
        {
            DummyMessagingEmitMessageTarget.MessageToSend = "E-Ros yells, {{W|'I'm coming, " + leader + "!'}}";
            DummyMessagingEmitMessageTarget.EmitMessage(new DummyGameObject(), "unused", ' ', false, true, false);
            DummyParticleTextTarget.Current.ParticleText("I'm coming, " + leader + "!", 'W');
        }
    }

    private sealed class DummyLongBladesCoreParticleProducer
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void FireEvent(DummyEvent eventContext)
        {
            _ = eventContext;
            DummyParticleTextTarget.Current.ParticleText("En garde!", 'W');
        }
    }

    private sealed class DummyPreacherHomilyProducer(string line)
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void PreacherHomily(DummyGameObject who, bool Dialog)
        {
            _ = who;
            _ = Dialog;
            DummyMessagingEmitMessageTarget.MessageToSend = "説教者は言う、{{W|'" + line + "'}}";
            DummyMessagingEmitMessageTarget.EmitMessage(new DummyGameObject(), "unused", ' ', false, true, false);
            DummyParticleTextTarget.Current.ParticleText("{{W|'" + line + "'}}", IgnoreVisibility: true);
        }
    }

    private sealed class DummyNestedJoppaScopeProducer(Action nestedAction)
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void ZealotDeclaim(DummyGameObject who, bool Dialog)
        {
            _ = who;
            _ = Dialog;
            DummyParticleTextTarget.Current.ParticleText("{{W|Who ventures into the Great Salt Desert, and nearer the Six Day Stilt?}}", IgnoreVisibility: true);
            nestedAction();
            DummyParticleTextTarget.Current.ParticleText("{{W|The beauty! My stomach is in stirs.}}", IgnoreVisibility: true);
        }
    }

    private sealed class DummyCanticlesChromaicProducer(string line)
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void UseToken()
        {
            DummyParticleTextTarget.Current.ParticleText("{{W|'" + line + "'}}");
        }
    }

    private sealed class DummyUnsupportedParticleOwner(string text = "{{W|unknown shout}}")
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void ZealotDeclaim(DummyGameObject who, bool Dialog)
        {
            _ = who;
            _ = Dialog;
            DummyParticleTextTarget.Current.ParticleText(text, IgnoreVisibility: true);
        }
    }

    private sealed class DummyParticleTextTarget
    {
        public static DummyParticleTextTarget Current { get; } = new();

        public static string LastText { get; private set; } = string.Empty;

        public static char LastColor { get; private set; }

        public static bool LastIgnoreVisibility { get; private set; }

        private object CellForTests { get; } = new();

        public bool Visible { get; set; } = true;

        private object GetCurrentCell()
        {
            return CellForTests;
        }

        private bool IsVisible()
        {
            return Visible;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void ParticleText(string Text, bool IgnoreVisibility = false)
        {
            LastText = Text;
            LastIgnoreVisibility = IgnoreVisibility;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void ParticleText(string Text, float Velocity, int Life)
        {
            _ = Velocity;
            _ = Life;
            LastText = Text;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void ParticleText(string Text, float xVel, float yVel, char Color = ' ', bool IgnoreVisibility = false)
        {
            _ = xVel;
            _ = yVel;
            LastText = Text;
            LastColor = Color;
            LastIgnoreVisibility = IgnoreVisibility;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void ParticleText(string Text, char Color, bool IgnoreVisibility = false, float juiceDuration = 1.5f, float floatLength = -8f)
        {
            _ = juiceDuration;
            _ = floatLength;
            LastText = Text;
            LastColor = Color;
            LastIgnoreVisibility = IgnoreVisibility;
        }

        public static void Reset()
        {
            LastText = string.Empty;
            LastColor = '\0';
            LastIgnoreVisibility = false;
            Current.Visible = true;
            _ = Current.GetCurrentCell();
            _ = Current.IsVisible();
        }
    }

    private static class DummyCombatJuiceRenderer
    {
        public static string LastText { get; private set; } = string.Empty;

        public static UnityEngine.Color LastColor { get; private set; }

        public static float LastFloatLength { get; private set; }

        public static bool Render(
            object cell,
            string text,
            UnityEngine.Color color,
            float duration,
            float floatLength,
            float scale,
            bool ignoreVisibility,
            object gameObject)
        {
            _ = cell;
            _ = duration;
            _ = scale;
            _ = ignoreVisibility;
            _ = gameObject;
            LastText = text;
            LastColor = color;
            LastFloatLength = floatLength;
            return true;
        }

        public static void Reset()
        {
            LastText = string.Empty;
            LastColor = default;
            LastFloatLength = 0f;
        }
    }
}
