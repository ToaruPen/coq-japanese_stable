using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class ExaminerTranslationPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-examiner-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        WriteDisplayNameDictionary();

        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
        DummyPopupShow.Reset();
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

    [TestCase(
        nameof(DummyExaminerProducerTarget.ResultSuccess),
        "You now understand {{C|奇妙な装置}}.",
        "{{C|奇妙な装置}}を理解した。",
        "Understand")]
    [TestCase(
        nameof(DummyExaminerProducerTarget.ResultExceptionalSuccess),
        "You discover something about {{Y|古びた箱}} that was hidden!",
        "{{Y|古びた箱}}について隠されていたことを発見した！",
        "DiscoverHidden")]
    [TestCase(
        nameof(DummyExaminerProducerTarget.ResultFailure),
        "You are puzzled by {{R|ひび割れた銃}}.",
        "{{R|ひび割れた銃}}のことがわからない。",
        "Puzzled")]
    [TestCase(
        nameof(DummyExaminerProducerTarget.ResultFakeConfusionFailure),
        "You think you broke {{G|謎の装置}}...",
        "{{G|謎の装置}}を壊してしまった気がする。",
        "Broke")]
    [TestCase(
        nameof(DummyExaminerProducerTarget.ResultCriticalFailure),
        "You are puzzled by {{R|ひび割れた銃}}.",
        "{{R|ひび割れた銃}}のことがわからない。",
        "Puzzled")]
    public void Patch_TranslatesExaminerResultPopups_WhenOwnerPatched(
        string methodName,
        string source,
        string expected,
        string detail)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(ExaminerTranslationPatch),
            RequireOwnerMethod(methodName),
            () =>
            {
                var target = new DummyExaminerProducerTarget
                {
                    PopupMessageToShow = source,
                };

                InvokeOwnerMethod(target, methodName);

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
                    Assert.That(ExaminerHitCount(detail), Is.EqualTo(1));
                });
            });
    }

    [TestCase(
        nameof(DummyPopupShow.ShowFail),
        "Whatever it is, it's broken...",
        "それが何であれ、壊れている...",
        "Broken")]
    [TestCase(
        nameof(DummyPopupShow.ShowYesNoCancel),
        "{{Y|奇妙な装置}} is not owned by you, and examining it risks damaging it. Are you sure you want to do so?",
        "{{Y|奇妙な装置}}はあなたのものではない。調べるとそれを傷つけるおそれがある。それでもそうしますか？",
        "OwnedExamine")]
    [TestCase(
        nameof(DummyPopupShow.ShowYesNoCancel),
        "{{Y|箱}} is not owned by you, and examining a {{C|奇妙な装置}} inside it risks causing damage. Are you sure you want to do so?",
        "{{Y|箱}}はあなたのものではない。それの中にある{{C|奇妙な装置}}を調べると損傷を引き起こすおそれがある。それでもそうしますか？",
        "ContainerOwnedExamine")]
    public void Patch_TranslatesExaminerHandleEventPopups_WhenOwnerPatched(
        string popupMethod,
        string source,
        string expected,
        string detail)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(ExaminerTranslationPatch),
            RequireOwnerMethod(nameof(DummyExaminerProducerTarget.HandleEvent)),
            () =>
            {
                var target = new DummyExaminerProducerTarget
                {
                    PopupMessageToShow = source,
                    PopupMethod = popupMethod,
                };

                _ = target.HandleEvent(new DummyInventoryActionEvent());

                Assert.Multiple(() =>
                {
                    Assert.That(LastPopupMessage(popupMethod), Is.EqualTo(expected));
                    Assert.That(ExaminerHitCount(detail), Is.EqualTo(1));
                });
            });
    }

    [TestCase(
        "You identify your ピストル as a masterwork scoped チェーンピストル.",
        "ピストルを傑作 スコープ付き チェーンピストルだと鑑定した。",
        "Identify")]
    [TestCase(
        "You commit the distinguishing characteristics of the {{Y|奇妙な遺物}} to memory.",
        "{{Y|奇妙な遺物}}の特徴を記憶した。",
        "CommitMemory")]
    [TestCase(
        "You make some progress understanding the {{Y|奇妙な遺物}}.",
        "{{Y|奇妙な遺物}}の理解が少し進んだ。",
        "ProgressOnly")]
    [TestCase(
        "You make some progress understanding the {{Y|奇妙な遺物}}. It seems to be a masterwork scoped チェーンピストル.",
        "{{Y|奇妙な遺物}}の理解が少し進んだ。それは傑作 スコープ付き チェーンピストルだ。",
        "ProgressKnown")]
    [TestCase(
        "You make some progress understanding the {{Y|奇妙な遺物}}. It seems to be a ピストル, and you think it's probably a variety of チェーンピストル; you believe you would be able to recognize an ordinary one of those now.",
        "{{Y|奇妙な遺物}}の理解が少し進んだ。それはピストルで、おそらくチェーンピストルの一種だ。これで普通のチェーンピストルなら見分けられるはずだ。",
        "ProgressKnownVariety")]
    [TestCase(
        "You make some progress understanding the {{Y|奇妙な遺物}}. You think it's probably a variety of チェーンピストル, and you believe you would be able to recognize an ordinary one of those now.",
        "{{Y|奇妙な遺物}}の理解が少し進んだ。おそらくチェーンピストルの一種だ。これで普通のチェーンピストルなら見分けられるはずだ。",
        "ProgressVariety")]
    public void Patch_TranslatesExaminerIdentificationPopups_WhenOwnerPatched(
        string source,
        string expected,
        string detail)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(ExaminerTranslationPatch),
            RequireOwnerMethod(nameof(DummyExaminerProducerTarget.ResultPartialSuccess)),
            () =>
            {
                var target = new DummyExaminerProducerTarget
                {
                    PopupMessageToShow = source,
                };

                target.ResultPartialSuccess(new DummyGameObject());

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
                    Assert.That(ExaminerHitCount(detail), Is.EqualTo(1));
                });
            });
    }

    [TestCase(
        "MakeUnderstood",
        "You identify your ピストル as a masterwork scoped チェーンピストル.",
        "ピストルを傑作 スコープ付き チェーンピストルだと鑑定した。",
        "Identify")]
    [TestCase(
        "MakePartiallyUnderstood",
        "You make some progress understanding the {{Y|奇妙な遺物}}.",
        "{{Y|奇妙な遺物}}の理解が少し進んだ。",
        "ProgressOnly")]
    public void Patch_TranslatesExaminerMakeUnderstandingPopups_WhenOwnerPatched(
        string methodName,
        string source,
        string expected,
        string detail)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(ExaminerTranslationPatch),
            RequireMakeUnderstandingOwnerMethod(methodName),
            () =>
            {
                var target = new DummyExaminerMakeUnderstandingTarget
                {
                    PopupMessageToShow = source,
                };

                InvokeMakeUnderstandingOwnerMethod(target, methodName);

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
                    Assert.That(ExaminerHitCount(detail), Is.EqualTo(1));
                });
            });
    }

    [Test]
    public void Patch_TranslatesExaminerIdentify_WhenZeroWidthMarkupPrecedesPossessive()
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(ExaminerTranslationPatch),
            RequireOwnerMethod(nameof(DummyExaminerProducerTarget.ResultPartialSuccess)),
            () =>
            {
                var target = new DummyExaminerProducerTarget
                {
                    PopupMessageToShow = "You identify {{R|}} your ピストル as a masterwork scoped チェーンピストル.",
                };

                target.ResultPartialSuccess(new DummyGameObject());

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("ピストルを傑作 スコープ付き チェーンピストルだと鑑定した。"));
                    Assert.That(ExaminerHitCount("Identify"), Is.EqualTo(1));
                });
            });
    }

    [Test]
    public void Patch_TranslatesExaminerIdentify_DropsRuntimePairOfPrefixFromKnownItem()
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(ExaminerTranslationPatch),
            RequireOwnerMethod(nameof(DummyExaminerProducerTarget.ResultPartialSuccess)),
            () =>
            {
                var target = new DummyExaminerProducerTarget
                {
                    PopupMessageToShow = "You identify the 奇妙な遺物 as a pair of 尺骨刺激装置.",
                };

                target.ResultPartialSuccess(new DummyGameObject());

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("奇妙な遺物を尺骨刺激装置だと鑑定した。"));
                    Assert.That(ExaminerHitCount("Identify"), Is.EqualTo(1));
                });
            });
    }

    [Test]
    public void Patch_LeavesNonMatchingPartialSuccessPopupUnchanged_WhenOwnerPatched()
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(ExaminerTranslationPatch),
            RequireOwnerMethod(nameof(DummyExaminerProducerTarget.ResultPartialSuccess)),
            () =>
            {
                const string source = "You inspect {{C|奇妙な装置}} carefully.";
                var target = new DummyExaminerProducerTarget
                {
                    PopupMessageToShow = source,
                };

                target.ResultPartialSuccess(new DummyGameObject());

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
                    Assert.That(ExaminerHitCount("Identify"), Is.Zero);
                    Assert.That(ExaminerHitCount("CommitMemory"), Is.Zero);
                    Assert.That(ExaminerHitCount("ProgressOnly"), Is.Zero);
                    Assert.That(ExaminerHitCount("ProgressKnown"), Is.Zero);
                    Assert.That(ExaminerHitCount("ProgressKnownVariety"), Is.Zero);
                });
            });
    }

    [Test]
    public void Patch_DoesNotRetranslateDirectMarkedPartialSuccessPopup_WhenOwnerPatched()
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(ExaminerTranslationPatch),
            RequireOwnerMethod(nameof(DummyExaminerProducerTarget.ResultPartialSuccess)),
            () =>
            {
                const string unmarked = "You make some progress understanding the {{Y|奇妙な遺物}}.";
                var source = MessageFrameTranslator.MarkDirectTranslation(unmarked);
                var target = new DummyExaminerProducerTarget
                {
                    PopupMessageToShow = source,
                };

                target.ResultPartialSuccess(new DummyGameObject());

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(unmarked));
                    Assert.That(ExaminerHitCount("ProgressOnly"), Is.Zero);
                });
            });
    }

    [Test]
    public void Patch_TranslatesExaminerPartialSuccess_WhenRuntimeGrammarPrefixLeaksBeforeSeems()
    {
        const string source =
            "You make some progress understanding the {{Y|奇妙な遺物}}. ☻seemV2♥8▼♥It seems to be a ピストル, and you think it's probably a variety of チェーンピストル; you believe you would be able to recognize an ordinary one of those now.";

        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(ExaminerTranslationPatch),
            RequireOwnerMethod(nameof(DummyExaminerProducerTarget.ResultPartialSuccess)),
            () =>
            {
                var target = new DummyExaminerProducerTarget
                {
                    PopupMessageToShow = source,
                };

                target.ResultPartialSuccess(new DummyGameObject());

                Assert.Multiple(() =>
                {
                    Assert.That(
                        DummyPopupShow.LastShowMessage,
                        Is.EqualTo("{{Y|奇妙な遺物}}の理解が少し進んだ。それはピストルで、おそらくチェーンピストルの一種だ。これで普通のチェーンピストルなら見分けられるはずだ。"));
                    Assert.That(ExaminerHitCount("ProgressKnownVariety"), Is.EqualTo(1));
                });
            });
    }

    [Test]
    public void Patch_TranslatesExaminerPartialSuccess_UsesExplicitOrdinaryRecognitionTarget()
    {
        const string source =
            "You make some progress understanding the {{Y|奇妙な遺物}}. It seems to be a ピストル, and you think it's probably a variety of チェーンピストル; you believe you would be able to recognize an ordinary ピストル of those now.";

        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(ExaminerTranslationPatch),
            RequireOwnerMethod(nameof(DummyExaminerProducerTarget.ResultPartialSuccess)),
            () =>
            {
                var target = new DummyExaminerProducerTarget
                {
                    PopupMessageToShow = source,
                };

                target.ResultPartialSuccess(new DummyGameObject());

                Assert.That(
                    DummyPopupShow.LastShowMessage,
                    Is.EqualTo("{{Y|奇妙な遺物}}の理解が少し進んだ。それはピストルで、おそらくチェーンピストルの一種だ。これで普通のピストルなら見分けられるはずだ。"));
            });
    }

    [Test]
    public void Patch_DoesNotTranslateHandleEventPopup_WhenOwnerAbsent()
    {
        const string source =
            "{{Y|奇妙な装置}} is not owned by you, and examining it risks damaging it. Are you sure you want to do so?";

        OwnerPopupRouteTestHarness.WithPatchedPopupOnly(
            () =>
            {
                _ = DummyPopupShow.ShowYesNoCancel(source);

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowYesNoCancelMessage, Is.EqualTo(source));
                    Assert.That(ExaminerHitCount("OwnedExamine"), Is.Zero);
                });
            });
    }

    [Test]
    public void Patch_DoesNotRetranslateDirectMarkedHandleEventPopup_WhenOwnerPatched()
    {
        const string unmarked =
            "{{Y|奇妙な装置}} is not owned by you, and examining it risks damaging it. Are you sure you want to do so?";
        var source = MessageFrameTranslator.MarkDirectTranslation(unmarked);

        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(ExaminerTranslationPatch),
            RequireOwnerMethod(nameof(DummyExaminerProducerTarget.HandleEvent)),
            () =>
            {
                var target = new DummyExaminerProducerTarget
                {
                    PopupMessageToShow = source,
                    PopupMethod = nameof(DummyPopupShow.ShowYesNoCancel),
                };

                _ = target.HandleEvent(new DummyInventoryActionEvent());

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowYesNoCancelMessage, Is.EqualTo(unmarked));
                    Assert.That(ExaminerHitCount("OwnedExamine"), Is.Zero);
                });
            });
    }

    [Test]
    public void Patch_DoesNotTranslateExaminerPopup_WhenOwnerAbsent()
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOnly(
            () =>
            {
                const string source = "You now understand {{C|奇妙な装置}}.";
                DummyPopupShow.Show(source);

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
                    Assert.That(ExaminerHitCount("Understand"), Is.Zero);
                });
            });
    }

    [Test]
    public void Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(ExaminerTranslationPatch),
            RequireOwnerMethod(nameof(DummyExaminerProducerTarget.ResultFailure)),
            () =>
            {
                var source = MessageFrameTranslator.MarkDirectTranslation("You are puzzled by {{R|ひび割れた銃}}.");
                var target = new DummyExaminerProducerTarget
                {
                    PopupMessageToShow = source,
                };

                target.ResultFailure(new DummyGameObject());

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("You are puzzled by {{R|ひび割れた銃}}."));
                    Assert.That(ExaminerHitCount("Puzzled"), Is.Zero);
                });
            });
    }

    [Test]
    public void Patch_LeavesDirectMarkedNonMatchingEnglishPopupUnchanged_WhenOwnerPatched()
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(ExaminerTranslationPatch),
            RequireOwnerMethod(nameof(DummyExaminerProducerTarget.ResultFailure)),
            () =>
            {
                var source = MessageFrameTranslator.MarkDirectTranslation("You inspect {{C|奇妙な装置}} carefully.");
                var target = new DummyExaminerProducerTarget
                {
                    PopupMessageToShow = source,
                };

                target.ResultFailure(new DummyGameObject());

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("You inspect {{C|奇妙な装置}} carefully."));
                    Assert.That(ExaminerHitCount("Understand"), Is.Zero);
                    Assert.That(ExaminerHitCount("DiscoverHidden"), Is.Zero);
                    Assert.That(ExaminerHitCount("Puzzled"), Is.Zero);
                    Assert.That(ExaminerHitCount("Broke"), Is.Zero);
                });
            });
    }

    [Test]
    public void Patch_LeavesNonMatchingEnglishPopupUnchanged_WhenOwnerPatched()
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(ExaminerTranslationPatch),
            RequireOwnerMethod(nameof(DummyExaminerProducerTarget.ResultFailure)),
            () =>
            {
                const string source = "You inspect {{C|奇妙な装置}} carefully.";
                var target = new DummyExaminerProducerTarget
                {
                    PopupMessageToShow = source,
                };

                target.ResultFailure(new DummyGameObject());

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
                    Assert.That(ExaminerHitCount("Understand"), Is.Zero);
                    Assert.That(ExaminerHitCount("DiscoverHidden"), Is.Zero);
                    Assert.That(ExaminerHitCount("Puzzled"), Is.Zero);
                    Assert.That(ExaminerHitCount("Broke"), Is.Zero);
                });
            });
    }

    [Test]
    public void Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(ExaminerTranslationPatch),
            RequireOwnerMethod(nameof(DummyExaminerProducerTarget.ResultFailure)),
            () =>
            {
                var target = new DummyExaminerProducerTarget();

                target.ResultFailure(new DummyGameObject());

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(string.Empty));
                    Assert.That(ExaminerHitCount("Puzzled"), Is.Zero);
                });
            });
    }

    private static MethodInfo RequireOwnerMethod(string methodName)
    {
        if (methodName == nameof(DummyExaminerProducerTarget.HandleEvent))
        {
            return OwnerPopupRouteTestHarness.RequireMethod(
                typeof(DummyExaminerProducerTarget),
                methodName,
                typeof(DummyInventoryActionEvent));
        }

        return OwnerPopupRouteTestHarness.RequireMethod(typeof(DummyExaminerProducerTarget), methodName, typeof(DummyGameObject));
    }

    private static MethodInfo RequireMakeUnderstandingOwnerMethod(string methodName)
    {
        return OwnerPopupRouteTestHarness.RequireMethod(typeof(DummyExaminerMakeUnderstandingTarget), methodName, typeof(bool));
    }

    private static void InvokeOwnerMethod(DummyExaminerProducerTarget target, string methodName)
    {
        _ = RequireOwnerMethod(methodName).Invoke(target, new object[] { new DummyGameObject() });
    }

    private static void InvokeMakeUnderstandingOwnerMethod(DummyExaminerMakeUnderstandingTarget target, string methodName)
    {
        _ = RequireMakeUnderstandingOwnerMethod(methodName).Invoke(target, new object[] { true });
    }

    private static int ExaminerHitCount(string detail)
    {
        return OwnerPopupRouteTestHarness.RouteHitCount(typeof(ExaminerTranslationPatch), detail);
    }

    private static string? LastPopupMessage(string popupMethod)
    {
        return popupMethod == nameof(DummyPopupShow.ShowYesNoCancel)
            ? DummyPopupShow.LastShowYesNoCancelMessage
            : DummyPopupShow.LastShowMessage;
    }

    private void WriteDisplayNameDictionary()
    {
        WriteDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("masterwork", "GetDisplayName.Adjective", "傑作"),
            ("scoped", "GetDisplayName.Adjective", "スコープ付き"));
    }

    private void WriteDictionaryFile(string fileName, params (string key, string? context, string text)[] entries)
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
                .Append('"');
            if (entries[index].context is not null)
            {
                builder.Append(",\"context\":\"")
                    .Append(EscapeJson(entries[index].context!))
                    .Append('"');
            }

            builder.Append(",\"text\":\"")
                .Append(EscapeJson(entries[index].text))
                .Append("\"}");
        }

        builder.Append("]}");
        File.WriteAllText(Path.Combine(tempDirectory, fileName), builder.ToString(), Encoding.UTF8);
    }

    private static string EscapeJson(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private sealed class DummyExaminerMakeUnderstandingTarget
    {
        public string PopupMessageToShow { get; set; } = string.Empty;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool MakeUnderstood(bool showMessage)
        {
            return ShowPopup(showMessage);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool MakePartiallyUnderstood(bool showMessage)
        {
            return ShowPopup(showMessage);
        }

        private bool ShowPopup(bool showMessage)
        {
            if (showMessage)
            {
                DummyPopupShow.Show(PopupMessageToShow);
            }

            return true;
        }
    }
}
