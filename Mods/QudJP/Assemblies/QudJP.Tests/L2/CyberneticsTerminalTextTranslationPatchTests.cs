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
            ("Your curiosity is admirable, aristocrat.\n\nCybernetics are bionic augmentations implanted in your body to assist in your self-actualization. You can have implants installed at becoming nooks such as this one. Either load them in the rack or carry them on your person.", "その好奇心は見事である、貴顕よ。\n\nサイバネティクスとは、自己実現を助けるために肉体へ埋め込む生体改造である。このような変容の窪みで装着できる。ラックに載せるか、自ら携えるがよい。"),
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
            ("Welcome, Aristocrat, to a becoming nook. {0} one step closer to the Grand Unification. Please choose from the following options.", "ようこそ、貴顕よ、変容の窪みへ。{0}は大統一へまた一歩近づいた。以下の選択肢から選ぶがよい。"),
            ("[{0} license points]", "[{0} ライセンスポイント]"),
            (" [will replace {0}]", " [{0} を置き換える]"));

        RunWithCyberneticsTerminalTextTranspiler(() =>
        {
            var screen = new DummyDynamicCyberneticsScreen();
            screen.Update();

            Assert.Multiple(() =>
            {
                Assert.That(screen.MainText, Is.EqualTo("ようこそ、貴顕よ、変容の窪みへ。you areは大統一へまた一歩近づいた。以下の選択肢から選ぶがよい。"));
                Assert.That(screen.Options[0], Is.EqualTo("Night Vision Goggles {{C|[3 ライセンスポイント]}}"));
                Assert.That(screen.Options[1], Is.EqualTo("Optic Chisel [Night Vision Goggles を置き換える]"));
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
