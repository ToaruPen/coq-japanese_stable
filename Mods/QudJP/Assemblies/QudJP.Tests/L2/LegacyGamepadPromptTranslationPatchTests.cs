using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class LegacyGamepadPromptTranslationPatchTests
{
    [Test]
    public void XrlManualTranspiler_TranslatesRenderedHelpPrompts()
    {
        RunWithPatch(
            typeof(DummyXrlManualTarget),
            nameof(DummyXrlManualTarget.RenderIndex),
            typeof(XrlManualTranslationPatch),
            () =>
            {
                var target = new DummyXrlManualTarget();
                target.RenderIndex(0);

                Assert.That(target.Buffer.Writes, Is.EqualTo(new[]
                {
                    " [{{W|A}}] トピックを選択 ",
                    " [{{W|B}}] ヘルプを終了 ",
                }));
            });
    }

    [Test]
    public void InventoryScreenTranspiler_TranslatesFooterAndFilterPrompts()
    {
        RunWithPatch(
            typeof(DummyInventoryScreenTarget),
            nameof(DummyInventoryScreenTarget.Show),
            typeof(InventoryScreenTranslationPatch),
            () =>
            {
                var target = new DummyInventoryScreenTarget();
                target.Show();

                Assert.Multiple(() =>
                {
                    Assert.That(
                        target.FooterLength,
                        Is.EqualTo(DummyLegacyMarkup.StripFormatting("< {{W|LB}} キャラクター | 装備 {{W|RB}} >").Length));
                    Assert.That(target.Buffer.Writes, Does.Contain(" {{W|B}} 終了 "));
                    Assert.That(target.Buffer.Writes, Does.Contain("< {{W|LB}} キャラクター | 装備 {{W|RB}} >"));
                    Assert.That(target.Buffer.Writes, Does.Contain("<続き…>"));
                    Assert.That(target.Buffer.Writes, Does.Contain("<…前へ>"));
                    Assert.That(target.Buffer.Writes, Does.Contain("フィルターにより5個のアイテムが非表示"));
                });
            });
    }

    [Test]
    public void StatusScreenTranspiler_TranslatesFooterAndMutationPrompts()
    {
        RunWithPatch(
            typeof(DummyStatusScreenTarget),
            nameof(DummyStatusScreenTarget.Show),
            typeof(StatusScreenTranslationPatch),
            () =>
            {
                var target = new DummyStatusScreenTarget();
                target.Show();

                Assert.Multiple(() =>
                {
                    Assert.That(target.Buffer.Writes, Does.Contain(" {{W|B}} 終了 "));
                    Assert.That(target.Buffer.Writes, Does.Contain("< {{W|LB}} スキル | インベントリ {{W|RB}} >"));
                    Assert.That(target.Buffer.Writes, Does.Contain(" [{{W|A}}] 上昇"));
                    Assert.That(target.Buffer.Writes, Does.Contain("新しいランダムなmutationを4 MPで購入"));
                });
            });
    }

    [Test]
    public void JournalScreenTranspiler_TranslatesFooterAndActionPrompts()
    {
        RunWithPatch(
            typeof(DummyJournalScreenTarget),
            nameof(DummyJournalScreenTarget.Show),
            typeof(JournalScreenTranslationPatch),
            () =>
            {
                var target = new DummyJournalScreenTarget();
                target.Show();

                Assert.Multiple(() =>
                {
                    Assert.That(target.Buffer.Writes, Does.Contain(" {{W|B}} 終了 "));
                    Assert.That(target.Buffer.Writes, Does.Contain("< {{W|LB}} クエスト | ティンカリング {{W|RB}} >"));
                    Assert.That(target.Buffer.Writes, Does.Contain(" {{W|X}} - 削除 "));
                    Assert.That(target.Buffer.Writes, Does.Contain(" {{W|Y}} 追加 {{W|X}} - 削除 "));
                });
            });
    }

    [Test]
    public void TinkeringScreenTranspiler_TranslatesFooterAndBottomPrompts()
    {
        RunWithPatch(
            typeof(DummyTinkeringScreenTarget),
            nameof(DummyTinkeringScreenTarget.Show),
            new[] { typeof(object), typeof(object), typeof(object) },
            typeof(TinkeringScreenTranslationPatch),
            () =>
            {
                var target = new DummyTinkeringScreenTarget();
                target.Show(new object());

                Assert.Multiple(() =>
                {
                    Assert.That(target.Buffer.Writes, Does.Contain("{{Y|>}} {{W|製作}}    {{w|改造}}"));
                    Assert.That(target.Buffer.Writes, Does.Contain(" {{W|A}} アイテム改造  {{W|Y}} 改造一覧  {{W|B}} 終了 "));
                    Assert.That(target.Buffer.Writes, Does.Contain(" {{W|A}} 製作  {{W|RT}}/{{W|LT}} スクロール  {{W|B}} 終了 "));
                    Assert.That(target.Buffer.Writes, Does.Contain("< {{W|LB}} ジャーナル | スキル {{W|RB}} >"));
                });
            });
    }

    [Test]
    public void QuestLogTranspiler_TranslatesFooterPrompt()
    {
        RunWithPatch(
            typeof(DummyLegacyQuestLogScreenTarget),
            nameof(DummyLegacyQuestLogScreenTarget.Show),
            typeof(QuestLogGamepadPromptTranslationPatch),
            () =>
            {
                var target = new DummyLegacyQuestLogScreenTarget();
                target.Show();

                Assert.Multiple(() =>
                {
                    Assert.That(target.Buffer.Writes, Does.Contain(" {{W|B}} 終了 "));
                    Assert.That(target.Buffer.Writes, Does.Contain("< {{W|LB}} 派閥 | ジャーナル {{W|RB}} >"));
                });
            });
    }

    [Test]
    public void FactionsScreenTranspiler_TranslatesFooterPrompt()
    {
        RunWithPatch(
            typeof(DummyFactionsScreenTarget),
            nameof(DummyFactionsScreenTarget.Show),
            typeof(FactionsScreenGamepadPromptTranslationPatch),
            () =>
            {
                var target = new DummyFactionsScreenTarget();
                target.Show();

                Assert.Multiple(() =>
                {
                    Assert.That(target.Buffer.Writes, Does.Contain(" {{W|B}} 終了 "));
                    Assert.That(target.Buffer.Writes, Does.Contain("< {{W|LB}} 装備 | クエスト {{W|RB}} >"));
                });
            });
    }

    [Test]
    public void SkillsAndPowersScreenTranspiler_TranslatesFooterAndBuyPrompt()
    {
        RunWithPatch(
            typeof(DummyLegacySkillsAndPowersScreenTarget),
            nameof(DummyLegacySkillsAndPowersScreenTarget.Show),
            typeof(SkillsAndPowersScreenTranslationPatch),
            () =>
            {
                var target = new DummyLegacySkillsAndPowersScreenTarget();
                target.Show();

                Assert.Multiple(() =>
                {
                    Assert.That(target.Buffer.Writes, Does.Contain(" {{W|B}} 終了 "));
                    Assert.That(target.Buffer.Writes, Does.Contain("< {{W|LB}} ティンカリング | キャラクター {{W|RB}} >"));
                    Assert.That(target.Buffer.Writes, Does.Contain(" [{{W|A}}-購入] "));
                });
            });
    }

    [Test]
    public void EquipmentScreenTranspiler_TranslatesFooterAndPrimaryLimbPrompt()
    {
        RunWithPatch(
            typeof(DummyEquipmentScreenTarget),
            nameof(DummyEquipmentScreenTarget.Show),
            typeof(EquipmentScreenTranslationPatch),
            () =>
            {
                var target = new DummyEquipmentScreenTarget();
                target.Show();

                Assert.Multiple(() =>
                {
                    Assert.That(target.Buffer.Writes, Does.Contain(" {{W|B}} 終了 "));
                    Assert.That(target.Buffer.Writes, Does.Contain("< {{W|LB}} インベントリ | 派閥 {{W|RB}} >"));
                    Assert.That(target.Buffer.Writes, Does.Contain("[{{W|Y - 主要部位を設定}}]"));
                    Assert.That(target.Buffer.Writes, Does.Contain("[{{K|Y - 主要部位を設定}}]"));
                });
            });
    }

    private static void RunWithPatch(Type targetType, string methodName, Type patchType, Action assertion)
    {
        RunWithPatch(targetType, methodName, Type.EmptyTypes, patchType, assertion);
    }

    private static void RunWithPatch(Type targetType, string methodName, Type[] parameterTypes, Type patchType, Action assertion)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(targetType, methodName, parameterTypes),
                transpiler: new HarmonyMethod(RequireMethod(patchType, "Transpiler")));

            assertion();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static string CreateHarmonyId()
    {
        return $"qudjp.tests.legacy-gamepad.{Guid.NewGuid():N}";
    }

    private static MethodInfo RequireMethod(Type type, string methodName, params Type[] parameterTypes)
    {
        var method = parameterTypes.Length == 0
            ? AccessTools.Method(type, methodName)
            : AccessTools.Method(type, methodName, parameterTypes);

        return method
               ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }
}
