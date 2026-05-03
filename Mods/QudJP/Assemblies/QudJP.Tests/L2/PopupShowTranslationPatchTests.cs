using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class PopupShowTranslationPatchTests
{
    private string tempDirectory = null!;
    private string patternFilePath = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-popup-show-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        patternFilePath = Path.Combine(tempDirectory, "messages.ja.json");

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        LocalizationAssetResolver.SetLocalizationRootForTests(null);
        MessagePatternTranslator.ResetForTests();
        MessagePatternTranslator.SetPatternFileForTests(patternFilePath);
        File.WriteAllText(patternFilePath, "{\"patterns\":[]}\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
        DummyPopupShow.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        LocalizationAssetResolver.SetLocalizationRootForTests(null);
        MessagePatternTranslator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void Prefix_TranslatesPopupShowMessage()
    {
        WriteDictionary(("Delete save game?", "セーブデータを削除しますか？"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.Show)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));

            DummyPopupShow.Show("Delete save game?");

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("セーブデータを削除しますか？"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesDiscoverLocationTemplate()
    {
        WriteDictionary(("You discover {0}!", "{0}を発見した！"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.Show)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));

            DummyPopupShow.Show("You discover Rust Wells!");

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("Rust Wellsを発見した！"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesDiscoverLocationTemplateWithColorWrappedTarget()
    {
        WriteDictionary(("You discover {0}!", "{0}を発見した！"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.Show)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));

            DummyPopupShow.Show("You discover {{Y|Rust Wells}}!");

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("{{Y|Rust Wells}}を発見した！"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesExaminerHiddenDiscoveryTemplate()
    {
        WriteDictionary(("You discover something about {0} that was hidden!", "{0}について隠されていたことを発見した！"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.Show)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));

            DummyPopupShow.Show("You discover something about phase cannon that was hidden!");

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("phase cannonについて隠されていたことを発見した！"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesQuestReceivedPopupWithQuestTitleExclamation()
    {
        WriteDictionary(("You have received a new quest, {0}!", "新しいクエスト「{0}」を受けた！"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.Show)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));

            DummyPopupShow.Show("You have received a new quest, {{W|O Glorious Shekhinah!}}!");

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("新しいクエスト「{{W|O Glorious Shekhinah!}}」を受けた！"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_LeavesUnknownPopupShowMessageUnchanged()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.Show)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));

            const string source = "Untranslated popup message";
            DummyPopupShow.Show(source);

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_DirectMarker_StillStripped()
    {
        WriteDictionary(("既に翻訳済み", "別訳"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.Show)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));

            DummyPopupShow.Show("\u0001既に翻訳済み");

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("既に翻訳済み"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesHistoricPopupMessagePattern()
    {
        WriteMessagePatternDictionary(("^You eat the meal\\.$", "食事をとった。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.Show)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));

            DummyPopupShow.Show("You eat the meal.");

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("食事をとった。"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(PopupShowTranslationPatch),
                        "Popup.ProducerText.Pattern"),
                    Is.GreaterThan(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesStartupJoppaIntroPattern()
    {
        LocalizationAssetResolver.SetLocalizationRootForTests(GetLocalizationRoot());
        MessagePatternTranslator.SetPatternFileForTests(null);

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        var source = "On the 10th of Iyur Ut, you arrive at the oasis-hamlet of Joppa, along the far rim of Moghra'yi, the Great Salt Desert.\n\n"
            + "All around you, moisture farmers tend to groves of viridian watervine. There are huts wrought from rock salt and brinestalk.\n\n"
            + "On the horizon, Qud's jungles strangle chrome steeples and rusted archways to the earth. Further and beyond, the fabled Spindle rises above the fray and pierces the cloud-ribboned sky.";
        var expected = "Iyur Utの10th日、あなたは大塩砂漠モグラヤイの遥かな縁にあるオアシス集落ジョッパに到着した。\n\n"
            + "あたりではウォーターヴァインの茂みを水耕農家たちが世話している。岩塩とブラインストークで組まれた小屋が建っている。\n\n"
            + "地平線では、Qudのジャングルがクロームの尖塔と錆びたアーチを大地に絡みつかせている。さらにその彼方では、伝説のスピンドルが乱景の上にそびえ、雲の帯を貫いて空へ伸びている。";

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.Show)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));

            DummyPopupShow.Show(source);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(PopupShowTranslationPatch),
                        "Popup.ProducerText.Pattern"),
                    Is.GreaterThan(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesPopupShowFailMessage()
    {
        WriteDictionary(("You are frozen solid!", "あなたは完全に凍り付いている！"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.ShowFail)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));

            DummyPopupShow.ShowFail("You are frozen solid!");

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("あなたは完全に凍り付いている！"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesPopupShowYesNoAsyncMessage()
    {
        WriteDictionary(("Are you sure you want to quit?", "本当に終了しますか？"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.ShowYesNoAsync)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));

            _ = DummyPopupShow.ShowYesNoAsync("Are you sure you want to quit?").GetAwaiter().GetResult();

            Assert.That(DummyPopupShow.LastShowYesNoAsyncMessage, Is.EqualTo("本当に終了しますか？"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesPopupShowYesNoCancelAsyncMessage()
    {
        WriteDictionary(("Delete save game?", "セーブデータを削除しますか？"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.ShowYesNoCancelAsync)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));

            _ = DummyPopupShow.ShowYesNoCancelAsync("Delete save game?").GetAwaiter().GetResult();

            Assert.That(DummyPopupShow.LastShowYesNoCancelAsyncMessage, Is.EqualTo("セーブデータを削除しますか？"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_PreservesTranslatedShowFailMessage_ThroughPopupMessageAndUITextSkin()
    {
        WriteDictionary(
            ("You do not have a missile weapon equipped!", "射撃武器を装備していない！"),
            ("[Esc] Cancel", "[Esc] キャンセル"));
        DummyPopupMessageTarget.Reset();

        var buttons = new List<DummyPopupMessageItem>
        {
            new("{{W|[Esc]}} {{y|Cancel}}", "Cancel", "Cancel"),
        };

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.ShowFail)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupMessageTarget), nameof(DummyPopupMessageTarget.ShowPopup)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupMessageTranslationPatch), nameof(PopupMessageTranslationPatch.Prefix))));

            DummyPopupShow.ShowFail("You do not have a missile weapon equipped!");
            new DummyPopupMessageTarget().ShowPopup(DummyPopupShow.LastShowMessage!, buttons);

            var renderedMessage = DummyPopupMessageTarget.LastRenderedBodyText;
            var renderedButton = DummyPopupMessageTarget.LastButtons![0].text;
            UITextSkinTranslationPatch.Prefix(ref renderedMessage);
            UITextSkinTranslationPatch.Prefix(ref renderedButton);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("射撃武器を装備していない！"));
                Assert.That(DummyPopupMessageTarget.LastMessage, Is.EqualTo("射撃武器を装備していない！"));
                Assert.That(DummyPopupMessageTarget.LastButtons![0].text, Is.EqualTo("{{W|[Esc]}} {{y|キャンセル}}"));
                Assert.That(renderedMessage, Is.EqualTo("{{y|射撃武器を装備していない！}}"));
                Assert.That(renderedButton, Is.EqualTo("{{W|[Esc]}} {{y|キャンセル}}"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

#if !HAS_GAME_DLL
    [Test]
    public void TargetMethods_ResolvesShowFamilyOverloads()
    {
        _ = typeof(Genkit.Location2D);
        _ = typeof(XRL.UI.DialogResult);

        var targetMethods = RequireMethod(typeof(PopupShowTranslationPatch), "TargetMethods");
        var resolved = ((IEnumerable<MethodBase>)targetMethods.Invoke(null, null)!).ToList();

        Assert.That(
            resolved.Any(method => method.DeclaringType?.FullName == "XRL.UI.Popup"
                && method.Name == nameof(DummyPopupShow.ShowFail)
                && string.Join("|", method.GetParameters().Select(parameter => parameter.ParameterType.FullName))
                    == "System.String|System.Boolean|System.Boolean|System.Boolean"),
            Is.True);
        Assert.That(
            resolved.Any(method => method.DeclaringType?.FullName == "XRL.UI.Popup"
                && method.Name == nameof(DummyPopupShow.ShowYesNoAsync)
                && string.Join("|", method.GetParameters().Select(parameter => parameter.ParameterType.FullName))
                    == "System.String"),
            Is.True);
        Assert.That(
            resolved.Any(method => method.DeclaringType?.FullName == "XRL.UI.Popup"
                && method.Name == nameof(DummyPopupShow.ShowYesNoCancelAsync)
                && string.Join("|", method.GetParameters().Select(parameter => parameter.ParameterType.FullName))
                    == "System.String"),
            Is.True);
    }
#endif

    private static string CreateHarmonyId()
    {
        return $"qudjp.tests.{Guid.NewGuid():N}";
    }

    private static string GetLocalizationRoot()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "Localization");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Localization directory not found from test directory: {TestContext.CurrentContext.TestDirectory}");
    }

    private static MethodInfo RequireMethod(Type type, string methodName)
    {
        return AccessTools.Method(type, methodName)
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

        var path = Path.Combine(tempDirectory, "popup-show.ja.json");
        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private void WriteMessagePatternDictionary(params (string pattern, string template)[] patterns)
    {
        var builder = new StringBuilder();
        builder.Append("{\"patterns\":[");

        for (var index = 0; index < patterns.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append("{\"pattern\":\"");
            builder.Append(EscapeJson(patterns[index].pattern));
            builder.Append("\",\"template\":\"");
            builder.Append(EscapeJson(patterns[index].template));
            builder.Append("\"}");
        }

        builder.Append("]}");
        builder.AppendLine();

        File.WriteAllText(patternFilePath, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
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
