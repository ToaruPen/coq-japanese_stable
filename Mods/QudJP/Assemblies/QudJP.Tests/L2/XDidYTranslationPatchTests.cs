using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class XDidYTranslationPatchTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private string tempDirectory = null!;
    private string dictionaryPath = null!;
    private string? lastMessage;
    private bool lastUsePopup;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-xdidy-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        dictionaryPath = Path.Combine(tempDirectory, "verbs.ja.json");

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        File.WriteAllText(Path.Combine(tempDirectory, "ui-test.ja.json"), "{\"entries\":[]}\n", Utf8WithoutBom);
        MessageFrameTranslator.ResetForTests();
        MessageFrameTranslator.SetDictionaryPathForTests(dictionaryPath);
        XDidYTranslationPatch.SetMessageDispatcherForTests((_, message, _, usePopup) =>
        {
            lastMessage = message;
            lastUsePopup = usePopup;
        });
        DummyXDidYTarget.Reset();
        lastMessage = null;
        lastUsePopup = false;
    }

    [TearDown]
    public void TearDown()
    {
        XDidYTranslationPatch.SetMessageDispatcherForTests(null);
        MessageFrameTranslator.ResetForTests();
        Translator.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void TheTypeName_UsesCanonicalGameNamespace()
    {
        Assert.That(XDidYTranslationPatch.TheTypeName, Is.EqualTo("XRL.The"));
#if HAS_GAME_DLL
        var gameAssemblyName = AssemblyName.GetAssemblyName(Path.Combine(AppContext.BaseDirectory, "Assembly-CSharp.dll"));
        _ = Assembly.Load(gameAssemblyName);
        Assert.Multiple(() =>
        {
            Assert.That(GameTypeResolver.FindType(XDidYTranslationPatch.TheTypeName, "The")?.FullName, Is.EqualTo("XRL.The"));
            Assert.That(AccessTools.TypeByName("XRL.World.The"), Is.Null);
        });
#endif
    }

    [Test]
    public void Prefix_TranslatesXDidYAndSkipsOriginalEnglishAssembly()
    {
        WriteDictionary(tier1: new[] { ("block", "防いだ") });

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyXDidYTarget), nameof(DummyXDidYTarget.XDidY)),
                prefix: new HarmonyMethod(RequireMethod(typeof(XDidYTranslationPatch), nameof(XDidYTranslationPatch.PrefixXDidYForTests))));

            DummyXDidYTarget.XDidY(
                Actor: null,
                Verb: "block",
                SubjectOverride: "熊",
                AlwaysVisible: true);

            Assert.Multiple(() =>
            {
                Assert.That(DummyXDidYTarget.OriginalExecuted, Is.False);
                Assert.That(lastMessage, Is.EqualTo("\u0001熊は防いだ。"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesXDidY_StripsGeneratedSubjectArticle()
    {
        WriteDictionary(tier1: new[] { ("appear", "現れた") });

        var actor = new DummyCurrentDisplayNameTarget(
            "光葉",
            definiteDisplayName: "The 光葉",
            indefiniteDisplayName: "An 光葉");

        RunWithXDidYPatch(() =>
        {
            DummyXDidYTarget.XDidY(
                Actor: actor,
                Verb: "appear",
                AlwaysVisible: true);

            Assert.Multiple(() =>
            {
                Assert.That(DummyXDidYTarget.OriginalExecuted, Is.False);
                Assert.That(lastMessage, Is.EqualTo("\u0001光葉は現れた。"));
            });
        });
    }

    [Test]
    public void Prefix_TranslatesBreatherConeXDidYAndSkipsOriginalEnglishAssembly()
    {
        WriteDictionary(tier2: new[] { ("breath", "a cone of poison gas", "毒ガスを円錐状に吐き出した") });

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyXDidYTarget), nameof(DummyXDidYTarget.XDidY)),
                prefix: new HarmonyMethod(RequireMethod(typeof(XDidYTranslationPatch), nameof(XDidYTranslationPatch.PrefixXDidYForTests))));

            DummyXDidYTarget.XDidY(
                Actor: null,
                Verb: "breath",
                Extra: "a cone of poison gas",
                EndMark: "!",
                SubjectOverride: "熊",
                AlwaysVisible: true);

            Assert.Multiple(() =>
            {
                Assert.That(DummyXDidYTarget.OriginalExecuted, Is.False);
                Assert.That(lastMessage, Is.EqualTo("\u0001熊は毒ガスを円錐状に吐き出した！"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesActorDisplayNameFromCurrentOneSignature()
    {
        WriteUiDictionary(("CanvasWall", "帆布壁"));
        WriteDictionary(tier1: new[] { ("collapse", "崩れた") });

        var actor = new DummyCurrentDisplayNameTarget("CanvasWall");
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyXDidYTarget), nameof(DummyXDidYTarget.XDidY)),
                prefix: new HarmonyMethod(RequireMethod(typeof(XDidYTranslationPatch), nameof(XDidYTranslationPatch.PrefixXDidYForTests))));

            DummyXDidYTarget.XDidY(
                Actor: actor,
                Verb: "collapse",
                AlwaysVisible: true);

            Assert.Multiple(() =>
            {
                Assert.That(DummyXDidYTarget.OriginalExecuted, Is.False);
                Assert.That(lastMessage, Is.EqualTo("\u0001帆布壁は崩れた。"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [TestCase("CanvasWall", "\u0001CanvasWallは崩れた。", false, false)]
    [TestCase("", null, false, true)]
    [TestCase("{{W|CanvasWall}}", "\u0001{{W|帆布壁}}は崩れた。", true, false)]
    [TestCase("\u0001CanvasWall", "\u0001CanvasWallは崩れた。", true, false)]
    public void Prefix_TranslatesActorDisplayNameFromCurrentOneSignature_EdgeCases(
        string displayName,
        string? expectedMessage,
        bool includeDisplayDictionary,
        bool expectedOriginalExecuted)
    {
        if (includeDisplayDictionary)
        {
            WriteUiDictionary(("CanvasWall", "帆布壁"));
        }

        WriteDictionary(tier1: new[] { ("collapse", "崩れた") });
        var actor = new DummyCurrentDisplayNameTarget(displayName);

        RunWithXDidYPatch(() =>
        {
            DummyXDidYTarget.XDidY(
                Actor: actor,
                Verb: "collapse",
                AlwaysVisible: true);
        });

        Assert.Multiple(() =>
        {
            Assert.That(DummyXDidYTarget.OriginalExecuted, Is.EqualTo(expectedOriginalExecuted));
            Assert.That(lastMessage, Is.EqualTo(expectedMessage));
        });
    }

    [Test]
    public void Prefix_TranslatesDeathSubjectFromCurrentOneSignature_NotDisplayNameFallback()
    {
        WriteUiDictionary(("Pig Farmer Convert", "豚農家のメカニマス教徒改宗者"));
        WriteDictionary(tier1: new[] { ("die", "死んだ") });

        var actor = new DummyCurrentDisplayNameTarget(
            displayName: "Pig Farmer Convert",
            displayNameMember: "PigFarmerConvert",
            toStringText: "PigFarmerConvert");
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyXDidYTarget), nameof(DummyXDidYTarget.XDidY)),
                prefix: new HarmonyMethod(RequireMethod(typeof(XDidYTranslationPatch), nameof(XDidYTranslationPatch.PrefixXDidYForTests))));

            DummyXDidYTarget.XDidY(
                Actor: actor,
                Verb: "die",
                EndMark: "!",
                AlwaysVisible: true);

            Assert.Multiple(() =>
            {
                Assert.That(DummyXDidYTarget.OriginalExecuted, Is.False);
                Assert.That(lastMessage, Is.EqualTo("\u0001豚農家のメカニマス教徒改宗者は死んだ！"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesObjectDisplayNameFromCurrentOneSignature_NotDisplayNameFallback()
    {
        WriteUiDictionary(("Wooden Arrow", "木の矢"));
        WriteDictionary(tier2: new[] { ("fire", "at {0}", "{0}を撃った") });

        var target = new DummyCurrentDisplayNameTarget(
            displayName: "Wooden Arrow",
            displayNameMember: "ProjectileBlueprint",
            toStringText: "ProjectileDebugObject");
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyXDidYTarget), nameof(DummyXDidYTarget.XDidYToZ)),
                prefix: new HarmonyMethod(RequireMethod(typeof(XDidYTranslationPatch), nameof(XDidYTranslationPatch.PrefixXDidYToZForTests))));

            DummyXDidYTarget.XDidYToZ(
                Actor: null,
                Verb: "fire",
                Preposition: "at",
                Object: target,
                SubjectOverride: "砲台",
                AlwaysVisible: true);

            Assert.Multiple(() =>
            {
                Assert.That(DummyXDidYTarget.OriginalExecuted, Is.False);
                Assert.That(lastMessage, Is.EqualTo("\u0001砲台は木の矢を撃った。"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesDoubleObjectDisplayNamesFromCurrentOneSignature_NotFallbackText()
    {
        WriteUiDictionary(
            ("Wooden Arrow", "木の矢"),
            ("Pig Farmer Convert", "豚農家のメカニマス教徒改宗者"));
        WriteDictionary(tier3: new[] { ("give", "{0} to {1}", "{0}を{1}に渡した") });

        var item = new DummyCurrentDisplayNameTarget(
            displayName: "Wooden Arrow",
            displayNameMember: "ProjectileBlueprint",
            toStringText: "ProjectileDebugObject");
        var recipient = new DummyCurrentDisplayNameTarget(
            displayName: "Pig Farmer Convert",
            displayNameMember: "PigFarmerConvert",
            toStringText: "CreatureDebugObject");
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyXDidYTarget), nameof(DummyXDidYTarget.WDidXToYWithZ)),
                prefix: new HarmonyMethod(RequireMethod(typeof(XDidYTranslationPatch), nameof(XDidYTranslationPatch.PrefixWDidXToYWithZForTests))));

            DummyXDidYTarget.WDidXToYWithZ(
                Actor: null,
                Verb: "give",
                DirectPreposition: null,
                DirectObject: item,
                IndirectPreposition: "to",
                IndirectObject: recipient,
                SubjectOverride: "商人",
                AlwaysVisible: true);

            Assert.Multiple(() =>
            {
                Assert.That(DummyXDidYTarget.OriginalExecuted, Is.False);
                Assert.That(lastMessage, Is.EqualTo("\u0001商人は木の矢を豚農家のメカニマス教徒改宗者に渡した。"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_FallsBackToOriginalWhenTypedDisplayNameRouteIsMissing()
    {
        WriteDictionary(tier1: new[] { ("collapse", "崩れた") });

        var actor = new DummyDisplayNameFallbackOnlyTarget();
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyXDidYTarget), nameof(DummyXDidYTarget.XDidY)),
                prefix: new HarmonyMethod(RequireMethod(typeof(XDidYTranslationPatch), nameof(XDidYTranslationPatch.PrefixXDidYForTests))));

            DummyXDidYTarget.XDidY(
                Actor: actor,
                Verb: "collapse",
                AlwaysVisible: true);

            Assert.Multiple(() =>
            {
                Assert.That(DummyXDidYTarget.OriginalExecuted, Is.True);
                Assert.That(lastMessage, Is.Null);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_PromotesUsePopupFromDialogWhenHeldByPlayer()
    {
        WriteDictionary(tier1: new[] { ("block", "防いだ") });

        var playerHolder = new DummyVisibilityTarget(isPlayer: true);
        var actor = new DummyVisibilityTarget(holder: playerHolder);

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyXDidYTarget), nameof(DummyXDidYTarget.XDidY)),
                prefix: new HarmonyMethod(RequireMethod(typeof(XDidYTranslationPatch), nameof(XDidYTranslationPatch.PrefixXDidYForTests))));

            DummyXDidYTarget.XDidY(
                Actor: actor,
                Verb: "block",
                SubjectOverride: "熊",
                AlwaysVisible: true,
                FromDialog: true,
                UsePopup: false);

            Assert.Multiple(() =>
            {
                Assert.That(DummyXDidYTarget.OriginalExecuted, Is.False);
                Assert.That(lastMessage, Is.EqualTo("\u0001あなたの熊は防いだ。"));
                Assert.That(lastUsePopup, Is.True);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_SuppressesInvisibleMessageBeforeEnglishAssembly()
    {
        WriteDictionary(tier1: new[] { ("block", "防いだ") });

        var hiddenSource = new DummyVisibilityTarget(isVisible: false);

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyXDidYTarget), nameof(DummyXDidYTarget.XDidY)),
                prefix: new HarmonyMethod(RequireMethod(typeof(XDidYTranslationPatch), nameof(XDidYTranslationPatch.PrefixXDidYForTests))));

            DummyXDidYTarget.XDidY(
                Actor: null,
                Verb: "block",
                SubjectOverride: "熊",
                Source: hiddenSource,
                AlwaysVisible: false);

            Assert.Multiple(() =>
            {
                Assert.That(DummyXDidYTarget.OriginalExecuted, Is.False);
                Assert.That(lastMessage, Is.Null);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesXDidYToZWithObjectTemplate()
    {
        WriteDictionary(tier2: new[] { ("stare", "at {0} menacingly", "{0}を睨みつけた") });

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyXDidYTarget), nameof(DummyXDidYTarget.XDidYToZ)),
                prefix: new HarmonyMethod(RequireMethod(typeof(XDidYTranslationPatch), nameof(XDidYTranslationPatch.PrefixXDidYToZForTests))));

            DummyXDidYTarget.XDidYToZ(
                Actor: null,
                Verb: "stare",
                Preposition: "at",
                Object: "タム",
                Extra: "menacingly",
                SubjectOverride: "熊",
                AlwaysVisible: true);

            Assert.Multiple(() =>
            {
                Assert.That(DummyXDidYTarget.OriginalExecuted, Is.False);
                Assert.That(lastMessage, Is.EqualTo("\u0001熊はタムを睨みつけた。"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesHeatSelfOnFreezeWarmFrame()
    {
        WriteDictionary(tier2: new[] { ("vibrate", "to warm {0}", "{0}を温めようと振動した") });

        RunWithXDidYToZPatch(() =>
        {
            DummyXDidYTarget.XDidYToZ(
                Actor: null,
                Verb: "vibrate",
                Preposition: "to warm",
                Object: "自身",
                EndMark: "!",
                SubjectOverride: "熊",
                AlwaysVisible: true);
        });

        Assert.Multiple(() =>
        {
            Assert.That(DummyXDidYTarget.OriginalExecuted, Is.False);
            Assert.That(lastMessage, Is.EqualTo("\u0001熊は自身を温めようと振動した！"));
        });
    }

    [Test]
    public void Prefix_TranslatesNephalPropertiesAbsorbChordsFrame()
    {
        WriteDictionary(tier2: new[] { ("absorb", "a chord of {0} light", "{0}の光の和音を吸収した") });

        RunWithXDidYToZPatch(() =>
        {
            DummyXDidYTarget.XDidYToZ(
                Actor: null,
                Verb: "absorb",
                Preposition: "a chord of",
                Object: "{{G|Saad Amus}}",
                Extra: "light",
                SubjectOverride: "{{R|Agolgot}}",
                AlwaysVisible: true);
        });

        Assert.Multiple(() =>
        {
            Assert.That(DummyXDidYTarget.OriginalExecuted, Is.False);
            Assert.That(lastMessage, Is.EqualTo("\u0001{{R|Agolgot}}は{{G|Saad Amus}}の光の和音を吸収した。"));
        });
    }

    [Test]
    public void Prefix_TranslatesBarathrumShuttleShipLaunchInterfaceFrame()
    {
        WriteDictionary(tier2: new[] { ("start", "interfacing with {0}", "{t0}とのインターフェースを開始した") });
        WriteUiDictionary(("Sheva starship control", "シェバ宇宙船制御装置"));

        RunWithXDidYToZPatch(() =>
        {
            DummyXDidYTarget.XDidYToZ(
                Actor: null,
                Verb: "start",
                Preposition: "interfacing with",
                Object: "{{Y|Sheva starship control}}",
                SubjectOverride: "{{C|Barathrumite shuttle}}",
                AlwaysVisible: true,
                UsePopup: true);
        });

        Assert.Multiple(() =>
        {
            Assert.That(DummyXDidYTarget.OriginalExecuted, Is.False);
            Assert.That(lastMessage, Is.EqualTo("\u0001{{C|Barathrumite shuttle}}は{{Y|シェバ宇宙船制御装置}}とのインターフェースを開始した。"));
            Assert.That(lastUsePopup, Is.True);
        });
    }

    [Test]
    public void Prefix_TranslatesPetFrondzieTauntFrames()
    {
        WriteDictionary(
            tier2: new[] { ("are", "enraged by the mockery", "嘲りに激怒した") },
            tier3: new[]
            {
                (
                    "yell",
                    "an? (?:terrible|ghastly|corny|shameful|rude|painful|monstrous|horrid|puerile|childish|boring|ridiculous) (?:pun|joke|double entendre|jape|goof|tall tale) about (.+?)",
                    "{subject}は{t0}についてひどい冗談を叫んだ{endmark}"),
            });
        WriteUiDictionary(("the Spindle", "スピンドル"));

        RunWithXDidYPatch(() =>
        {
            DummyXDidYTarget.XDidY(
                Actor: null,
                Verb: "yell",
                Extra: "a terrible pun about {{Y|the Spindle}}",
                SubjectOverride: "{{G|Frondzie}}",
                AlwaysVisible: true);

            Assert.Multiple(() =>
            {
                Assert.That(DummyXDidYTarget.OriginalExecuted, Is.False);
                Assert.That(lastMessage, Is.EqualTo("\u0001{{G|Frondzie}}は{{Y|スピンドル}}についてひどい冗談を叫んだ"));
            });

            DummyXDidYTarget.Reset();
            DummyXDidYTarget.XDidY(
                Actor: null,
                Verb: "are",
                Extra: "enraged by the mockery",
                EndMark: "!",
                SubjectOverride: "スナップジョー",
                AlwaysVisible: true);
        });

        Assert.Multiple(() =>
        {
            Assert.That(DummyXDidYTarget.OriginalExecuted, Is.False);
            Assert.That(lastMessage, Is.EqualTo("\u0001スナップジョーは嘲りに激怒した！"));
        });
    }

    [Test]
    public void Prefix_TranslatesPetEbenshabatRecipeTeachingFrame()
    {
        WriteDictionary(tier3: new[] { ("teach", "{0} {1}", "{subject}は{0}に{1}を教えた{endmark}") });

        RunWithXDidYToZPatch(() =>
        {
            DummyXDidYTarget.XDidYToZ(
                Actor: null,
                Verb: "teach",
                Object: "あなた",
                Extra: "{{y|ワタワイン粥}}",
                EndMark: "!",
                SubjectOverride: "{{G|Eben Shabbat}}",
                AlwaysVisible: true);
        });

        Assert.Multiple(() =>
        {
            Assert.That(DummyXDidYTarget.OriginalExecuted, Is.False);
            Assert.That(lastMessage, Is.EqualTo("\u0001{{G|Eben Shabbat}}はあなたに{{y|ワタワイン粥}}を教えた！"));
        });
    }

    [Test]
    public void Prefix_TranslatesSpaceTimeVortexPeriodicFrames()
    {
        WriteDictionary(
            tier1: new[] { ("destabilize", "不安定化した") },
            tier3: new[]
            {
                ("climb", "through {0} {1}", "{subject}は{t1}の{t0}を通って這い出てきた{endmark}"),
                ("fall", "through {0} {1}", "{subject}は{t1}の{t0}を通って落ちてきた{endmark}"),
            });
        WriteUiDictionary(("space-time vortex", "時空の渦"));

        RunWithXDidYToZPatch(() =>
        {
            DummyXDidYTarget.XDidYToZ(
                Actor: null,
                Verb: "climb",
                Preposition: "through",
                Object: "space-time vortex",
                Extra: "to the north",
                EndMark: "!",
                SubjectOverride: "スナップジョー",
                IndefiniteSubject: true,
                AlwaysVisible: true);

            Assert.Multiple(() =>
            {
                Assert.That(DummyXDidYTarget.OriginalExecuted, Is.False);
                Assert.That(lastMessage, Is.EqualTo("\u0001スナップジョーは北側の時空の渦を通って這い出てきた！"));
            });

            DummyXDidYTarget.Reset();
            DummyXDidYTarget.XDidYToZ(
                Actor: null,
                Verb: "fall",
                Preposition: "through",
                Object: "space-time vortex",
                Extra: "to the south",
                EndMark: "!",
                SubjectOverride: "奇妙な箱",
                IndefiniteSubject: true,
                AlwaysVisible: true);
        });

        Assert.Multiple(() =>
        {
            Assert.That(DummyXDidYTarget.OriginalExecuted, Is.False);
            Assert.That(lastMessage, Is.EqualTo("\u0001奇妙な箱は南側の時空の渦を通って落ちてきた！"));
        });

        DummyXDidYTarget.Reset();
        RunWithXDidYPatch(() =>
        {
            DummyXDidYTarget.XDidY(
                Actor: null,
                Verb: "destabilize",
                EndMark: "!",
                SubjectOverride: "時空の渦",
                AlwaysVisible: true);
        });

        Assert.Multiple(() =>
        {
            Assert.That(DummyXDidYTarget.OriginalExecuted, Is.False);
            Assert.That(lastMessage, Is.EqualTo("\u0001時空の渦は不安定化した！"));
        });
    }

    [Test]
    public void Prefix_TranslatesXDidYToZFlinchOutOfWayOfProjectile()
    {
        WriteDictionary(tier3: new[] { ("flinch", "out of the way of {0}", "{0}をかわした") });

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyXDidYTarget), nameof(DummyXDidYTarget.XDidYToZ)),
                prefix: new HarmonyMethod(RequireMethod(typeof(XDidYTranslationPatch), nameof(XDidYTranslationPatch.PrefixXDidYToZForTests))));

            DummyXDidYTarget.XDidYToZ(
                Actor: null,
                Verb: "flinch",
                Preposition: "out of the way of",
                Object: "木の矢",
                SubjectOverride: "ドリンクス",
                AlwaysVisible: true);

            Assert.Multiple(() =>
            {
                Assert.That(DummyXDidYTarget.OriginalExecuted, Is.False);
                Assert.That(lastMessage, Is.EqualTo("\u0001ドリンクスは木の矢をかわした。"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesXDidYToZWadeThroughLiquid()
    {
        WriteDictionary(tier3: new[] { ("wade", "through {0}", "{0}の中をかき分けて進んだ") });

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyXDidYTarget), nameof(DummyXDidYTarget.XDidYToZ)),
                prefix: new HarmonyMethod(RequireMethod(typeof(XDidYTranslationPatch), nameof(XDidYTranslationPatch.PrefixXDidYToZForTests))));

            DummyXDidYTarget.XDidYToZ(
                Actor: null,
                Verb: "wade",
                Preposition: "through",
                Object: "塩気のある水たまり",
                SubjectOverride: "あなた",
                AlwaysVisible: true);

            Assert.Multiple(() =>
            {
                Assert.That(DummyXDidYTarget.OriginalExecuted, Is.False);
                Assert.That(lastMessage, Is.EqualTo("\u0001あなたは塩気のある水たまりの中をかき分けて進んだ。"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesXDidYToZWadeThroughLiquid_StripsGeneratedObjectArticle()
    {
        WriteDictionary(tier3: new[] { ("wade", "through {0}", "{0}の中をかき分けて進んだ") });

        var liquid = new DummyCurrentDisplayNameTarget(
            "塩水の水たまり",
            indefiniteDisplayName: "a 塩水の水たまり");

        RunWithXDidYToZPatch(() =>
        {
            DummyXDidYTarget.XDidYToZ(
                Actor: null,
                Verb: "wade",
                Preposition: "through",
                Object: liquid,
                SubjectOverride: "あなた",
                IndefiniteObject: true,
                AlwaysVisible: true);

            Assert.Multiple(() =>
            {
                Assert.That(DummyXDidYTarget.OriginalExecuted, Is.False);
                Assert.That(lastMessage, Is.EqualTo("\u0001あなたは塩水の水たまりの中をかき分けて進んだ。"));
            });
        });
    }

    [Test]
    public void Prefix_TranslatesXDidYToZWadeThroughLiquid_DoesNotDuplicateLocalizedPuddleLiquid()
    {
        WriteDictionary(tier3: new[] { ("wade", "through {0}", "{0}の中をかき分けて進んだ") });
        WriteUiDictionary(("salty water", "塩水"));

        RunWithXDidYToZPatch(() =>
        {
            DummyXDidYTarget.XDidYToZ(
                Actor: null,
                Verb: "wade",
                Preposition: "through",
                Object: "塩水の水たまり of salty water",
                SubjectOverride: "あなた",
                AlwaysVisible: true);

            Assert.Multiple(() =>
            {
                Assert.That(DummyXDidYTarget.OriginalExecuted, Is.False);
                Assert.That(lastMessage, Is.EqualTo("\u0001あなたは塩水の水たまりの中をかき分けて進んだ。"));
            });
        });
    }

    [Test]
    public void Prefix_RepositoryFrames_TranslateLiquidVolumeContactFrames()
    {
        UseRepositoryMessageFrames();

        RunWithXDidYToZPatch(() =>
        {
            DummyXDidYTarget.XDidYToZ(
                Actor: null,
                Verb: "swim",
                Preposition: "through",
                Object: "{{B|深い池}}",
                SubjectOverride: "あなた",
                AlwaysVisible: true);

            Assert.Multiple(() =>
            {
                Assert.That(DummyXDidYTarget.OriginalExecuted, Is.False);
                Assert.That(lastMessage, Is.EqualTo("\u0001あなたは{{B|深い池}}の中を泳いだ。"));
            });
        });
    }

    [Test]
    public void Prefix_RepositoryFrames_TranslateLiquidVolumeCleaningFrames()
    {
        UseRepositoryDictionaries();
        UseRepositoryMessageFrames();

        RunWithXDidYPatch(() =>
        {
            DummyXDidYTarget.XDidY(
                Actor: null,
                Verb: "clean",
                Extra: "the mess from itself",
                SubjectOverride: "あなた",
                AlwaysVisible: true);

            Assert.Multiple(() =>
            {
                Assert.That(DummyXDidYTarget.OriginalExecuted, Is.False);
                Assert.That(lastMessage, Is.EqualTo("\u0001あなたはそれ自身の汚れを落とした。"));
            });

            DummyXDidYTarget.Reset();
            DummyXDidYTarget.XDidY(
                Actor: null,
                Verb: "clean",
                Extra: "the mess from your 鉄の剣 with a dram of {{B|fresh water}} from canteen to the north",
                EndMark: "!",
                SubjectOverride: "あなた",
                AlwaysVisible: true);
        });

        Assert.Multiple(() =>
        {
            Assert.That(DummyXDidYTarget.OriginalExecuted, Is.False);
            Assert.That(lastMessage, Is.EqualTo("\u0001あなたは水筒（北側）から{{B|真水}} 1ドラムを使ってあなたの鉄の剣の汚れを落とした！"));
        });
    }

    [Test]
    public void Prefix_RepositoryFrames_TranslateCampfireExtinguishFrame()
    {
        UseRepositoryMessageFrames();

        RunWithXDidYToZPatch(() =>
        {
            DummyXDidYTarget.XDidYToZ(
                Actor: null,
                Verb: "extinguish",
                Object: "キャンプファイヤー",
                SubjectOverride: "あなた",
                AlwaysVisible: true);

            Assert.Multiple(() =>
            {
                Assert.That(DummyXDidYTarget.OriginalExecuted, Is.False);
                Assert.That(lastMessage, Is.EqualTo("\u0001あなたはキャンプファイヤーを消した。"));
            });
        });
    }

    [Test]
    public void Prefix_RepositoryFrames_TranslateMagazineAmmoLoaderTransferFrame()
    {
        UseRepositoryMessageFrames();
        UseRepositoryDictionaries();

        RunWithXDidYToZPatch(() =>
        {
            DummyXDidYTarget.XDidYToZ(
                Actor: null,
                Verb: "transfer",
                Preposition: "3 lead slugs to",
                Object: "{{Y|turret}}",
                SubjectOverride: "あなた",
                AlwaysVisible: true);

            Assert.Multiple(() =>
            {
                Assert.That(DummyXDidYTarget.OriginalExecuted, Is.False);
                Assert.That(lastMessage, Is.EqualTo("\u0001あなたは3 lead slugsを{{Y|タレット}}に移した。"));
            });
        });
    }

    [Test]
    public void Prefix_TranslatesXDidYToZObjectDisplayNameBeforePrepositionSuffix()
    {
        File.WriteAllText(
            Path.Combine(tempDirectory, "ui-test.ja.json"),
            "{\"entries\":[{\"key\":\"Bedroll\",\"text\":\"寝袋\"}]}\n",
            Utf8WithoutBom);
        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        WriteDictionary(tier1: new[] { ("lie", "横になった"), ("rise", "起き上がった") });

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyXDidYTarget), nameof(DummyXDidYTarget.XDidYToZ)),
                prefix: new HarmonyMethod(RequireMethod(typeof(XDidYTranslationPatch), nameof(XDidYTranslationPatch.PrefixXDidYToZForTests))));

            DummyXDidYTarget.XDidYToZ(
                Actor: null,
                Verb: "lie",
                Preposition: "down on",
                Object: "Bedroll",
                SubjectOverride: "あなた",
                AlwaysVisible: true);

            Assert.That(lastMessage, Is.EqualTo("\u0001あなたは寝袋の上に横になった。"));

            DummyXDidYTarget.Reset();
            lastMessage = null;

            DummyXDidYTarget.XDidYToZ(
                Actor: null,
                Verb: "rise",
                Preposition: "from",
                Object: "Bedroll",
                SubjectOverride: "あなた",
                AlwaysVisible: true);

            Assert.That(lastMessage, Is.EqualTo("\u0001あなたは寝袋から起き上がった。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesShrinePrayerVerbAndGeneratedStatueObject()
    {
        WriteUiDictionary(
            ("desecrated", "冒涜された"),
            ("stone", "石"),
            ("statue", "像"));
        WriteDictionary(tier2: new[] { ("voice", "a short prayer beneath {0}", "{0}の下で短い祈りを唱えた") });

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyXDidYTarget), nameof(DummyXDidYTarget.XDidYToZ)),
                prefix: new HarmonyMethod(RequireMethod(typeof(XDidYTranslationPatch), nameof(XDidYTranslationPatch.PrefixXDidYToZForTests))));

            DummyXDidYTarget.XDidYToZ(
                Actor: null,
                Verb: "voice",
                Preposition: "a short prayer beneath",
                Object: "desecrated stone statue of a 山羊人の種播き",
                SubjectOverride: "あなた",
                AlwaysVisible: true);

            Assert.Multiple(() =>
            {
                Assert.That(DummyXDidYTarget.OriginalExecuted, Is.False);
                Assert.That(lastMessage, Is.EqualTo("\u0001あなたは冒涜された山羊人の種播きの石の像の下で短い祈りを唱えた。"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [TestCase("brass statue of a 山羊人の種播き", "\u0001あなたは山羊人の種播きの真鍮の像の下で短い祈りを唱えた。", false)]
    [TestCase("", null, true)]
    [TestCase("{{W|stone statue of a 山羊人の種播き}}", "\u0001あなたは{{W|山羊人の種播きの石の像}}の下で短い祈りを唱えた。", false)]
    [TestCase("\u0001stone statue of a 山羊人の種播き", "\u0001あなたは\u0001stone statue of a 山羊人の種播きの下で短い祈りを唱えた。", false)]
    public void Prefix_TranslatesShrinePrayerVerbAndGeneratedStatueObject_EdgeCases(
        string objectName,
        string? expectedMessage,
        bool expectedOriginalExecuted)
    {
        WriteUiDictionary(
            ("brass", "真鍮"),
            ("stone", "石"),
            ("statue", "像"));
        WriteDictionary(tier2: new[] { ("voice", "a short prayer beneath {0}", "{0}の下で短い祈りを唱えた") });

        RunWithXDidYToZPatch(() =>
        {
            DummyXDidYTarget.XDidYToZ(
                Actor: null,
                Verb: "voice",
                Preposition: "a short prayer beneath",
                Object: objectName,
                SubjectOverride: "あなた",
                AlwaysVisible: true);
        });

        Assert.Multiple(() =>
        {
            Assert.That(DummyXDidYTarget.OriginalExecuted, Is.EqualTo(expectedOriginalExecuted));
            Assert.That(lastMessage, Is.EqualTo(expectedMessage));
        });
    }

    [Test]
    public void Prefix_TranslatesWDidXToYWithZWithTemplate()
    {
        WriteDictionary(tier3: new[] { ("strike", "{0} with {1} for {2} damage", "{1}で{0}に{2}ダメージを与えた") });

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyXDidYTarget), nameof(DummyXDidYTarget.WDidXToYWithZ)),
                prefix: new HarmonyMethod(RequireMethod(typeof(XDidYTranslationPatch), nameof(XDidYTranslationPatch.PrefixWDidXToYWithZForTests))));

            DummyXDidYTarget.WDidXToYWithZ(
                Actor: null,
                Verb: "strike",
                DirectPreposition: null,
                DirectObject: "スナップジョー",
                IndirectPreposition: "with",
                IndirectObject: "青銅の短剣",
                Extra: "for 5 damage",
                SubjectOverride: "熊",
                AlwaysVisible: true,
                EndMark: "!");

            Assert.Multiple(() =>
            {
                Assert.That(DummyXDidYTarget.OriginalExecuted, Is.False);
                Assert.That(lastMessage, Is.EqualTo("\u0001熊は青銅の短剣でスナップジョーに5ダメージを与えた！"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesWDidXToYWithZ_StripsGeneratedObjectArticles()
    {
        WriteDictionary(tier3: new[] { ("strike", "{0} with {1} for {2} damage", "{1}で{0}に{2}ダメージを与えた") });

        var target = new DummyCurrentDisplayNameTarget(
            "スナップジョー",
            indefiniteDisplayName: "an スナップジョー");
        var weapon = new DummyCurrentDisplayNameTarget(
            "青銅の短剣",
            indefiniteDisplayName: "a 青銅の短剣");

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyXDidYTarget), nameof(DummyXDidYTarget.WDidXToYWithZ)),
                prefix: new HarmonyMethod(RequireMethod(typeof(XDidYTranslationPatch), nameof(XDidYTranslationPatch.PrefixWDidXToYWithZForTests))));

            DummyXDidYTarget.WDidXToYWithZ(
                Actor: null,
                Verb: "strike",
                DirectPreposition: null,
                DirectObject: target,
                IndirectPreposition: "with",
                IndirectObject: weapon,
                Extra: "for 5 damage",
                SubjectOverride: "熊",
                IndefiniteDirectObject: true,
                IndefiniteIndirectObject: true,
                AlwaysVisible: true,
                EndMark: "!");

            Assert.Multiple(() =>
            {
                Assert.That(DummyXDidYTarget.OriginalExecuted, Is.False);
                Assert.That(lastMessage, Is.EqualTo("\u0001熊は青銅の短剣でスナップジョーに5ダメージを与えた！"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_FallsBackToOriginalWhenVerbLookupIsMissing()
    {
        WriteDictionary(tier1: new[] { ("block", "防いだ") });

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyXDidYTarget), nameof(DummyXDidYTarget.XDidY)),
                prefix: new HarmonyMethod(RequireMethod(typeof(XDidYTranslationPatch), nameof(XDidYTranslationPatch.PrefixXDidYForTests))));

            DummyXDidYTarget.XDidY(
                Actor: null,
                Verb: "teleport",
                SubjectOverride: "熊",
                AlwaysVisible: true);

            Assert.Multiple(() =>
            {
                Assert.That(DummyXDidYTarget.OriginalExecuted, Is.True);
                Assert.That(lastMessage, Is.Null);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private void WriteDictionary(
        IEnumerable<(string verb, string text)>? tier1 = null,
        IEnumerable<(string verb, string extra, string text)>? tier2 = null,
        IEnumerable<(string verb, string extra, string text)>? tier3 = null)
    {
        var builder = new StringBuilder();
        builder.AppendLine("{");
        builder.AppendLine("  \"entries\": [],");
        builder.AppendLine("  \"tier1\": [");
        WriteTier1(builder, tier1);
        builder.AppendLine("  ],");
        builder.AppendLine("  \"tier2\": [");
        WriteTier2(builder, tier2);
        builder.AppendLine("  ],");
        builder.AppendLine("  \"tier3\": [");
        WriteTier2(builder, tier3);
        builder.AppendLine("  ]");
        builder.AppendLine("}");

        File.WriteAllText(dictionaryPath, builder.ToString(), Utf8WithoutBom);
    }

    private void WriteUiDictionary(params (string key, string text)[] entries)
    {
        var builder = new StringBuilder();
        builder.Append("{\"entries\":[");
        for (var index = 0; index < entries.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append("{\"key\":\"")
                .Append(EscapeJson(entries[index].key))
                .Append("\",\"text\":\"")
                .Append(EscapeJson(entries[index].text))
                .Append("\"}");
        }

        builder.AppendLine("]}");
        File.WriteAllText(Path.Combine(tempDirectory, "ui-test.ja.json"), builder.ToString(), Utf8WithoutBom);
        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
    }

    private static void UseRepositoryDictionaries()
    {
        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(
            Path.Combine(
                QudJP.Tests.L1.TestProjectPaths.GetRepositoryRoot(),
                "Mods",
                "QudJP",
                "Localization",
                "Dictionaries"));
    }

    private static void UseRepositoryMessageFrames()
    {
        MessageFrameTranslator.ResetForTests();
        MessageFrameTranslator.SetDictionaryPathForTests(
            Path.Combine(
                QudJP.Tests.L1.TestProjectPaths.GetRepositoryRoot(),
                "Mods",
                "QudJP",
                "Localization",
                "MessageFrames",
                "verbs.ja.json"));
    }

    private static void RunWithXDidYPatch(Action action)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyXDidYTarget), nameof(DummyXDidYTarget.XDidY)),
                prefix: new HarmonyMethod(RequireMethod(typeof(XDidYTranslationPatch), nameof(XDidYTranslationPatch.PrefixXDidYForTests))));
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void RunWithXDidYToZPatch(Action action)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyXDidYTarget), nameof(DummyXDidYTarget.XDidYToZ)),
                prefix: new HarmonyMethod(RequireMethod(typeof(XDidYTranslationPatch), nameof(XDidYTranslationPatch.PrefixXDidYToZForTests))));
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void WriteTier1(StringBuilder builder, IEnumerable<(string verb, string text)>? entries)
    {
        if (entries is null)
        {
            return;
        }

        var first = true;
        foreach (var entry in entries)
        {
            if (!first)
            {
                builder.AppendLine(",");
            }

            first = false;
            builder.Append("    { \"verb\": \"")
                .Append(EscapeJson(entry.verb))
                .Append("\", \"text\": \"")
                .Append(EscapeJson(entry.text))
                .Append("\" }");
        }

        if (!first)
        {
            builder.AppendLine();
        }
    }

    private static void WriteTier2(StringBuilder builder, IEnumerable<(string verb, string extra, string text)>? entries)
    {
        if (entries is null)
        {
            return;
        }

        var first = true;
        foreach (var entry in entries)
        {
            if (!first)
            {
                builder.AppendLine(",");
            }

            first = false;
            builder.Append("    { \"verb\": \"")
                .Append(EscapeJson(entry.verb))
                .Append("\", \"extra\": \"")
                .Append(EscapeJson(entry.extra))
                .Append("\", \"text\": \"")
                .Append(EscapeJson(entry.text))
                .Append("\" }");
        }

        if (!first)
        {
            builder.AppendLine();
        }
    }

    private static MethodInfo RequireMethod(Type type, string methodName)
    {
        return AccessTools.Method(type, methodName)
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private static string CreateHarmonyId()
    {
        return $"qudjp.tests.xdidy.{Guid.NewGuid():N}";
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);
    }

    private sealed class DummyCurrentDisplayNameTarget
    {
        private readonly string displayName;
        private readonly string? definiteDisplayName;
        private readonly string? indefiniteDisplayName;
        private readonly string toStringText;

        public DummyCurrentDisplayNameTarget(
            string displayName,
            string? displayNameMember = null,
            string toStringText = "Object",
            string? definiteDisplayName = null,
            string? indefiniteDisplayName = null)
        {
            this.displayName = displayName;
            this.definiteDisplayName = definiteDisplayName;
            this.indefiniteDisplayName = indefiniteDisplayName;
            DisplayName = displayNameMember ?? displayName;
            this.toStringText = toStringText;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Major Code Smell",
            "S1144:Unused private types or members",
            Justification = "Read by the reflected One signature used in the test.")]
        public string DisplayName { get; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Major Code Smell",
            "S1144:Unused private types or members",
            Justification = "Invoked by XDidYTranslationPatch through reflection.")]
        public string One(
            int Cutoff = int.MaxValue,
            string? Base = null,
            string? Context = null,
            bool AsIfKnown = false,
            bool Single = false,
            bool NoConfusion = false,
            bool NoColor = false,
            bool Stripped = false,
            bool WithoutTitles = true,
            bool Short = true,
            bool BaseOnly = false,
            bool WithIndefiniteArticle = false,
            string? DefaultDefiniteArticle = null,
            bool IndicateHidden = true,
            bool SecondPerson = true,
            bool Reflexive = false,
            bool? IncludeAdjunctNoun = null,
            bool AsPossessed = false,
            object? AsPossessedBy = null,
            bool Reference = false)
        {
            return ResolveDisplayName(WithIndefiniteArticle);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Major Code Smell",
            "S1144:Unused private types or members",
            Justification = "Invoked by XDidYTranslationPatch through reflection.")]
        public string one(
            int Cutoff = int.MaxValue,
            string? Base = null,
            string? Context = null,
            bool AsIfKnown = false,
            bool Single = false,
            bool NoConfusion = false,
            bool NoColor = false,
            bool Stripped = false,
            bool WithoutTitles = true,
            bool Short = true,
            bool BaseOnly = false,
            bool WithIndefiniteArticle = false,
            string? DefaultDefiniteArticle = null,
            bool IndicateHidden = true,
            bool SecondPerson = true,
            bool Reflexive = false,
            bool? IncludeAdjunctNoun = null,
            bool AsPossessed = false,
            object? AsPossessedBy = null,
            bool Reference = false)
        {
            return ResolveDisplayName(WithIndefiniteArticle);
        }

        private string ResolveDisplayName(bool withIndefiniteArticle)
        {
            if (withIndefiniteArticle && indefiniteDisplayName is not null)
            {
                return indefiniteDisplayName;
            }

            if (!withIndefiniteArticle && definiteDisplayName is not null)
            {
                return definiteDisplayName;
            }

            return displayName;
        }

        public override string ToString()
        {
            return toStringText;
        }
    }

    private sealed class DummyDisplayNameFallbackOnlyTarget
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Major Code Smell",
            "S1144:Unused private types or members",
            Justification = "Verifies XDidYTranslationPatch does not use DisplayName reflection fallback.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Major Code Smell",
            "S2325:Methods and properties that don't access instance data should be static",
            Justification = "Reflection fallback probe must be an instance member.")]
        public string DisplayName => "FallbackDisplayName";

        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Major Code Smell",
            "S1144:Unused private types or members",
            Justification = "Invoked by XDidYTranslationPatch through reflection.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Major Code Smell",
            "S2325:Methods and properties that don't access instance data should be static",
            Justification = "Reflection visibility probe must be an instance member.")]
        public bool IsVisible()
        {
            return true;
        }

        public override string ToString()
        {
            return "FallbackToString";
        }
    }
}
