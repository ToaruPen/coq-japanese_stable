using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class XrlCoreRestoreModsLoadedTranslationPatchTests
{
    [Test]
    public async Task Transpiler_TranslatesRestoreModsLoadedPopupFrames_WhilePreservingModTitles()
    {
        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);
        try
        {
            var target = ResolveStateMachineMoveNext(RequireMethod(
                    typeof(DummyXrlCoreRestoreModsLoadedTarget),
                    nameof(DummyXrlCoreRestoreModsLoadedTarget.RestoreModsLoadedAsync)))
                ?? throw new InvalidOperationException("Dummy RestoreModsLoadedAsync state machine MoveNext not found.");
            harmony.Patch(
                original: target,
                transpiler: new HarmonyMethod(RequireMethod(
                    typeof(XrlCoreRestoreModsLoadedTranslationPatch),
                    nameof(XrlCoreRestoreModsLoadedTranslationPatch.Transpiler))));

            var result = await new DummyXrlCoreRestoreModsLoadedTarget().RestoreModsLoadedAsync().ConfigureAwait(false);

            Assert.Multiple(() =>
            {
                Assert.That(result.IncompleteTitle, Is.EqualTo("不完全なMod構成"));
                Assert.That(
                    result.UnavailableMessage,
                    Is.EqualTo("このセーブで有効な1つ以上のModが{{red|利用できません}}:{{red|Sample Mod}}このセーブを読み込んでみますか？"));
                Assert.That(result.DiffersTitle, Is.EqualTo("Mod構成が異なります"));
                Assert.That(
                    result.DiffersMessage,
                    Is.EqualTo("このセーブでは{{red|無効}}になっているMod:{{red|Extra Mod}}\nこのセーブでは{{green|有効}}になっているMod:{{green|Missing Mod}}"));
                Assert.That(result.Options, Is.EqualTo(new[]
                {
                    "セーブのMod構成で再起動",
                    "現在のMod構成のまま読み込む",
                }));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void TranslateLiteralForTests_LeavesUnknownLiteralUnchanged()
    {
        Assert.That(
            XrlCoreRestoreModsLoadedTranslationPatch.TranslateLiteralForTests("unrelated"),
            Is.EqualTo("unrelated"));
    }

    [Test]
    public void TranslateLiteralForTests_HandlesEmptyColorAndDirectMarkedLiterals()
    {
        Assert.Multiple(() =>
        {
            Assert.That(XrlCoreRestoreModsLoadedTranslationPatch.TranslateLiteralForTests(string.Empty), Is.Empty);
            Assert.That(
                XrlCoreRestoreModsLoadedTranslationPatch.TranslateLiteralForTests("{{red|Incomplete Mod Configuration}}"),
                Is.EqualTo("{{red|不完全なMod構成}}"));
            Assert.That(
                XrlCoreRestoreModsLoadedTranslationPatch.TranslateLiteralForTests(
                    MessageFrameTranslator.MarkDirectTranslation("Mod Configuration Differs")),
                Is.EqualTo("Mod Configuration Differs"));
        });
    }

    private static MethodInfo RequireMethod(Type type, string methodName)
    {
        return AccessTools.Method(type, methodName)
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private static MethodInfo? ResolveStateMachineMoveNext(MethodInfo sourceMethod)
    {
        var asyncStateMachine = sourceMethod.GetCustomAttribute<AsyncStateMachineAttribute>();
        return asyncStateMachine?.StateMachineType is null
            ? null
            : AccessTools.Method(asyncStateMachine.StateMachineType, "MoveNext");
    }
}
