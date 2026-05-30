using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class PopupMessageTranslationPatchTests
{
    private string tempDirectory = null!;
    private string dictionaryDirectory = null!;
    private string patternFilePath = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-popup-message-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        dictionaryDirectory = Path.Combine(tempDirectory, "dict");
        Directory.CreateDirectory(dictionaryDirectory);
        patternFilePath = Path.Combine(tempDirectory, "messages.ja.json");

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(dictionaryDirectory);
        MessagePatternTranslator.ResetForTests();
        MessagePatternTranslator.SetPatternFileForTests(patternFilePath);
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
        PopupTranslatedMessageHandoff.ResetForTests();
        File.WriteAllText(patternFilePath, "{\"patterns\":[]}\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        DummyPopupMessageTarget.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        MessagePatternTranslator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
        PopupTranslatedMessageHandoff.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void Prefix_TranslatesPopupContent_WhenPatched()
    {
        WriteDictionary(
            ("Are you sure you want to delete the save game for {0}?", "{0}のセーブデータを本当に削除しますか？"),
            ("Delete {0}", "{0}を削除"),
            ("Save Slots", "セーブ一覧"),
            ("Continue", "続ける"),
            ("[Enter] Accept", "[Enter] 承認"),
            ("[Esc] Cancel", "[Esc] キャンセル"));

        var buttons = new List<DummyPopupMessageItem>
        {
            new("{{W|[Enter]}} {{y|Accept}}", "Accept", "Accept"),
            new("{{W|[Esc]}} {{y|Cancel}}", "Cancel", "Cancel"),
        };
        var items = new List<DummyPopupMessageItem>
        {
            new("Continue", "Space", "Continue"),
        };

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupMessageTarget), nameof(DummyPopupMessageTarget.ShowPopup)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupMessageTranslationPatch), nameof(PopupMessageTranslationPatch.Prefix))));

            new DummyPopupMessageTarget().ShowPopup(
                "Are you sure you want to delete the save game for Yashur?",
                buttons,
                commandCallback: null,
                items: items,
                title: "Delete Yashur",
                contextTitle: "Save Slots",
                WantsSpecificPrompt: "ABANDON");

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupMessageTarget.LastMessage, Is.EqualTo("Yashurのセーブデータを本当に削除しますか？"));
                Assert.That(DummyPopupMessageTarget.LastTitle, Is.EqualTo("Yashurを削除"));
                Assert.That(DummyPopupMessageTarget.LastContextTitle, Is.EqualTo("セーブ一覧"));
                Assert.That(DummyPopupMessageTarget.LastButtons, Is.Not.Null);
                Assert.That(DummyPopupMessageTarget.LastButtons![0].text, Is.EqualTo("{{W|[Enter]}} {{y|承認}}"));
                Assert.That(DummyPopupMessageTarget.LastButtons[0].hotkey, Is.EqualTo("Accept"));
                Assert.That(DummyPopupMessageTarget.LastButtons[0].command, Is.EqualTo("Accept"));
                Assert.That(DummyPopupMessageTarget.LastButtons[1].text, Is.EqualTo("{{W|[Esc]}} {{y|キャンセル}}"));
                Assert.That(DummyPopupMessageTarget.LastItems, Is.Not.Null);
                Assert.That(DummyPopupMessageTarget.LastItems![0].text, Is.EqualTo("続ける"));
                Assert.That(DummyPopupMessageTarget.LastWantsSpecificPrompt, Is.EqualTo("ABANDON"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_PreservesColorCodes_WhenDeleteTemplatesReorderPlaceholder()
    {
        WriteDictionary(
            ("Are you sure you want to delete the save game for {0}?", "{0}のセーブデータを本当に削除しますか？"),
            ("Delete {0}", "{0}を削除"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupMessageTarget), nameof(DummyPopupMessageTarget.ShowPopup)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupMessageTranslationPatch), nameof(PopupMessageTranslationPatch.Prefix))));

            new DummyPopupMessageTarget().ShowPopup(
                "Are you sure you want to delete the save game for {{W|Yashur}}?",
                title: "Delete {{W|Yashur}}");

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupMessageTarget.LastMessage, Is.EqualTo("{{W|Yashur}}のセーブデータを本当に削除しますか？"));
                Assert.That(DummyPopupMessageTarget.LastTitle, Is.EqualTo("{{W|Yashur}}を削除"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_IsIdempotentForSharedButtonLists()
    {
        WriteDictionary(("[Esc] Cancel", "[Esc] キャンセル"));

        var sharedButtons = new List<DummyPopupMessageItem>
        {
            new("{{W|[Esc]}} {{y|Cancel}}", "Cancel", "Cancel"),
        };

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupMessageTarget), nameof(DummyPopupMessageTarget.ShowPopup)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupMessageTranslationPatch), nameof(PopupMessageTranslationPatch.Prefix))));

            var target = new DummyPopupMessageTarget();
            target.ShowPopup("Prompt", sharedButtons);
            target.ShowPopup("Prompt", sharedButtons);

            Assert.That(sharedButtons[0].text, Is.EqualTo("{{W|[Esc]}} {{y|キャンセル}}"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_FallsBackToEnglish_WhenKeyNotInDictionary()
    {
        WriteDictionary(("Cancel", "キャンセル"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupMessageTarget), nameof(DummyPopupMessageTarget.ShowPopup)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupMessageTranslationPatch), nameof(PopupMessageTranslationPatch.Prefix))));

            var target = new DummyPopupMessageTarget();
            target.ShowPopup("Unknown English Text");

            Assert.That(DummyPopupMessageTarget.LastMessage, Is.EqualTo("Unknown English Text"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_UsesPopupShowOwnerHandoff_WhenNewPopupQueueReceivesMarkupTransformedOriginal()
    {
        WriteDictionary(
            ("mutation:Triple-jointed", "関節が異様に柔らかい。"),
            ("mutation:Triple-jointed:rank:1", "敏捷+{{rules|2}}\nスプリントと敏捷前提のスキルが使用後にクールダウンしない確率 {{rules|10%}}"),
            ("mutation:Triple-jointed:rank:2", "敏捷+{{rules|2}}\nスプリントと敏捷前提のスキルが使用後にクールダウンしない確率 {{rules|13%}}"),
            ("This rank", "現在ランク"),
            ("Next rank", "次ランク"));

        const string source =
            "Your joints stretch much further than usual.\n\n{{w|This rank}}:\n+{{rules|2}} Agility\n{{rules|10%}} chance that Sprint and skills with Agility prerequisites don't go on cooldown after use\n\n{{w|Next rank}}:\n+{{rules|2}} Agility\n{{rules|13%}} chance that Sprint and skills with Agility prerequisites don't go on cooldown after use\n\n{{C|* This mutationの base rank is 1.}}\n\nIt will cost {{C|1}} mutation point to increase 三重関節's rank by 1.\nDo you wish to increase this mutationの rank?";
        const string markupTransformedSource =
            "&yYour joints stretch much further than usual.\n\n&wThis rank&y:\n+&C2&y Agility\n&C10%&y chance that Sprint and skills with Agility prerequisites don't go on cooldown after use\n\n&wNext rank&y:\n+&C2&y Agility\n&C13%&y chance that Sprint and skills with Agility prerequisites don't go on cooldown after use\n\n&C* This mutationの base rank is 1.&y\n\nIt will cost &C1&y mutation point to increase 三重関節's rank by 1.\nDo you wish to increase this mutationの rank?";

        object? ownerState = null;
        StatusScreenMutationPopupTranslationPatch.Prefix(
            new DummyCharacterMutation { EntryName = "Triple-jointed", DisplayName = "三重関節", Level = 1 },
            out ownerState);
        PopupTranslatedMessageHandoff.EnterScope(out var handoffScope);
        try
        {
            _ = PopupShowSemanticPipeline.TranslateMessage(source, nameof(PopupShowTranslationPatch));

            var harmonyId = CreateHarmonyId();
            var harmony = new Harmony(harmonyId);

            try
            {
                harmony.Patch(
                    original: RequireMethod(typeof(DummyPopupMessageTarget), nameof(DummyPopupMessageTarget.ShowPopup)),
                    prefix: new HarmonyMethod(RequireMethod(typeof(PopupMessageTranslationPatch), nameof(PopupMessageTranslationPatch.Prefix))));

                new DummyPopupMessageTarget().ShowPopup(markupTransformedSource);

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupMessageTarget.LastMessage, Does.Contain("関節が異様に柔らかい。"));
                    Assert.That(DummyPopupMessageTarget.LastMessage, Does.Contain("{{w|現在ランク}}:"));
                    Assert.That(DummyPopupMessageTarget.LastMessage, Does.Contain("{{C|* この変異の基本ランクは1。}}"));
                    Assert.That(DummyPopupMessageTarget.LastMessage, Does.EndWith("三重関節のランクを1上げるには変異ポイントが{{C|1}}ポイント必要だ。\nこの変異のランクを上げますか？"));
                    Assert.That(DummyPopupMessageTarget.LastMessage, Does.Not.Contain("Your joints"));
                });
            }
            finally
            {
                harmony.UnpatchAll(harmonyId);
            }
        }
        finally
        {
            PopupTranslatedMessageHandoff.ExitScope(handoffScope);
            _ = StatusScreenMutationPopupTranslationPatch.Finalizer(null, ownerState);
        }
    }

    [Test]
    public void Prefix_UsesDetachedPopupShowOwnerHandoff_WhenNewPopupRunsAfterShowScopeExits()
    {
        WriteDictionary(
            ("mutation:Freezing Ray", "任意の方向へ冷気の光線を放つ。近接攻撃でも目標の体温を下げる力を帯びる。"),
            ("mutation:Freezing Ray:rank:9", "選んだ方向に9マスの冷気線を放つ。\nダメージ: {{rules|9d3}}\nクールダウン: 20ラウンド\n近接攻撃時に敵を{{rules|-9d4}}度冷却する。"),
            ("mutation:Freezing Ray:rank:10", "選んだ方向に9マスの冷気線を放つ。\nダメージ: {{rules|10d3}}\nクールダウン: 20ラウンド\n近接攻撃時に敵を{{rules|-10d4}}度冷却する。"),
            ("This rank", "現在ランク"),
            ("Next rank", "次ランク"));

        const string source =
            "You emit a ray of frost from your forefeet.\n\n{{w|This rank}}:\nEmits a 9-square ray of frost in the direction of your choice.\nDamage: {{rules|9d3+2}}\nCooldown: 20 rounds\nMelee attacks cool opponents by {{rules|-9d4}} degrees\n\n{{w|Next rank}}:\nEmits a 9-square ray of frost in the direction of your choice.\nDamage: {{rules|10d3+2}}\nCooldown: 20 rounds\nMelee attacks cool opponents by {{rules|-10d4}} degrees\n\n{{C|* This mutationの base rank is 6.}}\n{{G|+ This mutationの rank is increased by 3 due to being rapidly advanced 1 time.}}\n\nIt will cost {{C|1}} mutation point to increase 凍結線's rank by 1.\nDo you wish to increase this mutationの rank?";
        const string markupTransformedSource =
            "&yYou emit a ray of frost from your forefeet.\n\n&wThis rank&y:\nEmits a 9-square ray of frost in the direction of your choice.\nDamage: &C9d3+2&y\nCooldown: 20 rounds\nMelee attacks cool opponents by &C-9d4&y degrees\n\n&wNext rank&y:\nEmits a 9-square ray of frost in the direction of your choice.\nDamage: &C10d3+2&y\nCooldown: 20 rounds\nMelee attacks cool opponents by &C-10d4&y degrees\n\n&C* This mutationの base rank is 6.&y\n&G+ This mutationの rank is increased by 3 due to being rapidly advanced 1 time.&y\n\nIt will cost &C1&y mutation point to increase 凍結線's rank by 1.\nDo you wish to increase this mutationの rank?";

        object? ownerState = null;
        StatusScreenMutationPopupTranslationPatch.Prefix(
            new DummyCharacterMutation { EntryName = "Freezing Ray", DisplayName = "凍結線", Level = 9 },
            out ownerState);
        PopupTranslatedMessageHandoff.EnterScope(out var handoffScope);
        try
        {
            _ = PopupShowSemanticPipeline.TranslateMessage(source, nameof(PopupShowTranslationPatch));
        }
        finally
        {
            PopupTranslatedMessageHandoff.ExitScope(handoffScope, retainPendingEntries: true);
            _ = StatusScreenMutationPopupTranslationPatch.Finalizer(null, ownerState);
        }

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupMessageTarget), nameof(DummyPopupMessageTarget.ShowPopup)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupMessageTranslationPatch), nameof(PopupMessageTranslationPatch.Prefix))));

            new DummyPopupMessageTarget().ShowPopup(markupTransformedSource);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupMessageTarget.LastMessage, Does.Contain("任意の方向へ冷気の光線を放つ。"));
                Assert.That(DummyPopupMessageTarget.LastMessage, Does.Contain("{{w|現在ランク}}:"));
                Assert.That(DummyPopupMessageTarget.LastMessage, Does.Contain("{{C|* この変異の基本ランクは6。}}"));
                Assert.That(DummyPopupMessageTarget.LastMessage, Does.Contain("{{G|+ この変異のランクは1回の急速成長により3上昇している。}}"));
                Assert.That(DummyPopupMessageTarget.LastMessage, Does.EndWith("凍結線のランクを1上げるには変異ポイントが{{C|1}}ポイント必要だ。\nこの変異のランクを上げますか？"));
                Assert.That(DummyPopupMessageTarget.LastMessage, Does.Not.Contain("You emit a ray"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_UsesDetachedPopupShowOwnerHandoff_WhenNewPopupRunsOnUiThread()
    {
        PopupTranslatedMessageHandoff.EnterScope(out var handoffScope);
        try
        {
            PopupTranslatedMessageHandoff.Remember("{{C|same text}}", "{{C|翻訳済み}}");
        }
        finally
        {
            PopupTranslatedMessageHandoff.ExitScope(handoffScope, retainPendingEntries: true);
        }

        string? translated = null;
        Exception? thrown = null;
        var thread = new System.Threading.Thread(() =>
        {
            try
            {
                var message = "&Csame text&y";
                string? title = null;
                string? contextTitle = null;
                PopupMessageTranslationPatch.Prefix(
                    ref message,
                    null,
                    null,
                    ref title,
                    ref contextTitle,
                    null);
                translated = message;
            }
            catch (Exception ex)
            {
                thrown = ex;
            }
        });

        thread.Start();
        Assert.That(thread.Join(5000), Is.True);
        if (thrown is not null)
        {
            Assert.Fail(thrown.ToString());
        }

        Assert.That(translated, Is.EqualTo("{{C|翻訳済み}}"));
    }

    [Test]
    public void Prefix_UsesDetachedPopupShowOwnerHandoff_WhenNewPopupWrapsMarkupTransformedBody()
    {
        WriteDictionary(
            ("mutation:Multiple Legs", "脚がもう1組ある。\n\n移動速度が上がり、所持重量の許容量も増える。"),
            ("mutation:Multiple Legs:rank:4", "移動速度+{{rules|80}}\n運搬容量+{{rules|9%}}"),
            ("mutation:Multiple Legs:rank:5", "移動速度+{{rules|100}}\n運搬容量+{{rules|10%}}"),
            ("This rank", "現在ランク"),
            ("Next rank", "次ランク"));

        const string source =
            "You have an extra set of legs.\n\n{{w|This rank}}:\n+{{rules|80}} move speed\n+{{rules|9%}} carry capacity\n\n{{w|Next rank}}:\n+{{rules|100}} move speed\n+{{rules|10%}} carry capacity\n\n{{C|* This mutationの base rank is 4.}}\n\nIt will cost {{C|1}} mutation point to increase 多脚's rank by 1.\nDo you wish to increase this mutationの rank?";
        const string markupTransformedSource =
            "{{y|&yYou have an extra set of legs.\n\n&wThis rank&y:\n+&C80&y move speed\n+&C9%&y carry capacity\n\n&wNext rank&y:\n+&C100&y move speed\n+&C10%&y carry capacity\n\n&C* This mutationの base rank is 4.&y\n\nIt will cost &C1&y mutation point to increase 多脚's rank by 1.\nDo you wish to increase this mutationの rank?}}";

        object? ownerState = null;
        StatusScreenMutationPopupTranslationPatch.Prefix(
            new DummyCharacterMutation { EntryName = "Multiple Legs", DisplayName = "多脚", Level = 4 },
            out ownerState);
        PopupTranslatedMessageHandoff.EnterScope(out var handoffScope);
        try
        {
            _ = PopupShowSemanticPipeline.TranslateMessage(source, nameof(PopupShowTranslationPatch));
        }
        finally
        {
            PopupTranslatedMessageHandoff.ExitScope(handoffScope, retainPendingEntries: true);
            _ = StatusScreenMutationPopupTranslationPatch.Finalizer(null, ownerState);
        }

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupMessageTarget), nameof(DummyPopupMessageTarget.ShowPopup)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupMessageTranslationPatch), nameof(PopupMessageTranslationPatch.Prefix))));

            new DummyPopupMessageTarget().ShowPopup(markupTransformedSource);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupMessageTarget.LastMessage, Does.Contain("脚がもう1組ある。"));
                Assert.That(DummyPopupMessageTarget.LastMessage, Does.Contain("{{w|現在ランク}}:"));
                Assert.That(DummyPopupMessageTarget.LastMessage, Does.Contain("{{C|* この変異の基本ランクは4。}}"));
                Assert.That(DummyPopupMessageTarget.LastMessage, Does.EndWith("多脚のランクを1上げるには変異ポイントが{{C|1}}ポイント必要だ。\nこの変異のランクを上げますか？"));
                Assert.That(DummyPopupMessageTarget.LastMessage, Does.Not.Contain("You have an extra set of legs"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_DropsDetachedPopupShowHandoff_WhenDifferentPopupArrivesFirst()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        PopupTranslatedMessageHandoff.EnterScope(out var handoffScope);
        try
        {
            PopupTranslatedMessageHandoff.Remember("{{R|same text}}", "{{R|翻訳済み}}");
        }
        finally
        {
            PopupTranslatedMessageHandoff.ExitScope(handoffScope, retainPendingEntries: true);
        }

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupMessageTarget), nameof(DummyPopupMessageTarget.ShowPopup)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupMessageTranslationPatch), nameof(PopupMessageTranslationPatch.Prefix))));

            var target = new DummyPopupMessageTarget();
            target.ShowPopup("{{G|different text}}");
            var firstMessage = DummyPopupMessageTarget.LastMessage;
            target.ShowPopup("{{R|same text}}");

            Assert.Multiple(() =>
            {
                Assert.That(firstMessage, Is.EqualTo("{{G|different text}}"));
                Assert.That(DummyPopupMessageTarget.LastMessage, Is.EqualTo("{{R|same text}}"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_DoesNotReusePopupShowHandoffAcrossDifferentColorShape()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupMessageTarget), nameof(DummyPopupMessageTarget.ShowPopup)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupMessageTranslationPatch), nameof(PopupMessageTranslationPatch.Prefix))));

            var target = new DummyPopupMessageTarget();
            string? firstMessage = null;
            string? secondMessage = null;
            WithPopupHandoffScope(() =>
            {
                PopupTranslatedMessageHandoff.Remember("{{R|same text}}", "{{R|翻訳済み}}");
                target.ShowPopup("{{G|same text}}");
                firstMessage = DummyPopupMessageTarget.LastMessage;
                target.ShowPopup("{{R|same text}}");
                secondMessage = DummyPopupMessageTarget.LastMessage;
                target.ShowPopup("{{R|same text}}");
            });

            Assert.Multiple(() =>
            {
                Assert.That(firstMessage, Is.EqualTo("{{G|same text}}"));
                Assert.That(secondMessage, Is.EqualTo("{{R|翻訳済み}}"));
                Assert.That(DummyPopupMessageTarget.LastMessage, Is.EqualTo("{{R|same text}}"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_DoesNotDropPopupShowHandoff_WhenDifferentMessageArrivesFirst()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupMessageTarget), nameof(DummyPopupMessageTarget.ShowPopup)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupMessageTranslationPatch), nameof(PopupMessageTranslationPatch.Prefix))));

            var target = new DummyPopupMessageTarget();
            string? unrelatedMessage = null;
            WithPopupHandoffScope(() =>
            {
                PopupTranslatedMessageHandoff.Remember("{{R|same text}}", "{{R|翻訳済み}}");
                target.ShowPopup("{{G|different text}}");
                unrelatedMessage = DummyPopupMessageTarget.LastMessage;
                target.ShowPopup("{{R|same text}}");
            });

            Assert.Multiple(() =>
            {
                Assert.That(unrelatedMessage, Is.EqualTo("{{G|different text}}"));
                Assert.That(DummyPopupMessageTarget.LastMessage, Is.EqualTo("{{R|翻訳済み}}"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_RemovesAllDetachedPopupShowHandoffs_WhenDetachedEntryMatches()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        PopupTranslatedMessageHandoff.EnterScope(out var handoffScope);
        try
        {
            PopupTranslatedMessageHandoff.Remember("{{R|same text}}", "{{R|翻訳済み}}");
            PopupTranslatedMessageHandoff.Remember("{{B|stale text}}", "{{B|残留}}");
        }
        finally
        {
            PopupTranslatedMessageHandoff.ExitScope(handoffScope, retainPendingEntries: true);
        }

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupMessageTarget), nameof(DummyPopupMessageTarget.ShowPopup)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupMessageTranslationPatch), nameof(PopupMessageTranslationPatch.Prefix))));

            var target = new DummyPopupMessageTarget();
            target.ShowPopup("{{R|same text}}");
            var matchedMessage = DummyPopupMessageTarget.LastMessage;
            target.ShowPopup("{{B|stale text}}");

            Assert.Multiple(() =>
            {
                Assert.That(matchedMessage, Is.EqualTo("{{R|翻訳済み}}"));
                Assert.That(DummyPopupMessageTarget.LastMessage, Is.EqualTo("{{B|stale text}}"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_PreservesNestedPopupShowHandoffs()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupMessageTarget), nameof(DummyPopupMessageTarget.ShowPopup)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupMessageTranslationPatch), nameof(PopupMessageTranslationPatch.Prefix))));

            var target = new DummyPopupMessageTarget();
            string? innerMessage = null;
            WithPopupHandoffScope(() =>
            {
                PopupTranslatedMessageHandoff.Remember("{{R|outer text}}", "{{R|外側}}");
                WithPopupHandoffScope(() =>
                {
                    PopupTranslatedMessageHandoff.Remember("{{G|inner text}}", "{{G|内側}}");
                    target.ShowPopup("{{G|inner text}}");
                    innerMessage = DummyPopupMessageTarget.LastMessage;
                });

                target.ShowPopup("{{R|outer text}}");
            });

            Assert.Multiple(() =>
            {
                Assert.That(innerMessage, Is.EqualTo("{{G|内側}}"));
                Assert.That(DummyPopupMessageTarget.LastMessage, Is.EqualTo("{{R|外側}}"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_ConsumesPopupShowHandoffOnce()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupMessageTarget), nameof(DummyPopupMessageTarget.ShowPopup)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupMessageTranslationPatch), nameof(PopupMessageTranslationPatch.Prefix))));

            var target = new DummyPopupMessageTarget();
            string? firstMessage = null;
            WithPopupHandoffScope(() =>
            {
                PopupTranslatedMessageHandoff.Remember("{{R|same text}}", "{{R|翻訳済み}}");
                target.ShowPopup("{{R|same text}}");
                firstMessage = DummyPopupMessageTarget.LastMessage;
                target.ShowPopup("{{R|same text}}");
            });

            Assert.Multiple(() =>
            {
                Assert.That(firstMessage, Is.EqualTo("{{R|翻訳済み}}"));
                Assert.That(DummyPopupMessageTarget.LastMessage, Is.EqualTo("{{R|same text}}"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_DoesNotConsumeStaleHandoffAfterPopupShowScopeExits()
    {
        WithPopupHandoffScope(() =>
            PopupTranslatedMessageHandoff.Remember("{{R|same text}}", "{{R|翻訳済み}}"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupMessageTarget), nameof(DummyPopupMessageTarget.ShowPopup)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupMessageTranslationPatch), nameof(PopupMessageTranslationPatch.Prefix))));

            WithPopupHandoffScope(() =>
                new DummyPopupMessageTarget().ShowPopup("{{R|same text}}"));

            Assert.That(DummyPopupMessageTarget.LastMessage, Is.EqualTo("{{R|same text}}"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_SkipsRetranslation_WhenDirectTranslationMarkerPresent()
    {
        WriteDictionary(("Cancel", "キャンセル"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupMessageTarget), nameof(DummyPopupMessageTarget.ShowPopup)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupMessageTranslationPatch), nameof(PopupMessageTranslationPatch.Prefix))));

            var markedMessage = "\u0001既に翻訳済み";
            var target = new DummyPopupMessageTarget();
            target.ShowPopup(markedMessage);

            // \x01 marker is stripped by TranslatePopupTextForRoute but translation is skipped
            Assert.That(DummyPopupMessageTarget.LastMessage, Is.EqualTo("既に翻訳済み"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesPlainHotkeyButtons_WhenPatched()
    {
        WriteDictionary(("B Cancel", "B キャンセル"));

        var buttons = new List<DummyPopupMessageItem>
        {
            new("{{W|B}} Cancel", "Cancel", "Cancel"),
        };

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupMessageTarget), nameof(DummyPopupMessageTarget.ShowPopup)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupMessageTranslationPatch), nameof(PopupMessageTranslationPatch.Prefix))));

            new DummyPopupMessageTarget().ShowPopup("Prompt", buttons);

            Assert.That(DummyPopupMessageTarget.LastButtons![0].text, Is.EqualTo("{{W|B}} キャンセル"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_PreservesInventoryActionMenuItemData_WhenPatched()
    {
        WriteDictionary(
            ("[g] get", "[g] 拾う"),
            ("[e] equip (auto)", "[e] 自動で装備"));

        var buttons = new List<DummyPopupMessageItem>
        {
            new("{{W|[e]}} {{y|equip (auto)}}", "char:e", "option:0"),
        };
        var items = new List<DummyPopupMessageItem>
        {
            new("{{W|[g]}} {{y|get}}", "char:g", "option:2"),
        };

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupMessageTarget), nameof(DummyPopupMessageTarget.ShowPopup)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupMessageTranslationPatch), nameof(PopupMessageTranslationPatch.Prefix))));

            new DummyPopupMessageTarget().ShowPopup(
                "Pick an action",
                buttons,
                items: items,
                PopupID: "InventoryActionMenu:ABC123");

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupMessageTarget.LastButtons![0].text, Is.EqualTo("{{W|[e]}} {{y|equip (auto)}}"));
                Assert.That(DummyPopupMessageTarget.LastItems![0].text, Is.EqualTo("{{W|[g]}} {{y|get}}"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_RecordsProducerRouteTransforms_WithoutPopupSinkObservation_WhenPatched()
    {
        WriteDictionary(("Save Slots", "セーブ一覧"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupMessageTarget), nameof(DummyPopupMessageTarget.ShowPopup)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupMessageTranslationPatch), nameof(PopupMessageTranslationPatch.Prefix))));

            const string source = "Save Slots";
            new DummyPopupMessageTarget().ShowPopup(source);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupMessageTarget.LastMessage, Is.EqualTo("セーブ一覧"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(PopupMessageTranslationPatch),
                        "Popup.ProducerText.Exact"),
                    Is.GreaterThan(0));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(PopupTranslationPatch),
                        nameof(PopupMessageTranslationPatch),
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
    public void Prefix_TranslatesQuitPayloadAcrossOwnedFields_WhenPatched()
    {
        WriteDictionary(
            ("Are you sure you want to quit?", "本当に終了しますか？"),
            ("Quit Without Saving", "セーブせずに終了"),
            ("Game Menu", "ゲームメニュー"),
            ("[Enter] Submit", "[Enter] 送信"),
            ("[Esc] Cancel", "[Esc] キャンセル"),
            ("Continue playing", "続行する"));

        var buttons = new List<DummyPopupMessageItem>
        {
            new("{{W|[Enter]}} {{y|Submit}}", "Accept", "Accept"),
            new("{{W|[Esc]}} {{y|Cancel}}", "Cancel", "Cancel"),
        };
        var items = new List<DummyPopupMessageItem>
        {
            new("Continue playing", "Space", "Continue"),
        };

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupMessageTarget), nameof(DummyPopupMessageTarget.ShowPopup)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupMessageTranslationPatch), nameof(PopupMessageTranslationPatch.Prefix))));

            new DummyPopupMessageTarget().ShowPopup(
                "Are you sure you want to quit?",
                buttons,
                items: items,
                title: "Quit Without Saving",
                contextTitle: "Game Menu",
                WantsSpecificPrompt: "QUIT");

            var renderedMessage = DummyPopupMessageTarget.LastMessage;
            var renderedButton = DummyPopupMessageTarget.LastButtons![0].text;
            UITextSkinTranslationPatch.Prefix(ref renderedMessage);
            UITextSkinTranslationPatch.Prefix(ref renderedButton);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupMessageTarget.LastMessage, Is.EqualTo("本当に終了しますか？"));
                Assert.That(DummyPopupMessageTarget.LastTitle, Is.EqualTo("セーブせずに終了"));
                Assert.That(DummyPopupMessageTarget.LastContextTitle, Is.EqualTo("ゲームメニュー"));
                Assert.That(DummyPopupMessageTarget.LastButtons![0].text, Is.EqualTo("{{W|[Enter]}} {{y|送信}}"));
                Assert.That(DummyPopupMessageTarget.LastButtons[1].text, Is.EqualTo("{{W|[Esc]}} {{y|キャンセル}}"));
                Assert.That(DummyPopupMessageTarget.LastItems![0].text, Is.EqualTo("続行する"));
                Assert.That(DummyPopupMessageTarget.LastWantsSpecificPrompt, Is.EqualTo("QUIT"));
                Assert.That(renderedMessage, Is.EqualTo("本当に終了しますか？"));
                Assert.That(renderedButton, Is.EqualTo("{{W|[Enter]}} {{y|送信}}"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_RendersTranslatedBodyAtFinalOwner_WhenPatched()
    {
        WriteDictionary(("You do not have a missile weapon equipped!", "射撃武器を装備していない！"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupMessageTarget), nameof(DummyPopupMessageTarget.ShowPopup)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupMessageTranslationPatch), nameof(PopupMessageTranslationPatch.Prefix))));

            new DummyPopupMessageTarget().ShowPopup("You do not have a missile weapon equipped!");

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupMessageTarget.LastMessage, Is.EqualTo("射撃武器を装備していない！"));
                Assert.That(DummyPopupMessageTarget.LastRenderedBodyText, Is.EqualTo("{{y|射撃武器を装備していない！}}"));
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

    private static void WithPopupHandoffScope(Action action)
    {
        PopupTranslatedMessageHandoff.EnterScope(out var scopeId);
        try
        {
            action();
        }
        finally
        {
            PopupTranslatedMessageHandoff.ExitScope(scopeId);
        }
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
        File.WriteAllText(
            Path.Combine(dictionaryDirectory, "test.ja.json"),
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
