using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class LegacyOptionsUiTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        var localizationRoot = Path.Combine(
            QudJP.Tests.L1.TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Localization");
        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(Path.Combine(localizationRoot, "Dictionaries"));
        DynamicTextObservability.ResetForTests();
        DummyPopupShow.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        DummyPopupShow.Reset();
    }

    [Test]
    public void Show_TranslatesLegacyOptionsChromeAndValues_WhenPatched()
    {
        WithPatchedShow(() =>
        {
            var screen = new DummyLegacyOptionsUiScreen();

            screen.Show();

            Assert.Multiple(() =>
            {
                Assert.That(screen.Buffer.Writes, Does.Contain("[ &wゲームオプション&y ]"));
                Assert.That(screen.Buffer.Writes, Does.Contain(" &WESC&y - 終了 "));
                Assert.That(screen.Buffer.Writes, Does.Contain("&Cゲームプレイ&y"));
                Assert.That(screen.Buffer.Writes, Does.Contain("&Y一般"));
                Assert.That(screen.Buffer.Writes, Does.Contain("&K無制限"));
                Assert.That(screen.Buffer.Writes, Does.Contain("  &W垂直同期  "));
                Assert.That(screen.Buffer.Writes, Does.Contain("&W<< &K[続き]  "));
                Assert.That(screen.Buffer.Writes, Does.Contain(" [&WSpace&y-オプション変更] "));
                Assert.That(screen.Buffer.Writes, Does.Contain("&W<続き…>"));
            });
        });
    }

    [Test]
    public void Show_TranslatesRestartPrompt_WhenPatched()
    {
        WithPatchedShow(() =>
        {
            var screen = new DummyLegacyOptionsUiScreen();

            screen.Show();

            Assert.That(
                DummyPopupShow.LastShowYesNoMessage,
                Is.EqualTo("これらのオプションを有効にするにはゲームの再起動が必要です:\n\n{{g|* タイルを使用}}\n\n今すぐ再起動しますか？"));
        });
    }

    [Test]
    public void TranslateBufferText_StripsDirectMarkerAndLeavesUnknownText()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                LegacyOptionsUiTranslationPatch.TranslateBufferText(
                    MessageFrameTranslator.DirectTranslationMarker + "Game Options"),
                Is.EqualTo("Game Options"));
            Assert.That(
                LegacyOptionsUiTranslationPatch.TranslateBufferText("Unmapped option text"),
                Is.EqualTo("Unmapped option text"));
        });
    }

    [Test]
    public void TranslateBufferText_TranslatesLiteralRestartPromptWhitespaceAndColorTerms()
    {
        var restartPrompt = "These options require a game restart to take effect:\n\n"
            + "{{g|* Use Tiles}}\n\nDo you want to do so now?";

        Assert.Multiple(() =>
        {
            Assert.That(
                LegacyOptionsUiTranslationPatch.TranslateBufferText("[ &wGame Options&y ]"),
                Is.EqualTo("[ &wゲームオプション&y ]"));
            Assert.That(
                LegacyOptionsUiTranslationPatch.TranslateBufferText(restartPrompt),
                Is.EqualTo("これらのオプションを有効にするにはゲームの再起動が必要です:\n\n{{g|* タイルを使用}}\n\n今すぐ再起動しますか？"));
            Assert.That(
                LegacyOptionsUiTranslationPatch.TranslateBufferText("  &WVSync  "),
                Is.EqualTo("  &W垂直同期  "));
            Assert.That(
                LegacyOptionsUiTranslationPatch.TranslateBufferText("&CGameplay&y"),
                Is.EqualTo("&Cゲームプレイ&y"));
        });
    }

    [Test]
    public void TranslateBufferText_StripsDirectMarkerWithoutRecordingTransform()
    {
        var result = LegacyOptionsUiTranslationPatch.TranslateBufferText(
            MessageFrameTranslator.MarkDirectTranslation("既に翻訳済みオプション"));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo("既に翻訳済みオプション"));
            Assert.That(
                DynamicTextObservability.GetRouteFamilyHitCountForTests(
                    LegacyOptionsUiTranslationPatch.Context,
                    LegacyOptionsUiTranslationPatch.Family),
                Is.Zero);
            });
    }

    [Test]
    public void TranslateBufferText_LeavesEmptyInputWithoutRecordingTransform()
    {
        var result = LegacyOptionsUiTranslationPatch.TranslateBufferText(string.Empty);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Empty);
            Assert.That(
                DynamicTextObservability.GetRouteFamilyHitCountForTests(
                    LegacyOptionsUiTranslationPatch.Context,
                    LegacyOptionsUiTranslationPatch.Family),
                Is.Zero);
        });
    }

    [Test]
    public void Transpiler_RoutesLiteralsWriteCallsAndToStringThroughTranslateBufferText()
    {
        var writeMethod = RequireMethod(typeof(DummyLegacyOptionsBuffer), nameof(DummyLegacyOptionsBuffer.Write), typeof(string));
        var toStringMethod = RequireMethod(typeof(StringBuilder), nameof(StringBuilder.ToString), Type.EmptyTypes);
        var instructions = new[]
        {
            new CodeInstruction(OpCodes.Ldstr, "[ &wGame Options&y ]"),
            new CodeInstruction(OpCodes.Callvirt, writeMethod),
            new CodeInstruction(OpCodes.Callvirt, toStringMethod),
        };

        var translated = LegacyOptionsUiTranslationPatch.Transpiler(instructions).ToList();

        Assert.That(translated.Count(IsTranslateBufferTextCall), Is.EqualTo(3));
    }

    private static void WithPatchedShow(Action action)
    {
        var harmonyId = "qudjp.tests.legacy-options-ui." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyLegacyOptionsUiScreen), nameof(DummyLegacyOptionsUiScreen.Show)),
                transpiler: new HarmonyMethod(RequireMethod(
                    typeof(LegacyOptionsUiTranslationPatch),
                    nameof(LegacyOptionsUiTranslationPatch.Transpiler),
                    typeof(IEnumerable<CodeInstruction>))));
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static MethodInfo RequireMethod(Type type, string name, params Type[] parameterTypes)
    {
        var method = AccessTools.Method(type, name, parameterTypes);
        Assert.That(method, Is.Not.Null, $"{type.FullName}.{name} not found");
        return method!;
    }

    private static bool IsTranslateBufferTextCall(CodeInstruction instruction)
    {
        return instruction.opcode == OpCodes.Call
            && instruction.operand is MethodInfo method
            && method.DeclaringType == typeof(LegacyOptionsUiTranslationPatch)
            && method.Name == nameof(LegacyOptionsUiTranslationPatch.TranslateBufferText);
    }
}

