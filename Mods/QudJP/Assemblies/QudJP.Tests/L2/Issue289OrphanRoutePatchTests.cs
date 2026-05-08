using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class Issue289OrphanRoutePatchTests
{
    private string tempDirectory = null!;

    public enum TutorialRouteKind
    {
        CellPopup,
        HighlightByCid,
        DirectHighlight,
    }

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-issue289-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        DynamicTextObservability.ResetForTests();
        DummyMessageLogLineTarget.ResetMenuOptions();
        DummyTutorialManagerTarget.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        DummyMessageLogLineTarget.ResetMenuOptions();
        DummyTutorialManagerTarget.Reset();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void HelpScreenPostfix_TranslatesMenuOptionsAndRenderedHotkeyBar_WhenPatched()
    {
        WriteDictionary(
            ("navigate", "移動"),
            ("Toggle Visibility", "表示を切り替え"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyHelpScreenTarget), nameof(DummyHelpScreenTarget.UpdateMenuBars)),
                postfix: new HarmonyMethod(RequireMethod(typeof(HelpScreenTranslationPatch), nameof(HelpScreenTranslationPatch.Postfix))));

            var target = new DummyHelpScreenTarget();
            target.UpdateMenuBars();

            Assert.Multiple(() =>
            {
                Assert.That(target.keyMenuOptions[0].Description, Is.EqualTo("移動"));
                Assert.That(target.keyMenuOptions[1].Description, Is.EqualTo("表示を切り替え"));
                Assert.That(target.hotkeyBar.choices[0].Description, Is.EqualTo("移動"));
                Assert.That(target.hotkeyBar.choices[1].Description, Is.EqualTo("表示を切り替え"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(HelpScreenTranslationPatch), "HelpScreen.MenuOption"),
                    Is.EqualTo(2));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void MessageLogStatusScreenTabPostfix_TranslatesLongAndShortLabels_WhenPatched()
    {
        WriteDictionary(
            ("Message Log", "メッセージログ"),
            ("Log", "ログ"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyMessageLogStatusScreenTarget), nameof(DummyMessageLogStatusScreenTarget.GetTabString)),
                postfix: new HarmonyMethod(RequireMethod(typeof(MessageLogStatusScreenTranslationPatch), nameof(MessageLogStatusScreenTranslationPatch.Postfix))));

            var longTarget = new DummyMessageLogStatusScreenTarget();
            var shortTarget = new DummyMessageLogStatusScreenTarget { CompactMode = true };

            Assert.Multiple(() =>
            {
                Assert.That(longTarget.GetTabString(), Is.EqualTo("メッセージログ"));
                Assert.That(shortTarget.GetTabString(), Is.EqualTo("ログ"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(MessageLogStatusScreenTranslationPatch), "MessageLogStatusScreen.TabString"),
                    Is.EqualTo(2));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void MessageLogLinePostfix_TranslatesExpandCollapseOptionsWithoutTouchingMessageText_WhenPatched()
    {
        WriteDictionary(
            ("Expand", "展開"),
            ("Collapse", "折りたたむ"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyMessageLogLineTarget), nameof(DummyMessageLogLineTarget.setData)),
                postfix: new HarmonyMethod(RequireMethod(typeof(MessageLogLineTranslationPatch), nameof(MessageLogLineTranslationPatch.Postfix))));

            var target = new DummyMessageLogLineTarget();
            target.setData(new DummyMessageLogLineDataTarget { text = "You hit snapjaw for 7 damage." });

            Assert.Multiple(() =>
            {
                Assert.That(target.text.Text, Is.EqualTo("You hit snapjaw for 7 damage."));
                Assert.That(DummyMessageLogLineTarget.categoryExpandOptions[0].Description, Is.EqualTo("展開"));
                Assert.That(DummyMessageLogLineTarget.categoryCollapseOptions[0].Description, Is.EqualTo("折りたたむ"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(MessageLogLineTranslationPatch), "MessageLogLine.MenuOption"),
                    Is.EqualTo(2));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void TutorialManagerPrefix_TranslatesPopupTextAndContinueButton_WhenPatched()
    {
        WriteDictionary(
            ("Use ~Accept to continue.", "~Accept で続行せよ。"),
            ("[~Accept] Continue", "[~Accept] 続行"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(
                    typeof(DummyTutorialManagerTarget),
                    nameof(DummyTutorialManagerTarget.ShowCIDPopupAsync),
                    new[]
                    {
                        typeof(string),
                        typeof(string),
                        typeof(string),
                        typeof(string),
                        typeof(int),
                        typeof(int),
                        typeof(float),
                        typeof(Action),
                    }),
                prefix: new HarmonyMethod(RequireMethod(typeof(TutorialManagerTranslationPatch), nameof(TutorialManagerTranslationPatch.Prefix))));

            DummyTutorialManagerTarget.ShowCIDPopupAsync(
                    "RootCanvas",
                    "Use ~Accept to continue.",
                    "s",
                    "[~Accept] Continue")
                .GetAwaiter()
                .GetResult();

            Assert.Multiple(() =>
            {
                Assert.That(DummyTutorialManagerTarget.LastPopupText, Is.EqualTo("~Accept で続行せよ。"));
                Assert.That(DummyTutorialManagerTarget.LastButtonText, Is.EqualTo("[~Accept] 続行"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(TutorialManagerTranslationPatch), "TutorialManager.PopupText"),
                    Is.EqualTo(1));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(TutorialManagerTranslationPatch), "TutorialManager.ButtonText"),
                    Is.EqualTo(1));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void TutorialManagerCellPopupPrefix_TranslatesPopupText_WhenPatched()
    {
        WriteDictionary(("Look around the cave.", "洞窟を見回せ。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(
                    typeof(DummyTutorialManagerTarget),
                    nameof(DummyTutorialManagerTarget.ShowCellPopup),
                    new[]
                    {
                        typeof(Genkit.Location2D),
                        typeof(string),
                        typeof(string),
                        typeof(int),
                        typeof(int),
                        typeof(Action),
                    }),
                prefix: new HarmonyMethod(RequireMethod(typeof(TutorialManagerCellPopupTranslationPatch), nameof(TutorialManagerCellPopupTranslationPatch.Prefix))));

            DummyTutorialManagerTarget.ShowCellPopup(
                    default!,
                    "Look around the cave.",
                    "ne")
                .GetAwaiter()
                .GetResult();

            Assert.Multiple(() =>
            {
                Assert.That(DummyTutorialManagerTarget.LastCellPopupText, Is.EqualTo("洞窟を見回せ。"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(TutorialManagerCellPopupTranslationPatch), "TutorialManager.CellPopupText"),
                    Is.EqualTo(1));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void TutorialManagerHighlightPrefix_TranslatesRawHighlightTextBeforeColorWrapping_WhenPatched()
    {
        WriteDictionary(("Inspect the snapjaw.", "スナップジョーを調べろ。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(
                    typeof(DummyTutorialManagerInstanceTarget),
                    nameof(DummyTutorialManagerInstanceTarget.HighlightByCID),
                    new[]
                    {
                        typeof(string),
                        typeof(string),
                        typeof(string),
                        typeof(int),
                        typeof(int),
                        typeof(float),
                        typeof(string),
                    }),
                prefix: new HarmonyMethod(RequireMethod(typeof(TutorialManagerHighlightTranslationPatch), nameof(TutorialManagerHighlightTranslationPatch.Prefix))));

            var target = new DummyTutorialManagerInstanceTarget();
            target.HighlightByCID(
                "QudTextMenuItem:look",
                "Inspect the snapjaw.",
                "ne");

            Assert.Multiple(() =>
            {
                Assert.That(target.LastHighlightText, Is.EqualTo("{{y|スナップジョーを調べろ。}}"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(TutorialManagerHighlightTranslationPatch), "TutorialManager.HighlightText"),
                    Is.EqualTo(1));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void TutorialManagerCellHighlightPrefix_TranslatesRawCellHighlightText_WhenPatched()
    {
        WriteDictionary(("Open the chest.", "チェストを開いてください。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(
                    typeof(DummyTutorialManagerInstanceTarget),
                    nameof(DummyTutorialManagerInstanceTarget.HighlightCell),
                    new[]
                    {
                        typeof(int),
                        typeof(int),
                        typeof(string),
                        typeof(string),
                        typeof(float),
                        typeof(float),
                        typeof(float),
                    }),
                prefix: new HarmonyMethod(RequireMethod(typeof(TutorialManagerCellHighlightTranslationPatch), nameof(TutorialManagerCellHighlightTranslationPatch.Prefix))));

            var target = new DummyTutorialManagerInstanceTarget();
            target.HighlightCell(16, 12, "Open the chest.", "ne");

            Assert.Multiple(() =>
            {
                Assert.That(target.LastCellHighlightText, Is.EqualTo("{{y|チェストを開いてください。}}"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(TutorialManagerCellHighlightTranslationPatch), "TutorialManager.CellHighlightText"),
                    Is.EqualTo(1));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void TutorialManagerCellHighlightPrefix_TranslatesExpandedHotkeyCellHighlightText_WhenPatched()
    {
        WriteDictionary((
            "You can interact with objects you're next to. Open the chest.\n\nPress Space or ",
            "隣接した物体には干渉できます。チェストを開いてください。\n\nSpace または  を押してください。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(
                    typeof(DummyTutorialManagerInstanceTarget),
                    nameof(DummyTutorialManagerInstanceTarget.HighlightCell),
                    new[]
                    {
                        typeof(int),
                        typeof(int),
                        typeof(string),
                        typeof(string),
                        typeof(float),
                        typeof(float),
                        typeof(float),
                    }),
                prefix: new HarmonyMethod(RequireMethod(typeof(TutorialManagerCellHighlightTranslationPatch), nameof(TutorialManagerCellHighlightTranslationPatch.Prefix))));

            var target = new DummyTutorialManagerInstanceTarget();
            target.HighlightCell(
                16,
                12,
                "You can interact with objects you're next to. Open the chest.\n\nPress {{hotkey|{{hotkey|Space}}}} or {{hotkey|{{hotkey|}}}}",
                "ne");

            Assert.That(
                target.LastCellHighlightText,
                Is.EqualTo("{{y|隣接した物体には干渉できます。チェストを開いてください。\n\nSpace または  を押してください。}}"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void TutorialManagerCellHighlightPrefix_TranslatesRawMarkupDictionaryKeyBeforeVisibleFallback_WhenPatched()
    {
        WriteDictionary((
            "You can attack a hostile creature by moving into its square. This is called {{W|bump attacking}}.",
            "敵対的な生き物のマスへ移動すると攻撃できます。これは{{W|体当たり攻撃}}と呼ばれます。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(
                    typeof(DummyTutorialManagerInstanceTarget),
                    nameof(DummyTutorialManagerInstanceTarget.HighlightCell),
                    new[]
                    {
                        typeof(int),
                        typeof(int),
                        typeof(string),
                        typeof(string),
                        typeof(float),
                        typeof(float),
                        typeof(float),
                    }),
                prefix: new HarmonyMethod(RequireMethod(typeof(TutorialManagerCellHighlightTranslationPatch), nameof(TutorialManagerCellHighlightTranslationPatch.Prefix))));

            var target = new DummyTutorialManagerInstanceTarget();
            target.HighlightCell(
                16,
                12,
                "You can attack a hostile creature by moving into its square. This is called {{W|bump attacking}}.",
                "ne");

            Assert.Multiple(() =>
            {
                Assert.That(
                    target.LastCellHighlightText,
                    Is.EqualTo("{{y|敵対的な生き物のマスへ移動すると攻撃できます。これは{{W|体当たり攻撃}}と呼ばれます。}}"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(TutorialManagerCellHighlightTranslationPatch), "TutorialManager.CellHighlightText"),
                    Is.EqualTo(1));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void TutorialManagerHighlightPrefix_LeavesNoMessageSentinelUntranslated_WhenPatched()
    {
        WriteDictionary(("<no message>", "翻訳してはいけない"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(
                    typeof(DummyTutorialManagerInstanceTarget),
                    nameof(DummyTutorialManagerInstanceTarget.HighlightByCID),
                    new[]
                    {
                        typeof(string),
                        typeof(string),
                        typeof(string),
                        typeof(int),
                        typeof(int),
                        typeof(float),
                        typeof(string),
                    }),
                prefix: new HarmonyMethod(RequireMethod(typeof(TutorialManagerHighlightTranslationPatch), nameof(TutorialManagerHighlightTranslationPatch.Prefix))));

            var target = new DummyTutorialManagerInstanceTarget();
            target.HighlightByCID(
                "QudTextMenuItem:cancel",
                "<no message>",
                "ne");

            Assert.Multiple(() =>
            {
                Assert.That(target.LastHighlightText, Is.EqualTo("{{y|<no message>}}"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(TutorialManagerHighlightTranslationPatch), "TutorialManager.HighlightText"),
                    Is.Zero);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [TestCase("<noframe>")]
    [TestCase("<nohighlight>")]
    public void TutorialManagerHighlightPrefix_LeavesControlSentinelsUntranslated_WhenPatched(string sentinel)
    {
        WriteDictionary((sentinel, "翻訳してはいけない"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(
                    typeof(DummyTutorialManagerInstanceTarget),
                    nameof(DummyTutorialManagerInstanceTarget.HighlightByCID),
                    new[]
                    {
                        typeof(string),
                        typeof(string),
                        typeof(string),
                        typeof(int),
                        typeof(int),
                        typeof(float),
                        typeof(string),
                    }),
                prefix: new HarmonyMethod(RequireMethod(typeof(TutorialManagerHighlightTranslationPatch), nameof(TutorialManagerHighlightTranslationPatch.Prefix))));

            var target = new DummyTutorialManagerInstanceTarget();
            target.HighlightByCID(
                "QudTextMenuItem:cancel",
                sentinel,
                "ne");

            Assert.Multiple(() =>
            {
                Assert.That(target.LastHighlightText, Is.EqualTo("{{y|" + sentinel + "}}"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(TutorialManagerHighlightTranslationPatch), "TutorialManager.HighlightText"),
                    Is.Zero);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void TutorialManagerDirectHighlightPrefix_TranslatesRawHighlightText_WhenPatched()
    {
        WriteDictionary(("You can name your character or choose Next for a random name.", "名前を付けるか、次へでランダムな名前を選べます。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(
                    typeof(DummyTutorialManagerInstanceTarget),
                    nameof(DummyTutorialManagerInstanceTarget.Highlight),
                    new[]
                    {
                        typeof(IDummyRectTransform),
                        typeof(string),
                        typeof(string),
                        typeof(float),
                        typeof(float),
                        typeof(float),
                        typeof(string),
                    }),
                prefix: new HarmonyMethod(RequireMethod(typeof(TutorialManagerDirectHighlightTranslationPatch), nameof(TutorialManagerDirectHighlightTranslationPatch.Prefix))));

            var target = new DummyTutorialManagerInstanceTarget();
            target.Highlight(
                null,
                "You can name your character or choose Next for a random name.",
                "se");

            Assert.Multiple(() =>
            {
                Assert.That(target.LastDirectHighlightText, Is.EqualTo("{{y|名前を付けるか、次へでランダムな名前を選べます。}}"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(TutorialManagerDirectHighlightTranslationPatch), "TutorialManager.DirectHighlightText"),
                    Is.EqualTo(1));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void TutorialManagerDirectHighlightPrefix_LeavesNoFrameSentinelUntranslated_WhenPatched()
    {
        WriteDictionary(("<noframe>", "翻訳してはいけない"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(
                    typeof(DummyTutorialManagerInstanceTarget),
                    nameof(DummyTutorialManagerInstanceTarget.Highlight),
                    new[]
                    {
                        typeof(IDummyRectTransform),
                        typeof(string),
                        typeof(string),
                        typeof(float),
                        typeof(float),
                        typeof(float),
                        typeof(string),
                    }),
                prefix: new HarmonyMethod(RequireMethod(typeof(TutorialManagerDirectHighlightTranslationPatch), nameof(TutorialManagerDirectHighlightTranslationPatch.Prefix))));

            var target = new DummyTutorialManagerInstanceTarget();
            target.Highlight(null, "<noframe>", "se");

            Assert.Multiple(() =>
            {
                Assert.That(target.LastDirectHighlightText, Is.EqualTo("{{y|<noframe>}}"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(TutorialManagerDirectHighlightTranslationPatch), "TutorialManager.DirectHighlightText"),
                    Is.Zero);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [TestCase(TutorialRouteKind.CellPopup)]
    [TestCase(TutorialRouteKind.HighlightByCid)]
    [TestCase(TutorialRouteKind.DirectHighlight)]
    public void TutorialManagerRoutePrefixes_LeaveMissingDictionaryTextUnchanged_WhenPatched(TutorialRouteKind route)
    {
        WriteDictionary(("Look around the cave.", "洞窟を見回せ。"));

        const string source = "Unmapped tutorial text.";

        var result = RunTutorialRouteWithPatch(route, source);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(ExpectedTutorialRouteOutput(route, source)));
            Assert.That(GetTutorialRouteHitCount(route), Is.Zero);
        });
    }

    [TestCase(TutorialRouteKind.CellPopup)]
    [TestCase(TutorialRouteKind.HighlightByCid)]
    [TestCase(TutorialRouteKind.DirectHighlight)]
    public void TutorialManagerRoutePrefixes_LeaveDirectTranslationMarkerUnchanged_WhenPatched(TutorialRouteKind route)
    {
        WriteDictionary(("Look around the cave.", "洞窟を見回せ。"));

        var source = MessageFrameTranslator.MarkDirectTranslation("Look around the cave.");

        var result = RunTutorialRouteWithPatch(route, source);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(ExpectedTutorialRouteOutput(route, source)));
            Assert.That(GetTutorialRouteHitCount(route), Is.Zero);
        });
    }

    [TestCase(TutorialRouteKind.CellPopup)]
    [TestCase(TutorialRouteKind.HighlightByCid)]
    [TestCase(TutorialRouteKind.DirectHighlight)]
    public void TutorialManagerRoutePrefixes_LeaveEmptyInputUnchanged_WhenPatched(TutorialRouteKind route)
    {
        WriteDictionary(("Look around the cave.", "洞窟を見回せ。"));

        var result = RunTutorialRouteWithPatch(route, string.Empty);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(ExpectedTutorialRouteOutput(route, string.Empty)));
            Assert.That(GetTutorialRouteHitCount(route), Is.Zero);
        });
    }

    [TestCase(TutorialRouteKind.CellPopup)]
    [TestCase(TutorialRouteKind.HighlightByCid)]
    [TestCase(TutorialRouteKind.DirectHighlight)]
    public void TutorialManagerRoutePrefixes_PreserveColorTags_WhenPatched(TutorialRouteKind route)
    {
        WriteDictionary(("Look around the cave.", "洞窟を見回せ。"));

        const string source = "{{W|Look around the cave.}}";
        const string translated = "{{W|洞窟を見回せ。}}";

        var result = RunTutorialRouteWithPatch(route, source);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(ExpectedTutorialRouteOutput(route, translated)));
            Assert.That(GetTutorialRouteHitCount(route), Is.EqualTo(1));
        });
    }

    private static string CreateHarmonyId()
    {
        return $"qudjp.tests.{Guid.NewGuid():N}";
    }

    private static int GetTutorialRouteHitCount(TutorialRouteKind route)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            route switch
            {
                TutorialRouteKind.CellPopup => nameof(TutorialManagerCellPopupTranslationPatch),
                TutorialRouteKind.HighlightByCid => nameof(TutorialManagerHighlightTranslationPatch),
                TutorialRouteKind.DirectHighlight => nameof(TutorialManagerDirectHighlightTranslationPatch),
                _ => throw new ArgumentOutOfRangeException(nameof(route), route, null),
            },
            route switch
            {
                TutorialRouteKind.CellPopup => "TutorialManager.CellPopupText",
                TutorialRouteKind.HighlightByCid => "TutorialManager.HighlightText",
                TutorialRouteKind.DirectHighlight => "TutorialManager.DirectHighlightText",
                _ => throw new ArgumentOutOfRangeException(nameof(route), route, null),
            });
    }

    private static string ExpectedTutorialRouteOutput(TutorialRouteKind route, string text)
    {
        return route == TutorialRouteKind.CellPopup
            ? text
            : "{{y|" + text + "}}";
    }

    private static string RunTutorialRouteWithPatch(TutorialRouteKind route, string text)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchTutorialRoute(harmony, route);

            switch (route)
            {
                case TutorialRouteKind.CellPopup:
                    DummyTutorialManagerTarget.ShowCellPopup(default!, text, "ne").GetAwaiter().GetResult();
                    return DummyTutorialManagerTarget.LastCellPopupText;

                case TutorialRouteKind.HighlightByCid:
                {
                    var target = new DummyTutorialManagerInstanceTarget();
                    target.HighlightByCID("QudTextMenuItem:look", text, "ne");
                    return target.LastHighlightText;
                }

                case TutorialRouteKind.DirectHighlight:
                {
                    var target = new DummyTutorialManagerInstanceTarget();
                    target.Highlight(null, text, "se");
                    return target.LastDirectHighlightText;
                }

                default:
                    throw new ArgumentOutOfRangeException(nameof(route), route, null);
            }
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void PatchTutorialRoute(Harmony harmony, TutorialRouteKind route)
    {
        switch (route)
        {
            case TutorialRouteKind.CellPopup:
                harmony.Patch(
                    original: RequireMethod(
                        typeof(DummyTutorialManagerTarget),
                        nameof(DummyTutorialManagerTarget.ShowCellPopup),
                        new[]
                        {
                            typeof(Genkit.Location2D),
                            typeof(string),
                            typeof(string),
                            typeof(int),
                            typeof(int),
                            typeof(Action),
                        }),
                    prefix: new HarmonyMethod(RequireMethod(typeof(TutorialManagerCellPopupTranslationPatch), nameof(TutorialManagerCellPopupTranslationPatch.Prefix))));
                break;

            case TutorialRouteKind.HighlightByCid:
                harmony.Patch(
                    original: RequireMethod(
                        typeof(DummyTutorialManagerInstanceTarget),
                        nameof(DummyTutorialManagerInstanceTarget.HighlightByCID),
                        new[]
                        {
                            typeof(string),
                            typeof(string),
                            typeof(string),
                            typeof(int),
                            typeof(int),
                            typeof(float),
                            typeof(string),
                        }),
                    prefix: new HarmonyMethod(RequireMethod(typeof(TutorialManagerHighlightTranslationPatch), nameof(TutorialManagerHighlightTranslationPatch.Prefix))));
                break;

            case TutorialRouteKind.DirectHighlight:
                harmony.Patch(
                    original: RequireMethod(
                        typeof(DummyTutorialManagerInstanceTarget),
                        nameof(DummyTutorialManagerInstanceTarget.Highlight),
                        new[]
                        {
                            typeof(IDummyRectTransform),
                            typeof(string),
                            typeof(string),
                            typeof(float),
                            typeof(float),
                            typeof(float),
                            typeof(string),
                        }),
                    prefix: new HarmonyMethod(RequireMethod(typeof(TutorialManagerDirectHighlightTranslationPatch), nameof(TutorialManagerDirectHighlightTranslationPatch.Prefix))));
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(route), route, null);
        }
    }

    private static MethodInfo RequireMethod(Type type, string methodName, Type[]? parameterTypes = null)
    {
        var method = parameterTypes is null
            ? AccessTools.Method(type, methodName)
            : AccessTools.Method(type, methodName, parameterTypes);
        return method
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
            Path.Combine(tempDirectory, "issue289.ja.json"),
            builder.ToString(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
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
}
