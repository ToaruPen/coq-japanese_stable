using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class LegacyGamepadPromptTranslationHelperTests
{
    [Test]
    public void InventoryRendered_TranslatesFooterExitAndFilterPrompts()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                LegacyGamepadPromptTranslationHelpers.TranslateInventoryRendered("< {{W|LB}} Character | Equipment {{W|RB}} >"),
                Is.EqualTo("< {{W|LB}} キャラクター | 装備 {{W|RB}} >"));
            Assert.That(
                LegacyGamepadPromptTranslationHelpers.TranslateInventoryRendered(" {{W|B}} to exit "),
                Is.EqualTo(" {{W|B}} 終了 "));
            Assert.That(
                LegacyGamepadPromptTranslationHelpers.TranslateInventoryRendered("<more...>"),
                Is.EqualTo("<続き…>"));
            Assert.That(
                LegacyGamepadPromptTranslationHelpers.TranslateInventoryRendered("<...more>"),
                Is.EqualTo("<…前へ>"));
            Assert.That(
                LegacyGamepadPromptTranslationHelpers.TranslateInventoryRendered("5 items hidden by filter"),
                Is.EqualTo("フィルターにより5個のアイテムが非表示"));
        });
    }

    [Test]
    public void StatusRendered_TranslatesRaiseAndRandomBuyPrompts()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                LegacyGamepadPromptTranslationHelpers.TranslateStatusRendered(" [{{W|A}}] Raise"),
                Is.EqualTo(" [{{W|A}}] 上昇"));
            Assert.That(
                LegacyGamepadPromptTranslationHelpers.TranslateStatusRendered("Buy a new random mutation for 4 MP"),
                Is.EqualTo("新しいランダムなmutationを4 MPで購入"));
            Assert.That(
                LegacyGamepadPromptTranslationHelpers.TranslateStatusRendered("{{W|M}} - Buy a new random defect for 4 MP"),
                Is.EqualTo("{{W|M}} - 新しいランダムなdefectを4 MPで購入"));
        });
    }

    [Test]
    public void JournalRendered_TranslatesFooterAndEntryActions()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                LegacyGamepadPromptTranslationHelpers.TranslateJournalRendered("< {{W|LB}} Quests | Tinkering {{W|RB}} >"),
                Is.EqualTo("< {{W|LB}} クエスト | ティンカリング {{W|RB}} >"));
            Assert.That(
                LegacyGamepadPromptTranslationHelpers.TranslateJournalRendered(" {{W|X}} - Delete "),
                Is.EqualTo(" {{W|X}} - 削除 "));
            Assert.That(
                LegacyGamepadPromptTranslationHelpers.TranslateJournalRendered(" {{W|Y}} Add {{W|X}} - Delete "),
                Is.EqualTo(" {{W|Y}} 追加 {{W|X}} - 削除 "));
        });
    }

    [Test]
    public void TinkeringRendered_TranslatesModeAndFooterPrompts()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                LegacyGamepadPromptTranslationHelpers.TranslateTinkeringRendered("{{Y|>}} {{W|Build}}    {{w|Mod}}"),
                Is.EqualTo("{{Y|>}} {{W|製作}}    {{w|改造}}"));
            Assert.That(
                LegacyGamepadPromptTranslationHelpers.TranslateTinkeringRendered(" {{W|A}} Mod Item  {{W|Y}} List Mods  {{W|B}} Exit "),
                Is.EqualTo(" {{W|A}} アイテム改造  {{W|Y}} 改造一覧  {{W|B}} 終了 "));
            Assert.That(
                LegacyGamepadPromptTranslationHelpers.TranslateTinkeringRendered(" {{W|A}} Build  {{W|RT}}/{{W|LT}} Scroll  {{W|B}} Exit "),
                Is.EqualTo(" {{W|A}} 製作  {{W|RT}}/{{W|LT}} スクロール  {{W|B}} 終了 "));
        });
    }

    [Test]
    public void EquipmentAndSkillsRendered_TranslatePromptLabels()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                LegacyGamepadPromptTranslationHelpers.TranslateSkillsAndPowersRendered(" [{{W|A}}-Buy] "),
                Is.EqualTo(" [{{W|A}}-購入] "));
            Assert.That(
                LegacyGamepadPromptTranslationHelpers.TranslateEquipmentRendered("[{{W|Y - Set primary limb}}]"),
                Is.EqualTo("[{{W|Y - 主要部位を設定}}]"));
            Assert.That(
                LegacyGamepadPromptTranslationHelpers.TranslateEquipmentRendered("[{{K|Y - Set primary limb}}]"),
                Is.EqualTo("[{{K|Y - 主要部位を設定}}]"));
        });
    }

    [Test]
    public void XrlManualRendered_TranslatesHelpPrompts()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                LegacyGamepadPromptTranslationHelpers.TranslateXrlManualRendered(" [{{W|A}}] Select Topic "),
                Is.EqualTo(" [{{W|A}}] トピックを選択 "));
            Assert.That(
                LegacyGamepadPromptTranslationHelpers.TranslateXrlManualRendered(" [{{W|B}}] Exit Help "),
                Is.EqualTo(" [{{W|B}}] ヘルプを終了 "));
        });
    }
}