internal sealed class DummyLegacyOptionsUiScreen
{
    public DummyLegacyOptionsBuffer Buffer { get; } = new();

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Show()
    {
        Buffer.Write("[ &wGame Options&y ]");
        Buffer.Write(" &WESC&y - Exit ");
        Buffer.Write("&C" + "Gameplay" + "&y");
        Buffer.Write("&Y" + "General");
        Buffer.Write("&K" + "Unlimited");
        Buffer.Write("  &W" + "VSync" + "  ");
        Buffer.Write("&W<< &K[more]  ");
        Buffer.Write(" [&WSpace&y-change option] ");
        Buffer.Write("&W<More...>");

        var restartPrompt = new StringBuilder();
        restartPrompt.Append("These options require a game restart to take effect:\n");
        restartPrompt.Append('\n');
        restartPrompt.Append("{{g|");
        restartPrompt.Append("* ");
        restartPrompt.Append("Use Tiles");
        restartPrompt.Append("}}");
        restartPrompt.Append("\n\nDo you want to do so now?");
        _ = DummyPopupShow.ShowYesNo(restartPrompt.ToString());
    }
}

internal sealed class DummyLegacyOptionsBuffer
{
    public List<string> Writes { get; } = [];

    public void Write(string text)
    {
        Writes.Add(text);
    }
}
