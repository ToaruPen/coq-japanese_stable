using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

public sealed partial class Issue201OtherUiBindingPatchTests
{
    [Test]
    public void CyberneticsTerminalTextTranspiler_TranslatesConstructorInitializedScreen()
    {
        WriteCyberneticsDictionary(
            ("Your curiosity is admirable, aristocrat.\n\nCybernetics are bionic augmentations implanted in your body to assist in your self-actualization. You can have implants installed at becoming nooks such as this one. Either load them in the rack or carry them on your person.", "その好奇心は見事である、貴顕よ。\n\nサイバネティクスとは、自己実現を助けるために肉体へ埋め込む生体改造である。このような変容の僻隅で装着できる。ラックに載せるか、自ら携えるがよい。"),
            ("How many implants can I install?", "インプラントは何個まで装着できますか?"),
            ("Return To Main Menu", "メインメニューに戻る"));

        RunWithCyberneticsTerminalTextTranspiler(() =>
        {
            var screen = new DummyConstructorCyberneticsScreen();
            screen.Update();

            Assert.Multiple(() =>
            {
                Assert.That(screen.MainText, Does.StartWith("その好奇心は見事である、貴顕よ。"));
                Assert.That(screen.Options[0], Is.EqualTo("インプラントは何個まで装着できますか?"));
                Assert.That(screen.Options[1], Is.EqualTo("メインメニューに戻る"));
                Assert.That(screen.RenderedText, Does.Contain("インプラントは何個まで装着できますか?"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(CyberneticsTerminalTextTranslator), "CyberneticsTerminal.MainText"),
                    Is.GreaterThan(0));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(CyberneticsTerminalTextTranslator), "CyberneticsTerminal.OptionText"),
                    Is.GreaterThan(0));
            });
        });
    }

    [Test]
    public void CyberneticsTerminalTextTranspiler_TranslatesOnUpdateInitializedScreen()
    {
        WriteCyberneticsDictionary(
            ("You are becoming, aristocrat. Choose an implant to install.", "変容しつつあるな、貴顕よ。装着するインプラントを選ぶがよい。"),
            ("Install Cybernetics", "サイバネティクスを装着"),
            ("Return to main menu", "メインメニューに戻る"));

        RunWithCyberneticsTerminalTextTranspiler(() =>
        {
            var screen = new DummyOnUpdateCyberneticsScreen();
            screen.Update();

            Assert.Multiple(() =>
            {
                Assert.That(screen.MainText, Is.EqualTo("変容しつつあるな、貴顕よ。装着するインプラントを選ぶがよい。"));
                Assert.That(screen.Options[0], Is.EqualTo("サイバネティクスを装着"));
                Assert.That(screen.Options[1], Is.EqualTo("メインメニューに戻る"));
                Assert.That(screen.RenderedText, Does.Contain("変容しつつあるな、貴顕よ。装着するインプラントを選ぶがよい。"));
            });
        });
    }

    [Test]
    public void CyberneticsTerminalTextTranspiler_ResolvesDynamicTemplates()
    {
        WriteCyberneticsDictionary(
            ("Welcome, Aristocrat, to a becoming nook. {0} one step closer to the Grand Unification. Please choose from the following options.", "ようこそ、貴顕よ、変容の僻隅へ。{0}は大統一へまた一歩近づいた。以下の選択肢から選ぶがよい。"),
            ("[{0} license points]", "[{0} ライセンスポイント]"),
            (" [will replace {0}]", " [{0}を置き換える]"),
            ("Night Vision Goggles", "暗視ゴーグル"),
            ("Optic Chisel", "視神経チゼル"));

        RunWithCyberneticsTerminalTextTranspiler(() =>
        {
            var screen = new DummyDynamicCyberneticsScreen();
            screen.Update();

            Assert.Multiple(() =>
            {
                Assert.That(screen.MainText, Is.EqualTo("ようこそ、貴顕よ、変容の僻隅へ。あなたは大統一へまた一歩近づいた。以下の選択肢から選ぶがよい。"));
                Assert.That(screen.Options[0], Is.EqualTo("暗視ゴーグル {{C|[3 ライセンスポイント]}}"));
                Assert.That(screen.Options[1], Is.EqualTo("視神経チゼル [暗視ゴーグルを置き換える]"));
            });
        });
    }

    [Test]
    public void CyberneticsTerminalTextTranspiler_TranslatesInstallOptionDisplayNames()
    {
        WriteCyberneticsDictionary(
            ("You are becoming, aristocrat. Choose an implant to install.", "変容しつつあるな、貴顕よ。装着するインプラントを選ぶがよい。"),
            ("[{0} license points]", "[{0} ライセンスポイント]"),
            ("[already installed]", "[装着済み]"),
            ("Night Vision Goggles", "暗視ゴーグル"),
            ("Optic Chisel", "視神経チゼル"));

        RunWithCyberneticsTerminalTextTranspiler(() =>
        {
            var screen = new CyberneticsScreen();
            screen.MainText = "You are becoming, aristocrat. Choose an implant to install.";
            screen.Options.Add("Night Vision Goggles {{C|[3 license points]}}");
            screen.Options.Add("Optic Chisel [already installed]");
            screen.Update();

            Assert.Multiple(() =>
            {
                Assert.That(screen.MainText, Is.EqualTo("変容しつつあるな、貴顕よ。装着するインプラントを選ぶがよい。"));
                Assert.That(screen.Options[0], Is.EqualTo("暗視ゴーグル {{C|[3 ライセンスポイント]}}"));
                Assert.That(screen.Options[1], Is.EqualTo("視神経チゼル [装着済み]"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(CyberneticsTerminalTextTranslator), "CyberneticsTerminal.OptionText"),
                    Is.GreaterThan(0));
            });
        });
    }

    [Test]
    public void CyberneticsTerminalTextTranspiler_StripsDirectMarkedVisibleOptionText()
    {
        RunWithCyberneticsTerminalTextTranspiler(() =>
        {
            var screen = new CyberneticsScreen();
            screen.Options.Add(MessageFrameTranslator.MarkDirectTranslation("既に翻訳済みオプション"));
            screen.Update();

            Assert.That(screen.Options[0], Is.EqualTo("既に翻訳済みオプション"));
        });
    }

    [Test]
    public void CyberneticsTerminalTextTranspiler_TranslatesBodySlotSuffixes()
    {
        WriteCyberneticsDictionary(("translucent skin", "透明皮膚"));

        RunWithCyberneticsTerminalTextTranspiler(() =>
        {
            var screen = new CyberneticsScreen();
            screen.MainText = "Please choose a target body part.";
            screen.Options.Add("{{Y|皮膚用断熱材}} (Back)");
            screen.Options.Add("translucent skin (Back)");
            screen.Options.Add("translucent skin (Hand)");
            screen.Options.Add("translucent skin (Tail)");
            screen.Options.Add("made-up cyberware (Back)");
            screen.Options.Add("Back");
            screen.Update();

            Assert.Multiple(() =>
            {
                Assert.That(screen.Options[0], Is.EqualTo("{{Y|皮膚用断熱材}}（背中）"));
                Assert.That(screen.Options[1], Is.EqualTo("透明皮膚（背中）"));
                Assert.That(screen.Options[2], Is.EqualTo("透明皮膚（手）"));
                Assert.That(screen.Options[3], Is.EqualTo("透明皮膚（尾）"));
                Assert.That(screen.Options[4], Is.EqualTo("made-up cyberware (Back)"));
                Assert.That(screen.Options[5], Is.EqualTo("Back"));
                Assert.That(screen.RenderedText, Does.Contain("{{Y|皮膚用断熱材}}（背中）"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(CyberneticsTerminalTextTranslator), "CyberneticsTerminal.OptionText"),
                    Is.GreaterThan(0));
            });
        });
    }

    [Test]
    public void CyberneticsTerminalTextTranspiler_LeavesNonCyberneticsScreenUntouched()
    {
        WriteCyberneticsDictionary(
            ("You are becoming, aristocrat. Choose an implant to install.", "変容しつつあるな、貴顕よ。装着するインプラントを選ぶがよい。"),
            ("Install Cybernetics", "サイバネティクスを装着"));

        RunWithCyberneticsTerminalTextTranspiler(() =>
        {
            var screen = new DummyNonCyberneticsScreen();
            screen.Update();

            Assert.Multiple(() =>
            {
                Assert.That(screen.MainText, Is.EqualTo("You are becoming, aristocrat. Choose an implant to install."));
                Assert.That(screen.Options[0], Is.EqualTo("Install Cybernetics"));
                Assert.That(screen.RenderedText, Does.Contain("Install Cybernetics"));
            });
        });
    }

    private static void RunWithCyberneticsTerminalTextTranspiler(Action assertion)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyTerminalScreen), nameof(DummyTerminalScreen.Update)),
                transpiler: new HarmonyMethod(RequireMethod(typeof(CyberneticsTerminalTextTranslationPatch), nameof(CyberneticsTerminalTextTranslationPatch.Transpiler))));

            assertion();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private void WriteCyberneticsDictionary(params (string key, string text)[] entries)
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
            builder.Append(EscapeJsonWithNewlines(entries[index].key));
            builder.Append("\",\"text\":\"");
            builder.Append(EscapeJsonWithNewlines(entries[index].text));
            builder.Append("\"}");
        }

        builder.Append("]}");
        builder.AppendLine();
        File.WriteAllText(
            Path.Combine(tempDirectory, "cybernetics-terminal-text-l2.ja.json"),
            builder.ToString(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string EscapeJsonWithNewlines(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);
    }
}
