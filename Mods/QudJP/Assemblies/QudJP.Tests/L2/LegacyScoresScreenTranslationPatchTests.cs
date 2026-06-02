using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using HarmonyLib;
using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class LegacyScoresScreenTranslationPatchTests
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
        DummyLegacyScoresScreen.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
    }

    [Test]
    public void Show_TranslatesLegacyScoreScreenLiteralsAndGameSummaryLines_WhenPatched()
    {
        WithPatchedShow(() =>
        {
            var screen = new DummyLegacyScoresScreen();

            screen.Show(hasScores: true, gameMode: "Classic");

            Assert.Multiple(() =>
            {
                Assert.That(screen.Buffer.Writes, Does.Contain("&Y>終了した冒険"));
                Assert.That(screen.Buffer.Writes, Does.Contain(" &yデイリー"));
                Assert.That(screen.Buffer.Writes, Does.Contain("&Y>デイリー (フレンド)"));
                Assert.That(screen.Buffer.Writes, Does.Contain("このゲームはクラシックモードでプレイされた。"));
                Assert.That(screen.Buffer.Writes, Does.Contain("<続き…>"));
                Assert.That(screen.Buffer.Writes, Does.Contain("&Y[&WR&y - エピローグ再訪&Y] &Y[&WD / Del&y - 削除&Y]"));
                Assert.That(screen.Buffer.Writes, Does.Contain("ページ 2 / 5"));
                Assert.That(screen.Buffer.Writes, Does.Contain("&WDown&y-次のページ &WUp&y-前のページ"));
                Assert.That(screen.Buffer.Writes, Does.Contain("&W7&y-前のボード &W9&y-次のボード"));
                Assert.That(screen.Buffer.Writes, Does.Contain("このゲームはロールプレイモードでプレイされた。"));
                Assert.That(HitCount(), Is.GreaterThanOrEqualTo(10));
            });
        });
    }

    [Test]
    public void Show_TranslatesNoScoresAndConnectionState_WhenPatched()
    {
        WithPatchedShow(() =>
        {
            var screen = new DummyLegacyScoresScreen();

            screen.Show(hasScores: false, gameMode: "Classic");

            Assert.Multiple(() =>
            {
                Assert.That(screen.Buffer.Writes, Does.Contain("ハイスコアはありません！"));
                Assert.That(screen.Buffer.Writes, Does.Contain("スコアを読み込み中…"));
                Assert.That(screen.Buffer.Writes, Does.Contain("<プロバイダーに接続されていません>"));
            });
        });
    }

    [Test]
    public void Show_StripsDirectMarkerAndLeavesUnknownText_WhenPatched()
    {
        DummyLegacyScoresScreen.ExtraWrites.Add(MessageFrameTranslator.MarkDirectTranslation("既訳スコア"));
        DummyLegacyScoresScreen.ExtraWrites.Add("Unmapped score text");

        WithPatchedShow(() =>
        {
            var screen = new DummyLegacyScoresScreen();

            screen.Show(hasScores: true, gameMode: "Classic");

            Assert.Multiple(() =>
            {
                Assert.That(screen.Buffer.Writes, Does.Contain("既訳スコア"));
                Assert.That(screen.Buffer.Writes, Does.Contain("Unmapped score text"));
            });
        });
    }

    private static void WithPatchedShow(Action action)
    {
        var harmonyId = "qudjp.tests.legacy-scores-screen." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(
                    typeof(DummyLegacyScoresScreen),
                    nameof(DummyLegacyScoresScreen.Show),
                    typeof(bool),
                    typeof(string)),
                transpiler: new HarmonyMethod(RequireMethod(
                    typeof(LegacyScoresScreenTranslationPatch),
                    nameof(LegacyScoresScreenTranslationPatch.Transpiler),
                    typeof(IEnumerable<CodeInstruction>))));
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static int HitCount()
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            LegacyScoresScreenTranslationPatch.Context,
            LegacyScoresScreenTranslationPatch.Family);
    }

    private static MethodInfo RequireMethod(Type type, string name, params Type[] parameterTypes)
    {
        var method = AccessTools.Method(type, name, parameterTypes);
        Assert.That(method, Is.Not.Null, $"{type.FullName}.{name} not found");
        return method!;
    }
}

internal sealed class DummyLegacyScoresScreen
{
    public DummyLegacyScoreBuffer Buffer { get; } = new();

    public static void Reset()
    {
        ExtraWrites.Clear();
    }

    public static List<string> ExtraWrites { get; } = [];

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Show(bool hasScores, string gameMode)
    {
        Buffer.Write("&Y>Local Scores");
        Buffer.Write(" &yDaily");
        Buffer.Write("&Y>Daily (friends)");

        if (!hasScores)
        {
            Buffer.Write("No high scores!");
            Buffer.Write("loading scores...");
            Buffer.Write("<not connected to provider>");
            return;
        }

        var builder = new StringBuilder();
        builder.Append("This game was played in ");
        builder.Append(gameMode);
        builder.Append(" mode.");
        Buffer.Write(builder);
        Buffer.Write("<more...>");
        Buffer.Write("&Y[&WR&y - Revisit Epilogue&Y] &Y[&WD / Del&y - Delete&Y]");
        Buffer.Write("Page " + 2 + " of " + 5);
        Buffer.Write("&WDown&y-next page &WUp&y-previous page");
        Buffer.Write("&W7&y-previous board &W9&y-next board");
        Buffer.Write("This game was played in Roleplay mode.");
        foreach (var write in ExtraWrites)
        {
            Buffer.Write(write);
        }
    }
}

internal sealed class DummyLegacyScoreBuffer
{
    public List<string> Writes { get; } = [];

    public void Write(string text)
    {
        Writes.Add(text);
    }

    public void Write(StringBuilder text)
    {
        Writes.Add(text.ToString());
    }
}
