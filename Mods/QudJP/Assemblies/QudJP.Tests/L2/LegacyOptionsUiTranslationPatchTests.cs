using System.Reflection;
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
