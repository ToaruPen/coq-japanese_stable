using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;
using QudJP.Tests.L2;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class LegacyOptionsUiTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        var localizationRoot = Path.Combine(
            TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Localization");
        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(Path.Combine(localizationRoot, "Dictionaries"));
        DynamicTextObservability.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        DynamicTextObservability.ResetForTests();
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
    public void TranslateBufferText_TranslatesLiteralDictionaryEntriesThroughCoreRoute()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                LegacyOptionsUiTranslationPatch.TranslateBufferText("Would you like to save your changes?"),
                Is.EqualTo("変更を保存しますか？"));
            Assert.That(
                LegacyOptionsUiTranslationPatch.TranslateBufferText("&W<More...>"),
                Is.EqualTo("&W<続き…>"));
        });
    }

    [Test]
    public void TranslateBufferText_TranslatesRestartPromptHeaderFooterAndOptionLabels()
    {
        var restartPrompt = "These options require a game restart to take effect:\n\n"
            + "{{g|* Use Tiles}}\n"
            + "{{g|* VSync}}\n\nDo you want to do so now?";

        Assert.That(
            LegacyOptionsUiTranslationPatch.TranslateBufferText(restartPrompt),
            Is.EqualTo("これらのオプションを有効にするにはゲームの再起動が必要です:\n\n"
                + "{{g|* タイルを使用}}\n"
                + "{{g|* 垂直同期}}\n\n今すぐ再起動しますか？"));
    }

    [Test]
    public void TranslateBufferText_PreservesOuterWhitespaceAndColorPrefixes()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                LegacyOptionsUiTranslationPatch.TranslateBufferText("   VSync\t"),
                Is.EqualTo("   垂直同期\t"));
            Assert.That(
                LegacyOptionsUiTranslationPatch.TranslateBufferText("&YGeneral"),
                Is.EqualTo("&Y一般"));
            Assert.That(
                LegacyOptionsUiTranslationPatch.TranslateBufferText("    "),
                Is.EqualTo("    "));
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

        Assert.That(translated.Count(IsTranslateBufferTextCall), Is.EqualTo(2));
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
