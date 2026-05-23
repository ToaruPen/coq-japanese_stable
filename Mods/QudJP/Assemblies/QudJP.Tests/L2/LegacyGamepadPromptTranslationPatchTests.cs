using System.IO;
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
    public void XrlCoreStartMainMenuTranspiler_TranslatesLegacyMainMenuBufferText()
    {
        RunWithPatch(
            typeof(DummyXrlCoreStartMainMenuTarget),
            nameof(DummyXrlCoreStartMainMenuTarget._Start),
            typeof(XrlCoreStartMainMenuTranslationPatch),
            () =>
            {
                var target = new DummyXrlCoreStartMainMenuTarget();
                RequireMethod(typeof(DummyXrlCoreStartMainMenuTarget), nameof(DummyXrlCoreStartMainMenuTarget._Start))
                    .Invoke(target, null);

                Assert.Multiple(() =>
                {
                    Assert.That(target.Buffer.Writes, Does.Contain("{{W|新}}しいゲーム"));
                    Assert.That(target.Buffer.Writes, Does.Contain("新しいゲーム"));
                    Assert.That(target.Buffer.Writes, Does.Contain("{{K|続ける}}"));
                    Assert.That(target.Buffer.Writes, Does.Contain("{{W|オ}}プション"));
                    Assert.That(target.Buffer.Writes, Does.Contain("{{W|ハ}}イスコア"));
                    Assert.That(target.Buffer.Writes, Does.Contain("[{{W|?}}] ヘルプ"));
                    Assert.That(target.Buffer.Writes, Does.Contain("{{W|終}}了"));
                    Assert.That(target.Buffer.Writes, Does.Contain("{{W|コ}}ードを引き換える"));
                    Assert.That(target.Buffer.Writes, Does.Contain("{{g|ドロマド版}}"));
                    Assert.That(target.Buffer.Writes, Does.Contain("{{W|M}}od"));
                    Assert.That(target.Buffer.Writes, Does.Contain("{{y|-}} {{R|エラーのあるModがあります。}}"));
                    Assert.That(target.Buffer.Writes, Does.Contain("{{y|-}} {{R|依存関係が不足しているModがあります。}}"));
                    Assert.That(target.Buffer.Writes, Does.Contain("{{y|-}} {{R|未承認のスクリプトModがあります。}}"));
                    Assert.That(target.Buffer.Writes, Does.Contain("  {{C|『Caves of Qud』}}  "));
                    Assert.That(target.Buffer.Writes, Does.Contain(" {{Y|著作権 ({{w|c}}) Freehold Games({{w|tm}})}} "));
                });
            });
    }

    [Test]
    public void MissileWeaponShowPickerTranspiler_TranslatesLegacyFireModePrompts()
    {
        RunWithPatch(
            typeof(DummyMissileWeaponShowPickerTarget),
            nameof(DummyMissileWeaponShowPickerTarget.ShowPicker),
            typeof(MissileWeaponShowPickerTranslationPatch),
            () =>
            {
                var target = new DummyMissileWeaponShowPickerTarget();
                RequireMethod(typeof(DummyMissileWeaponShowPickerTarget), nameof(DummyMissileWeaponShowPickerTarget.ShowPicker))
                    .Invoke(target, null);

                Assert.Multiple(() =>
                {
                    Assert.That(target.Buffer.Writes, Does.Contain("{{G|マーク済み対象}}"));
                    Assert.That(target.Buffer.Writes, Does.Contain(" {{W|M}} - 対象をマーク"));
                    Assert.That(target.Buffer.Writes, Does.Contain("{{K|A - 完全制圧射撃 (未マーク)}}"));
                    Assert.That(target.Buffer.Writes, Does.Contain("{{W|B}} - {{W|制圧射撃}}"));
                    Assert.That(target.Buffer.Writes, Does.Contain("{{K|C - 創傷射撃 ({{C|2}} ターン)}}"));
                    Assert.That(target.Buffer.Writes, Does.Contain("[{{W|M}}] メニュー"));
                    Assert.That(target.Buffer.Writes, Does.Contain("{{W|space}}-select | ロック解除 ({{hotkey|F1}}) | 飛び道具を射撃"));
                });
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
                    Assert.That(target.Buffer.Writes, Does.Contain("[ {{W|インベントリ}} ]"));
                    Assert.That(target.Buffer.Writes, Does.Contain(" {{W|B}} 終了 "));
                    Assert.That(target.Buffer.Writes, Does.Contain(" {{W|ESC}} or {{W|5}} 終了 "));
                    Assert.That(target.Buffer.Writes, Does.Contain("< {{W|LB}} キャラクター | 装備 {{W|RB}} >"));
                    Assert.That(target.Buffer.Writes, Does.Contain("<続き…>"));
                    Assert.That(target.Buffer.Writes, Does.Contain("<{{W|8}} 上へスクロール>"));
                    Assert.That(target.Buffer.Writes, Does.Contain("<…前へ>"));
                    Assert.That(target.Buffer.Writes, Does.Contain("<{{W|2}} 下へスクロール>"));
                    Assert.That(target.Buffer.Writes, Does.Contain("総重量: {{Y|12 {{y|/}}  250 ポンド}}"));
                    Assert.That(target.Buffer.Writes, Does.Contain("{{K|, 2個}}"));
                    Assert.That(target.Buffer.Writes, Does.Contain("{{K|, 1個}}"));
                    Assert.That(target.Buffer.Writes, Does.Contain("[{{W|?}} クイックキー表示]"));
                    Assert.That(target.Buffer.Writes, Does.Contain("フィルターにより5個のアイテムが非表示"));
                });
            });
    }

    [Test]
    public void AggregateTranspiler_DispatchesByFallbackTypeName()
    {
        RunWithPatch(
            typeof(InventoryScreen),
            nameof(InventoryScreen.Show),
            typeof(LegacyGamepadPromptTranslationPatch),
            () =>
            {
                var target = new InventoryScreen();
                target.Show();

                Assert.Multiple(() =>
                {
                    Assert.That(target.Buffer.Writes, Does.Contain(" {{W|B}} 終了 "));
                    Assert.That(target.Buffer.Writes, Does.Contain("< {{W|LB}} キャラクター | 装備 {{W|RB}} >"));
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
                    Assert.That(target.Buffer.Writes, Does.Contain("[ {{W|ティンカリング}} ]"));
                    Assert.That(target.Buffer.Writes, Does.Contain(" {{R|敵対者が近くにいる}} "));
                    Assert.That(target.Buffer.Writes, Does.Contain("{{Y|>}} {{W|製作}}    {{w|改造}}"));
                    Assert.That(target.Buffer.Writes, Does.Contain("  {{w|製作}}  {{Y|>}} {{W|改造}}"));
                    Assert.That(target.Buffer.Writes, Does.Contain("ティンカリングスキルを持っていない。"));
                    Assert.That(target.Buffer.Writes, Does.Contain("改造設計図を持っていない。"));
                    Assert.That(target.Buffer.Writes, Does.Contain("改造可能なアイテムを持っていない。"));
                    Assert.That(target.Buffer.Writes, Does.Contain("アイテム設計図を持っていない。"));
                    Assert.That(target.Buffer.Writes, Does.Contain(" {{W|A}} アイテム改造  {{W|Y}} 改造一覧  {{W|B}} 終了 "));
                    Assert.That(target.Buffer.Writes, Does.Contain(" {{W|A}} 製作  {{W|RT}}/{{W|LT}} スクロール  {{W|B}} 終了 "));
                    Assert.That(target.Buffer.Writes, Does.Contain(" ビットロッカー "));
                    Assert.That(target.Buffer.Writes, Does.Contain("{{R|A スクラップ動力系}}"));
                    Assert.That(target.Buffer.Writes, Does.Contain("{{G|\a スクラップ結晶}}"));
                    Assert.That(target.Buffer.Writes, Does.Contain("-または-"));
                    Assert.That(target.Buffer.Writes, Does.Contain("< {{W|LB}} ジャーナル | スキル {{W|RB}} >"));
                });
            });
    }

    [Test]
    public void AbilityManagerShowTranspiler_TranslatesLegacyChromeRowsAndDetails()
    {
        RunWithPatch(
            typeof(DummyAbilityManagerShowTarget),
            nameof(DummyAbilityManagerShowTarget.Show),
            typeof(AbilityManagerLegacyScreenTranslationPatch),
            () =>
            {
                var target = new DummyAbilityManagerShowTarget();
                RequireMethod(typeof(DummyAbilityManagerShowTarget), nameof(DummyAbilityManagerShowTarget.Show))
                    .Invoke(target, null);

                Assert.Multiple(() =>
                {
                    Assert.That(target.Buffer.Writes, Does.Contain("[ {{W|能力管理}} ]"));
                    Assert.That(target.Buffer.Writes, Does.Contain(" {{W|T}}-任意 "));
                    Assert.That(target.Buffer.Writes, Does.Contain(" {{W|ESC}}-終了 "));
                    Assert.That(target.Buffer.Writes, Does.Contain("{{W|戦技}}"));
                    Assert.That(target.FooterLength, Is.EqualTo("戦技".Length));
                    Assert.That(target.Buffer.Writes, Does.Contain("  a) スプリント [{{W|攻撃}}] {{Y|<{{w|S}}>}}"));
                    Assert.That(target.Buffer.Writes, Does.Contain("{{K|  b) テレポート [攻撃] [無効]}}"));
                    Assert.That(target.Buffer.Writes, Does.Contain("  {{K|c}}) スプリント [{{C|7}}ターンのクールダウン、アストラル束縛] {{K|[{{g|オン}}]}}"));
                    Assert.That(target.Buffer.Writes, Does.Contain("クールダウン: {{C|7}}ラウンド"));
                    Assert.That(target.Buffer.Writes, Does.Contain("[ {{W|Enter}}-能力を使用 {{W|Ins}}-キー割り当て {{W|Del}}-キー解除 {{W|Up}}/{{W|Down}}-順序変更 ]"));
                    Assert.That(target.Buffer.Writes, Does.Contain("{{W|<続き…>}}"));
                    Assert.That(target.Buffer.Writes, Does.Contain("既訳能力"));
                });
            });
    }

    [Test]
    public void TradeUiShowTradeScreenTranspiler_TranslatesLegacyConsoleChrome()
    {
        RunWithPatch(
            typeof(DummyLegacyTradeUiScreenTarget),
            nameof(DummyLegacyTradeUiScreenTarget.ShowTradeScreen),
            typeof(TradeUiLegacyScreenTranslationPatch),
            () =>
            {
                var target = new DummyLegacyTradeUiScreenTarget();
                RequireMethod(typeof(DummyLegacyTradeUiScreenTarget), nameof(DummyLegacyTradeUiScreenTarget.ShowTradeScreen))
                    .Invoke(target, null);

                Assert.Multiple(() =>
                {
                    Assert.That(target.AddRemoveLength, Is.EqualTo(DummyLegacyMarkup.StripFormatting("[{{W|A}}/{{W|R}} 追加/削除]").Length));
                    Assert.That(target.OfferLength, Is.EqualTo(DummyLegacyMarkup.StripFormatting("[{{W|O}} 提示]").Length));
                    Assert.That(target.Buffer.Writes, Does.Contain("[ {{W|dromadのインベントリ}} ]"));
                    Assert.That(target.Buffer.Writes, Does.Contain("[ {{W|あなたのインベントリ}} ]"));
                    Assert.That(target.Buffer.Writes, Does.Contain(" {{G|[あなたの所有]}}"));
                    Assert.That(target.Buffer.Writes, Does.Contain("{{R|[ 他人の所有 ]}}"));
                    Assert.That(target.Buffer.Writes, Does.Contain("[{{W|ESC}} 終了]"));
                    Assert.That(target.Buffer.Writes, Does.Contain("[{{W|A}}/{{W|R}} 追加/削除]"));
                    Assert.That(target.Buffer.Writes, Does.Contain("[{{W|0-9}} 選択]"));
                    Assert.That(target.Buffer.Writes, Does.Contain("[{{W|O}} 提示]"));
                    Assert.That(target.Buffer.Writes, Does.Contain("[{{W|H}} 値切る]"));
                    Assert.That(target.Buffer.Writes, Does.Contain("[{{W|T}} 受け渡し]"));
                    Assert.That(target.Buffer.Writes, Does.Contain("[{{W|V}} 操作]"));
                    Assert.That(target.Buffer.Writes, Does.Contain(" {{C|42}} ドラム <-> {{C|10}} ドラム "));
                    Assert.That(target.Buffer.Writes, Does.Contain(" {{K|12/250 ポンド}} "));
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
        LocalizationAssetResolver.SetLocalizationRootForTests(
            Path.Combine(L1.TestProjectPaths.GetRepositoryRoot(), "Mods", "QudJP", "Localization"));
        try
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
                        Assert.That(target.Buffer.Writes, Does.Contain("[{{C|100}}sp] {{w|工匠}}"));
                        Assert.That(target.Buffer.Writes, Does.Contain("{{g|工匠 II}} [{{C|200}}sp] {{C|23}} {{R|INT}}"));
                        Assert.That(target.Buffer.Writes, Does.Contain(", {{G|工匠 I}}"));
                    });
                });
        }
        finally
        {
            LocalizationAssetResolver.SetLocalizationRootForTests(null);
        }
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
