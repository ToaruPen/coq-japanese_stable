using System.Reflection;
using HarmonyLib;
using QudJP;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
public sealed class SinkPrereqTranslationPatchTests
{
    private string tempDir = null!;
    private Harmony harmony = null!;

    private static MethodInfo RequireMethod(System.Type type, string name) =>
        AccessTools.Method(type, name) ?? throw new InvalidOperationException(
            string.Format(System.Globalization.CultureInfo.InvariantCulture, "Method {0}.{1} not found", type.Name, name));

    [SetUp]
    public void SetUp()
    {
        tempDir = Path.Combine(Path.GetTempPath(), "qudjp-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        SinkPrereqTextFieldTranslator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDir);
        harmony = new Harmony("QudJP.Tests.SinkPrereq." + Guid.NewGuid().ToString("N"));
    }

    [TearDown]
    public void TearDown()
    {
        harmony.UnpatchAll(harmony.Id);
        Translator.ResetForTests();
        SinkPrereqTextFieldTranslator.ResetForTests();
        if (Directory.Exists(tempDir))
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Test]
    public void SetDataPatch_TranslatesLeftSideCategoryText()
    {
        WriteDictionary(("Keybinds", "キーバインド"));
        Translator.SetDictionaryDirectoryForTests(tempDir);

        harmony.Patch(
            original: RequireMethod(typeof(DummyLeftSideCategory), "setData"),
            postfix: new HarmonyMethod(RequireMethod(
                typeof(SinkPrereqSetDataTranslationPatch), "Postfix")));

        var instance = new DummyLeftSideCategory();
        instance.setData(new DummyFrameworkDataElement { Description = "Keybinds" });

        Assert.That(instance.text.text, Does.Contain("キーバインド"),
            "LeftSideCategory text should contain translated Japanese (inside color markup).");
    }

    [Test]
    public void SetDataPatch_TranslatesFrameworkHeaderText()
    {
        WriteDictionary(("Choose your genotype", "遺伝子型を選択"));
        Translator.SetDictionaryDirectoryForTests(tempDir);

        harmony.Patch(
            original: RequireMethod(typeof(DummyFrameworkHeader), "setData"),
            postfix: new HarmonyMethod(RequireMethod(
                typeof(SinkPrereqSetDataTranslationPatch), "Postfix")));

        var instance = new DummyFrameworkHeader();
        instance.setData(new DummyFrameworkDataElement { Description = "Choose your genotype" });

        Assert.That(instance.textSkin.text, Is.EqualTo("遺伝子型を選択"));
    }

    [Test]
    public void SetDataPatch_TranslatesSummaryBlockBothFields()
    {
        WriteDictionary(("Summary Description", "概要説明"), ("Summary Title", "概要タイトル"));
        Translator.SetDictionaryDirectoryForTests(tempDir);

        harmony.Patch(
            original: RequireMethod(typeof(DummySummaryBlockControl), "setData"),
            postfix: new HarmonyMethod(RequireMethod(
                typeof(SinkPrereqSetDataTranslationPatch), "Postfix")));

        var instance = new DummySummaryBlockControl();
        instance.setData(new DummyFrameworkDataElement
        {
            Description = "Summary Description",
            Title = "Summary Title"
        });

        Assert.That(instance.text.text, Is.EqualTo("概要説明"));
        Assert.That(instance.title.text, Is.EqualTo("概要タイトル").Or.Contains("概要タイトル"),
            "Title should be translated (may include color markup).");
    }

    [Test]
    public void SetDataPatch_TranslatesObjectFinderLineBothFields()
    {
        WriteDictionary(("nearby", "近く"), ("a sword", "剣"));
        Translator.SetDictionaryDirectoryForTests(tempDir);

        harmony.Patch(
            original: RequireMethod(typeof(DummyObjectFinderLine), "setData"),
            postfix: new HarmonyMethod(RequireMethod(
                typeof(SinkPrereqSetDataTranslationPatch), "Postfix")));

        var instance = new DummyObjectFinderLine();
        instance.setData(new DummyFrameworkDataElement { Title = "nearby", Description = "a sword" });

        Assert.That(instance.RightText.text, Is.EqualTo("近く"));
        Assert.That(instance.ObjectDescription.text, Is.EqualTo("剣"));
    }

    [Test]
    public void SetDataPatch_TranslatesCharacterEffectLineText()
    {
        WriteDictionary(("Confused", "混乱"));
        Translator.SetDictionaryDirectoryForTests(tempDir);

        harmony.Patch(
            original: RequireMethod(typeof(DummyCharacterEffectLine), "setData"),
            postfix: new HarmonyMethod(RequireMethod(
                typeof(SinkPrereqSetDataTranslationPatch), "Postfix")));

        var instance = new DummyCharacterEffectLine();
        instance.setData(new DummyFrameworkDataElement { Description = "Confused" });

        Assert.That(instance.text.text, Is.EqualTo("混乱"));
    }

    [Test]
    public void SetDataPatch_TranslatesTinkeringDetailsLineMultipleFields()
    {
        WriteDictionary(
            ("Laser Pistol", "レーザーピストル"),
            ("A basic weapon.", "基本的な武器。"),
            ("Enhanced damage", "ダメージ強化"));
        Translator.SetDictionaryDirectoryForTests(tempDir);

        harmony.Patch(
            original: RequireMethod(typeof(DummyTinkeringDetailsLine), "setData"),
            postfix: new HarmonyMethod(RequireMethod(
                typeof(SinkPrereqSetDataTranslationPatch), "Postfix")));

        var instance = new DummyTinkeringDetailsLine();
        instance.setData(new DummyFrameworkDataElement
        {
            Title = "Laser Pistol",
            Description = "A basic weapon.",
            LongDescription = "Enhanced damage"
        });

        Assert.That(instance.text.text, Is.EqualTo("レーザーピストル"));
        Assert.That(instance.descriptionText.text, Is.EqualTo("基本的な武器。"));
        Assert.That(instance.modDescriptionText.text, Is.EqualTo("ダメージ強化"));
    }

    [Test]
    public void UiMethodPatch_TranslatesCategoryMenusScrollerFields()
    {
        WriteDictionary(("True Kin", "トゥルーキン"), ("Born of the ancient stock.", "古の血統に生まれし者。"));
        Translator.SetDictionaryDirectoryForTests(tempDir);

        harmony.Patch(
            original: RequireMethod(typeof(DummyCategoryMenusScroller), "UpdateDescriptions"),
            postfix: new HarmonyMethod(RequireMethod(
                typeof(SinkPrereqUiMethodTranslationPatch), "Postfix")));

        var instance = new DummyCategoryMenusScroller();
        instance.UpdateDescriptions(new DummyFrameworkDataElement
        {
            Description = "True Kin",
            LongDescription = "Born of the ancient stock."
        });

        Assert.That(instance.selectedTitleText.text, Is.EqualTo("トゥルーキン"));
        Assert.That(instance.selectedDescriptionText.text, Is.EqualTo("古の血統に生まれし者。"));
    }

    [Test]
    public void UiMethodPatch_TranslatesTitledIconButtonTitle()
    {
        WriteDictionary(("Accept", "承認"));
        Translator.SetDictionaryDirectoryForTests(tempDir);

        harmony.Patch(
            original: RequireMethod(typeof(DummyTitledIconButton), "Update"),
            postfix: new HarmonyMethod(RequireMethod(
                typeof(SinkPrereqUiMethodTranslationPatch), "Postfix")));

        var instance = new DummyTitledIconButton { Title = "Accept" };
        instance.Update();

        Assert.That(instance.TitleText.text, Is.EqualTo("承認"));
    }

    [Test]
    public void UiMethodPatch_TranslatesCyberneticsTerminalRowDescription()
    {
        WriteDictionary(("Welcome to the terminal.", "ターミナルへようこそ。"));
        Translator.SetDictionaryDirectoryForTests(tempDir);

        harmony.Patch(
            original: RequireMethod(typeof(DummyCyberneticsTerminalRow), "Update"),
            postfix: new HarmonyMethod(RequireMethod(
                typeof(SinkPrereqUiMethodTranslationPatch), "Postfix")));

        var instance = new DummyCyberneticsTerminalRow { DataText = "Welcome to the terminal." };
        instance.Update();

        Assert.That(instance.description.text, Is.EqualTo("ターミナルへようこそ。"));
    }

    [Test]
    public void UiMethodPatch_TranslatesAbilityManagerScreenFields()
    {
        WriteDictionary(("Sprint", "スプリント"), ("Move quickly.", "素早く移動する。"));
        Translator.SetDictionaryDirectoryForTests(tempDir);

        harmony.Patch(
            original: RequireMethod(typeof(DummyAbilityManagerScreen), "HandleHighlightLeft"),
            postfix: new HarmonyMethod(RequireMethod(
                typeof(SinkPrereqUiMethodTranslationPatch), "Postfix")));

        var instance = new DummyAbilityManagerScreen();
        instance.HandleHighlightLeft(new DummyFrameworkDataElement
        {
            Description = "Sprint",
            LongDescription = "Move quickly."
        });

        Assert.That(instance.rightSideHeaderText.text, Is.EqualTo("スプリント"));
        Assert.That(instance.rightSideDescriptionArea.text, Is.EqualTo("素早く移動する。"));
    }

    [Test]
    public void UiMethodPatch_TranslatesMapScrollerPinItemFields()
    {
        WriteDictionary(("Joppa", "ジョッパ"), ("A small village.", "小さな村。"));
        Translator.SetDictionaryDirectoryForTests(tempDir);

        harmony.Patch(
            original: RequireMethod(typeof(DummyMapScrollerPinItem), "SetData"),
            postfix: new HarmonyMethod(RequireMethod(
                typeof(SinkPrereqUiMethodTranslationPatch), "Postfix")));

        var instance = new DummyMapScrollerPinItem();
        instance.SetData(new DummyFrameworkDataElement { Title = "Joppa", Description = "A small village." });

        Assert.That(instance.titleText.text, Is.EqualTo("ジョッパ"));
        Assert.That(instance.detailsText.text, Is.EqualTo("小さな村。"));
    }

    [Test]
    public void UiMethodPatch_TranslatesPlayerStatusBarZone()
    {
        WriteDictionary(("World Map", "ワールドマップ"));
        Translator.SetDictionaryDirectoryForTests(tempDir);

        harmony.Patch(
            original: RequireMethod(typeof(DummyPlayerStatusBar), "Update"),
            postfix: new HarmonyMethod(RequireMethod(
                typeof(SinkPrereqUiMethodTranslationPatch), "Postfix")));

        var instance = new DummyPlayerStatusBar { ZoneString = "World Map" };
        instance.Update();

        Assert.That(instance.ZoneText.text, Is.EqualTo("ワールドマップ"));
    }

    [Test]
    public void UiMethodPatch_TranslatesTradeScreenFields()
    {
        WriteDictionary(("a copper nugget", "銅のナゲット"), ("Trade Details", "取引詳細"));
        Translator.SetDictionaryDirectoryForTests(tempDir);

        harmony.Patch(
            original: RequireMethod(typeof(DummyTradeScreen), "HandleHighlightObject"),
            postfix: new HarmonyMethod(RequireMethod(
                typeof(SinkPrereqUiMethodTranslationPatch), "Postfix")));

        var instance = new DummyTradeScreen();
        instance.HandleHighlightObject(new DummyFrameworkDataElement
        {
            Description = "a copper nugget",
            Title = "Trade Details"
        });

        Assert.That(instance.detailsRightText.text, Is.EqualTo("銅のナゲット"));
        Assert.That(instance.detailsLeftText.text, Is.EqualTo("取引詳細"));
    }

    [Test]
    public void SetDataPatch_UntranslatedTextPassesThrough()
    {
        // No dictionary entries — text should pass through unchanged
        harmony.Patch(
            original: RequireMethod(typeof(DummyCharacterEffectLine), "setData"),
            postfix: new HarmonyMethod(RequireMethod(
                typeof(SinkPrereqSetDataTranslationPatch), "Postfix")));

        var instance = new DummyCharacterEffectLine();
        instance.setData(new DummyFrameworkDataElement { Description = "UnknownEffect123" });

        Assert.That(instance.text.text, Is.EqualTo("UnknownEffect123"),
            "Text without dictionary entry should pass through unchanged.");
    }

    private void WriteDictionary(params (string Key, string Text)[] entries)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("{\"entries\":[");
        for (var i = 0; i < entries.Length; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append("{\"key\":\"");
            sb.Append(entries[i].Key.Replace("\"", "\\\""));
            sb.Append("\",\"text\":\"");
            sb.Append(entries[i].Text.Replace("\"", "\\\""));
            sb.Append("\"}");
        }
        sb.Append("]}");
        File.WriteAllText(Path.Combine(tempDir, "test.ja.json"), sb.ToString());
    }
}
