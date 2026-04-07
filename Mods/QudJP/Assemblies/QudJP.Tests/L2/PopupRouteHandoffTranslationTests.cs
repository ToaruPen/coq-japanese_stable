using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class PopupRouteHandoffTranslationTests
{
    private string tempDirectory = null!;
    private string dictionaryDirectory = null!;
    private string patternFilePath = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-popup-route-handoff-l2", Guid.NewGuid().ToString("N"));
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
        File.WriteAllText(patternFilePath, "{\"patterns\":[]}\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        DummyPopupShow.Reset();
        DummyPopupGenericTarget.Reset();
        DummyPopupMessageTarget.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        MessagePatternTranslator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void ShowRoute_HandsOffTranslatedMessage_ToPopupMessageAndUITextSkin()
    {
        WriteDictionary(("You do not have a missile weapon equipped!", "飛び道具を装備していない！"));

        using var showPatch = PatchMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.Show), typeof(PopupShowTranslationPatch));
        using var popupMessagePatch = PatchMethod(typeof(DummyPopupMessageTarget), nameof(DummyPopupMessageTarget.ShowPopup), typeof(PopupMessageTranslationPatch));

        DummyPopupShow.Show("You do not have a missile weapon equipped!");
        new DummyPopupMessageTarget().ShowPopup(DummyPopupShow.LastShowMessage!);

        var rendered = WrapPopupBody(DummyPopupMessageTarget.LastMessage);
        var sinkText = UITextSkinTranslationPatch.TranslatePreservingColors(rendered, nameof(PopupMessageTranslationPatch));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("飛び道具を装備していない！"));
            Assert.That(DummyPopupMessageTarget.LastMessage, Is.EqualTo("飛び道具を装備していない！"));
            Assert.That(sinkText, Is.EqualTo("{{y|飛び道具を装備していない！}}"));
            Assert.That(
                DynamicTextObservability.GetRouteFamilyHitCountForTests(
                    nameof(PopupShowTranslationPatch),
                    "Popup.ProducerText.Exact"),
                Is.GreaterThan(0));
            Assert.That(
                SinkObservation.GetHitCountForTests(
                    nameof(UITextSkinTranslationPatch),
                    nameof(PopupMessageTranslationPatch),
                    SinkObservation.ObservationOnlyDetail,
                    "{{y|You do not have a missile weapon equipped!}}",
                    "You do not have a missile weapon equipped!"),
                Is.EqualTo(0));
        });
    }

    [Test]
    public void AskStringQuitRoute_HandsOffTranslatedPrompt_AndConfirmButtons()
    {
        WriteDictionary(
            ("Are you sure you want to quit?", "本当に終了しますか？"),
            ("Hold to Accept", "長押しで決定"),
            ("Quit Without Saving", "保存せず終了"),
            ("[Tab] Hold to Accept", "[Tab] 長押しで決定"),
            ("[Esc] Quit Without Saving", "[Esc] 保存せず終了"));

        using var askStringPatch = PatchMethod(typeof(DummyPopupGenericTarget), nameof(DummyPopupGenericTarget.AskStringAsync), typeof(PopupAskStringTranslationPatch));
        using var popupMessagePatch = PatchMethod(typeof(DummyPopupMessageTarget), nameof(DummyPopupMessageTarget.ShowPopup), typeof(PopupMessageTranslationPatch));

        _ = DummyPopupGenericTarget.AskStringAsync("Are you sure you want to quit?", WantsSpecificPrompt: "QUIT")
            .GetAwaiter()
            .GetResult();

        var clippedMessage = SimulateClipText(DummyPopupGenericTarget.LastAskStringMessage, 5);
        var buttons = new List<DummyPopupMessageItem>
        {
            new("{{W|[Tab]}} {{y|Hold to Accept}}", "Submit", "Submit"),
            new("{{W|[Esc]}} {{y|Quit Without Saving}}", "Cancel", "Cancel"),
        };

        new DummyPopupMessageTarget().ShowPopup(
            clippedMessage,
            buttons,
            WantsSpecificPrompt: "QUIT");

        var rendered = WrapPopupBody(DummyPopupMessageTarget.LastMessage);
        var sinkText = UITextSkinTranslationPatch.TranslatePreservingColors(rendered, nameof(PopupMessageTranslationPatch));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupGenericTarget.LastAskStringMessage, Is.EqualTo("本当に終了しますか？"));
            Assert.That(clippedMessage, Does.Contain('\n'));
            Assert.That(DummyPopupMessageTarget.LastMessage, Is.EqualTo(clippedMessage));
            Assert.That(DummyPopupMessageTarget.LastButtons, Is.Not.Null);
            Assert.That(DummyPopupMessageTarget.LastButtons![0].text, Is.EqualTo("{{W|[Tab]}} {{y|長押しで決定}}"));
            Assert.That(DummyPopupMessageTarget.LastButtons[1].text, Is.EqualTo("{{W|[Esc]}} {{y|保存せず終了}}"));
            Assert.That(DummyPopupMessageTarget.LastWantsSpecificPrompt, Is.EqualTo("QUIT"));
            Assert.That(sinkText, Is.EqualTo(rendered));
            Assert.That(
                DynamicTextObservability.GetRouteFamilyHitCountForTests(
                    nameof(PopupAskStringTranslationPatch),
                    "Popup.ProducerText.Exact"),
                Is.GreaterThan(0));
        });
    }

    [Test]
    public void PickOptionRoute_HandsOffTranslatedSiblingOptions_ToSelectableText()
    {
        WriteDictionary(
            ("Look", "調べる"),
            ("Show Effects", "効果を見る"),
            ("Detonate", "起爆する"),
            ("Quit Without Saving", "保存せず終了"));

        using var pickOptionPatch = PatchMethod(typeof(DummyPopupGenericTarget), nameof(DummyPopupGenericTarget.PickOption), typeof(PopupPickOptionTranslationPatch));

        DummyPopupGenericTarget.PickOption(Options: new[] { "Look", "Show Effects", "Detonate", "Quit Without Saving" });

        Assert.That(DummyPopupGenericTarget.LastPickOptionOptions, Is.Not.Null);

        var renderedOptions = new[]
        {
            "{{W|[L]}} {{y|" + DummyPopupGenericTarget.LastPickOptionOptions![0] + "}}",
            "{{W|[E]}} {{y|" + DummyPopupGenericTarget.LastPickOptionOptions[1] + "}}",
            "{{W|[N]}} {{y|" + DummyPopupGenericTarget.LastPickOptionOptions[2] + "}}",
            "{{W|[Q]}} {{y|" + DummyPopupGenericTarget.LastPickOptionOptions[3] + "}}",
        };

        Assert.Multiple(() =>
        {
            Assert.That(
                DummyPopupGenericTarget.LastPickOptionOptions,
                Is.EqualTo(new[] { "調べる", "効果を見る", "起爆する", "保存せず終了" }));
            Assert.That(
                UITextSkinTranslationPatch.TranslatePreservingColors(renderedOptions[0], nameof(PopupMessageTranslationPatch)),
                Is.EqualTo(renderedOptions[0]));
            Assert.That(
                UITextSkinTranslationPatch.TranslatePreservingColors(renderedOptions[1], nameof(PopupMessageTranslationPatch)),
                Is.EqualTo(renderedOptions[1]));
            Assert.That(
                UITextSkinTranslationPatch.TranslatePreservingColors(renderedOptions[2], nameof(PopupMessageTranslationPatch)),
                Is.EqualTo(renderedOptions[2]));
            Assert.That(
                UITextSkinTranslationPatch.TranslatePreservingColors(renderedOptions[3], nameof(PopupMessageTranslationPatch)),
                Is.EqualTo(renderedOptions[3]));
            Assert.That(
                DynamicTextObservability.GetRouteFamilyHitCountForTests(
                    nameof(PopupPickOptionTranslationPatch),
                    "Popup.ProducerText.Exact"),
                Is.GreaterThan(0));
        });
    }

    [Test]
    public void ShowConversationRoute_HandsOffTranslatedPayload_ThroughPopupMessageRebuild()
    {
        WriteDictionary(
            ("Trade", "取引"),
            ("Choose your response.", "返答を選択してください。"),
            ("Ask about water", "水について尋ねる"),
            ("Leave", "立ち去る"),
            ("[L] Look", "[L] 調べる"),
            ("[Esc] Cancel", "[Esc] キャンセル"));

        using var conversationPatch = PatchMethod(
            typeof(DummyPopupTarget),
            nameof(DummyPopupTarget.ShowConversation),
            new[]
            {
                typeof(string),
                typeof(object),
                typeof(string),
                typeof(List<string>),
                typeof(bool),
                typeof(bool),
                typeof(bool),
                typeof(bool),
            },
            typeof(PopupTranslationPatch));
        using var popupMessagePatch = PatchMethod(typeof(DummyPopupMessageTarget), nameof(DummyPopupMessageTarget.ShowPopup), typeof(PopupMessageTranslationPatch));

        DummyPopupTarget.ShowConversation(
            Title: "Trade",
            Icon: null,
            Intro: "Choose your response.",
            Options: new List<string> { "Ask about water", "{{G|Leave}}" },
            AllowTrade: false,
            AllowEscape: true,
            AllowRenderMapBehind: false,
            ForwardToPopupMessage: true);

        var renderedMessage = WrapPopupBody(DummyPopupMessageTarget.LastMessage);
        var renderedButton = DummyPopupMessageTarget.LastButtons![0].text;
        var renderedItem = DummyPopupMessageTarget.LastItems![0].text;
        UITextSkinTranslationPatch.Prefix(ref renderedMessage);
        UITextSkinTranslationPatch.Prefix(ref renderedButton);
        UITextSkinTranslationPatch.Prefix(ref renderedItem);

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupTarget.LastShowConversationTitle, Is.EqualTo("取引"));
            Assert.That(DummyPopupTarget.LastShowConversationIntro, Is.EqualTo("返答を選択してください。"));
            Assert.That(DummyPopupTarget.LastShowConversationOptions, Is.EqualTo(new[] { "水について尋ねる", "{{G|立ち去る}}" }));
            Assert.That(DummyPopupMessageTarget.LastMessage, Is.EqualTo("返答を選択してください。\n\n"));
            Assert.That(DummyPopupMessageTarget.LastTitle, Is.EqualTo("取引"));
            Assert.That(DummyPopupMessageTarget.LastButtons![0].text, Is.EqualTo("{{W|[L]}} {{y|調べる}}"));
            Assert.That(DummyPopupMessageTarget.LastButtons[1].text, Is.EqualTo("{{W|[Esc]}} {{y|キャンセル}}"));
            Assert.That(DummyPopupMessageTarget.LastItems![0].text, Is.EqualTo("{{w|[1]}} 水について尋ねる\n\n"));
            Assert.That(DummyPopupMessageTarget.LastItems[1].text, Is.EqualTo("{{w|[2]}} {{G|立ち去る}}\n\n"));
            Assert.That(renderedMessage, Is.EqualTo("{{y|返答を選択してください。\n\n}}"));
            Assert.That(renderedButton, Is.EqualTo("{{W|[L]}} {{y|調べる}}"));
            Assert.That(renderedItem, Is.EqualTo("{{w|[1]}} 水について尋ねる\n\n"));
            Assert.That(
                DynamicTextObservability.GetRouteFamilyHitCountForTests(
                    nameof(PopupTranslationPatch),
                    "Popup.ProducerText.Exact"),
                Is.GreaterThan(0));
            Assert.That(
                DynamicTextObservability.GetRouteFamilyHitCountForTests(
                    nameof(PopupMessageTranslationPatch),
                    "Popup.ProducerText.Exact"),
                Is.GreaterThan(0));
        });
    }

    [Test]
    public void PopupMessageRoute_OwnsMixedQuitFlowFields_Directly()
    {
        WriteDictionary(
            ("You can't deploy there!", "そこには設置できない！"),
            ("Quit Without Saving", "保存せず終了"),
            ("Are you sure you want to quit?", "本当に終了しますか？"),
            ("Hold to Accept", "長押しで決定"),
            ("Look", "調べる"),
            ("Show Effects", "効果を見る"),
            ("Detonate", "起爆する"),
            ("[Tab] Hold to Accept", "[Tab] 長押しで決定"),
            ("[Esc] Quit Without Saving", "[Esc] 保存せず終了"),
            ("[L] Look", "[L] 調べる"),
            ("[E] Show Effects", "[E] 効果を見る"),
            ("[N] Detonate", "[N] 起爆する"));

        using var popupMessagePatch = PatchMethod(typeof(DummyPopupMessageTarget), nameof(DummyPopupMessageTarget.ShowPopup), typeof(PopupMessageTranslationPatch));

        var buttons = new List<DummyPopupMessageItem>
        {
            new("{{W|[Tab]}} {{y|Hold to Accept}}", "Submit", "Submit"),
            new("{{W|[Esc]}} {{y|Quit Without Saving}}", "Cancel", "Cancel"),
        };
        var items = new List<DummyPopupMessageItem>
        {
            new("{{W|[L]}} {{y|Look}}", "Look", "Look"),
            new("{{W|[E]}} {{y|Show Effects}}", "ShowEffects", "ShowEffects"),
            new("{{W|[N]}} {{y|Detonate}}", "Detonate", "Detonate"),
        };

        new DummyPopupMessageTarget().ShowPopup(
            "You can't deploy there!",
            buttons,
            items: items,
            title: "Quit Without Saving",
            contextTitle: "Are you sure you want to quit?");

        var rendered = WrapPopupBody(DummyPopupMessageTarget.LastMessage);
        var sinkText = UITextSkinTranslationPatch.TranslatePreservingColors(rendered, nameof(PopupMessageTranslationPatch));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupMessageTarget.LastMessage, Is.EqualTo("そこには設置できない！"));
            Assert.That(DummyPopupMessageTarget.LastTitle, Is.EqualTo("保存せず終了"));
            Assert.That(DummyPopupMessageTarget.LastContextTitle, Is.EqualTo("本当に終了しますか？"));
            Assert.That(DummyPopupMessageTarget.LastButtons, Is.Not.Null);
            Assert.That(DummyPopupMessageTarget.LastButtons![0].text, Is.EqualTo("{{W|[Tab]}} {{y|長押しで決定}}"));
            Assert.That(DummyPopupMessageTarget.LastButtons[1].text, Is.EqualTo("{{W|[Esc]}} {{y|保存せず終了}}"));
            Assert.That(DummyPopupMessageTarget.LastItems, Is.Not.Null);
            Assert.That(DummyPopupMessageTarget.LastItems![0].text, Is.EqualTo("{{W|[L]}} {{y|調べる}}"));
            Assert.That(DummyPopupMessageTarget.LastItems[1].text, Is.EqualTo("{{W|[E]}} {{y|効果を見る}}"));
            Assert.That(DummyPopupMessageTarget.LastItems[2].text, Is.EqualTo("{{W|[N]}} {{y|起爆する}}"));
            Assert.That(sinkText, Is.EqualTo("{{y|そこには設置できない！}}"));
            Assert.That(
                DynamicTextObservability.GetRouteFamilyHitCountForTests(
                    nameof(PopupMessageTranslationPatch),
                    "Popup.ProducerText.Exact"),
                Is.GreaterThan(0));
        });
    }

    private static IDisposable PatchMethod(Type targetType, string methodName, Type patchType)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        harmony.Patch(
            original: RequireMethod(targetType, methodName),
            prefix: new HarmonyMethod(RequireMethod(patchType, "Prefix")));
        return new HarmonyPatchScope(harmony, harmonyId);
    }

    private static IDisposable PatchMethod(Type targetType, string methodName, Type[] parameterTypes, Type patchType)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        harmony.Patch(
            original: RequireMethod(targetType, methodName, parameterTypes),
            prefix: new HarmonyMethod(RequireMethod(patchType, "Prefix")));
        return new HarmonyPatchScope(harmony, harmonyId);
    }

    private static string WrapPopupBody(string text)
    {
        return "{{y|" + text + "}}";
    }

    private static string SimulateClipText(string text, int width)
    {
        if (string.IsNullOrEmpty(text) || width <= 0 || text.Length <= width)
        {
            return text;
        }

        var builder = new StringBuilder(text.Length + (text.Length / width));
        for (var index = 0; index < text.Length; index += width)
        {
            if (builder.Length > 0)
            {
                builder.Append('\n');
            }

            builder.Append(text.AsSpan(index, Math.Min(width, text.Length - index)));
        }

        return builder.ToString();
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

    private static MethodInfo RequireMethod(Type type, string methodName, Type[] parameterTypes)
    {
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
        File.WriteAllText(
            Path.Combine(dictionaryDirectory, "popup-route-handoff.ja.json"),
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

    private sealed class HarmonyPatchScope : IDisposable
    {
        private readonly Harmony harmony;
        private readonly string harmonyId;

        public HarmonyPatchScope(Harmony harmony, string harmonyId)
        {
            this.harmony = harmony;
            this.harmonyId = harmonyId;
        }

        public void Dispose()
        {
            harmony.UnpatchAll(harmonyId);
        }
    }
}
