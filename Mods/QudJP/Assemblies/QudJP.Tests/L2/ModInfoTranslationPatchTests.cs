using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class ModInfoTranslationPatchTests
{
    [Test]
    public void Transpiler_TranslatesDependencyUpdateAndProgressLiterals_WhenPatched()
    {
        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);
        try
        {
            var transpiler = new HarmonyMethod(RequireMethod(typeof(ModInfoTranslationPatch), nameof(ModInfoTranslationPatch.Transpiler)));
            harmony.Patch(
                original: RequireMethod(typeof(DummyModInfoTarget), nameof(DummyModInfoTarget.ConfirmDependencies)),
                transpiler: transpiler);
            harmony.Patch(
                original: RequireMethod(typeof(DummyModInfoTarget), nameof(DummyModInfoTarget.ConfirmUpdate)),
                transpiler: transpiler);
            harmony.Patch(
                original: RequireMethod(typeof(DummyModInfoTarget), nameof(DummyModInfoTarget.ConfirmFailure)),
                transpiler: transpiler);
            harmony.Patch(
                original: RequireMethod(typeof(DummyModInfoTarget), nameof(DummyModInfoTarget.DownloadUpdate)),
                transpiler: transpiler);
            harmony.Patch(
                original: RequireMethod(typeof(DummyModInfoTarget), nameof(DummyModInfoTarget.AppendDependencyConfirmation)),
                transpiler: transpiler);

            var target = new DummyModInfoTarget();
            target.ConfirmDependencies();
            target.ConfirmUpdate();
            target.ConfirmFailure();
            _ = target.DownloadUpdate();

            Assert.Multiple(() =>
            {
                Assert.That(target.LastDependencyPopupTitle, Is.EqualTo("{{W|依存関係}}"));
                Assert.That(target.LastUpdatePopupTitle, Is.EqualTo("{{W|更新あり}}"));
                Assert.That(
                    target.LastUpdatePopupMessage,
                    Is.EqualTo("Sample Modの新しいバージョンが利用可能です: 2.0.5.\n\nダウンロードしますか？"));
                Assert.That(target.LastFailurePopupTitle, Is.EqualTo("Sample Mod - {{R|エラー}}"));
                Assert.That(
                    target.LastFailurePopupMessage,
                    Is.EqualTo("first error\nsecond error\nthird error\n(... {{R|+2}} 件追加)\n\n必要なら転送できるよう、自動的にクリップボードへコピーされています: Mod作者。"));
                Assert.That(target.LastRetryText, Is.EqualTo("{{W|[R]}} {{y|再試行}}"));
                Assert.That(target.LastWorkshopText, Is.EqualTo("{{W|[W]}} {{y|ワークショップ}}"));
                Assert.That(target.LastLoadingText, Is.EqualTo("Sample Modを更新中…"));
                Assert.That(target.AppendDependencyConfirmation(0), Is.EqualTo("無効"));
                Assert.That(target.AppendDependencyConfirmation(1), Is.EqualTo("OK"));
                Assert.That(target.AppendDependencyConfirmation(2), Is.EqualTo("バージョン不一致"));
                Assert.That(target.AppendDependencyConfirmation(3), Is.EqualTo("未検出"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static MethodInfo RequireMethod(Type type, string methodName)
    {
        return AccessTools.Method(type, methodName)
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }
}
