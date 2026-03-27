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
        SinkObservation.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDir);
        harmony = new Harmony("QudJP.Tests.SinkPrereq." + Guid.NewGuid().ToString("N"));
    }

    [TearDown]
    public void TearDown()
    {
        harmony.UnpatchAll(harmony.Id);
        Translator.ResetForTests();
        SinkPrereqTextFieldTranslator.ResetForTests();
        SinkObservation.ResetForTests();
        if (Directory.Exists(tempDir))
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Test]
    public void SetDataPatch_ObservationOnly_LeavesLeftSideCategoryTextUnchanged()
    {
        WriteDictionary(("Keybinds", "キーバインド"));
        Translator.SetDictionaryDirectoryForTests(tempDir);

        harmony.Patch(
            original: RequireMethod(typeof(DummyLeftSideCategory), "setData"),
            postfix: new HarmonyMethod(RequireMethod(
                typeof(SinkPrereqSetDataTranslationPatch), "Postfix")));

        var instance = new DummyLeftSideCategory();
        instance.setData(new DummyFrameworkDataElement { Description = "Keybinds" });

        Assert.Multiple(() =>
        {
            Assert.That(instance.text.text, Does.Contain("Keybinds"),
                "Observation-only sink prerequisite patch should leave source text unchanged.");
            Assert.That(
                SinkObservation.GetHitCountForTests(
                    nameof(UITextSkinTranslationPatch),
                    nameof(SinkPrereqSetDataTranslationPatch),
                    SinkObservation.ObservationOnlyDetail,
                    "{{C|Keybinds}}",
                    "Keybinds"),
                Is.GreaterThan(0));
        });
    }

    [Test]
    public void SetDataPatch_ObservationOnly_LeavesFrameworkHeaderTextUnchanged()
    {
        WriteDictionary(("Choose your genotype", "遺伝子型を選択"));
        Translator.SetDictionaryDirectoryForTests(tempDir);

        harmony.Patch(
            original: RequireMethod(typeof(DummyFrameworkHeader), "setData"),
            postfix: new HarmonyMethod(RequireMethod(
                typeof(SinkPrereqSetDataTranslationPatch), "Postfix")));

        var instance = new DummyFrameworkHeader();
        instance.setData(new DummyFrameworkDataElement { Description = "Choose your genotype" });

        Assert.Multiple(() =>
        {
            Assert.That(instance.textSkin.text, Is.EqualTo("Choose your genotype"));
            Assert.That(
                SinkObservation.GetHitCountForTests(
                    nameof(UITextSkinTranslationPatch),
                    nameof(SinkPrereqSetDataTranslationPatch),
                    SinkObservation.ObservationOnlyDetail,
                    "Choose your genotype",
                    "Choose your genotype"),
                Is.GreaterThan(0));
        });
    }

    [Test]
    public void SetDataPatch_ObservationOnly_LeavesSummaryBlockBothFieldsUnchanged()
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

        Assert.Multiple(() =>
        {
            Assert.That(instance.text.text, Is.EqualTo("Summary Description"));
            Assert.That(instance.title.text, Is.EqualTo("Summary Title").Or.Contains("Summary Title"),
                "Observation-only sink prerequisite patch should leave source text unchanged.");
            Assert.That(
                SinkObservation.GetHitCountForTests(
                    nameof(UITextSkinTranslationPatch),
                    nameof(SinkPrereqSetDataTranslationPatch),
                    SinkObservation.ObservationOnlyDetail,
                    "Summary Description",
                    "Summary Description"),
                Is.GreaterThan(0));
        });
    }

    [Test]
    public void SetDataPatch_ObservationOnly_LeavesObjectFinderLineBothFieldsUnchanged()
    {
        WriteDictionary(("nearby", "近く"), ("a sword", "剣"));
        Translator.SetDictionaryDirectoryForTests(tempDir);

        harmony.Patch(
            original: RequireMethod(typeof(DummyObjectFinderLine), "setData"),
            postfix: new HarmonyMethod(RequireMethod(
                typeof(SinkPrereqSetDataTranslationPatch), "Postfix")));

        var instance = new DummyObjectFinderLine();
        instance.setData(new DummyFrameworkDataElement { Title = "nearby", Description = "a sword" });

        Assert.Multiple(() =>
        {
            Assert.That(instance.RightText.text, Is.EqualTo("nearby"));
            Assert.That(instance.ObjectDescription.text, Is.EqualTo("a sword"));
            Assert.That(
                SinkObservation.GetHitCountForTests(
                    nameof(UITextSkinTranslationPatch),
                    nameof(SinkPrereqSetDataTranslationPatch),
                    SinkObservation.ObservationOnlyDetail,
                    "nearby",
                    "nearby"),
                Is.GreaterThan(0));
        });
    }

    [Test]
    public void SetDataPatch_ObservationOnly_LeavesCharacterEffectLineTextUnchanged()
    {
        WriteDictionary(("Confused", "混乱"));
        Translator.SetDictionaryDirectoryForTests(tempDir);

        harmony.Patch(
            original: RequireMethod(typeof(DummyCharacterEffectLine), "setData"),
            postfix: new HarmonyMethod(RequireMethod(
                typeof(SinkPrereqSetDataTranslationPatch), "Postfix")));

        var instance = new DummyCharacterEffectLine();
        instance.setData(new DummyFrameworkDataElement { Description = "Confused" });

        Assert.Multiple(() =>
        {
            Assert.That(instance.text.text, Is.EqualTo("Confused"));
            Assert.That(
                SinkObservation.GetHitCountForTests(
                    nameof(UITextSkinTranslationPatch),
                    nameof(SinkPrereqSetDataTranslationPatch),
                    SinkObservation.ObservationOnlyDetail,
                    "Confused",
                    "Confused"),
                Is.GreaterThan(0));
        });
    }

    [Test]
    public void SetDataPatch_ObservationOnly_LeavesTinkeringDetailsLineFieldsUnchanged()
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

        Assert.Multiple(() =>
        {
            Assert.That(instance.text.text, Is.EqualTo("Laser Pistol"));
            Assert.That(instance.descriptionText.text, Is.EqualTo("A basic weapon."));
            Assert.That(instance.modDescriptionText.text, Is.EqualTo("Enhanced damage"));
            Assert.That(
                SinkObservation.GetHitCountForTests(
                    nameof(UITextSkinTranslationPatch),
                    nameof(SinkPrereqSetDataTranslationPatch),
                    SinkObservation.ObservationOnlyDetail,
                    "Laser Pistol",
                    "Laser Pistol"),
                Is.GreaterThan(0));
        });
    }

    [Test]
    public void UiMethodPatch_ObservationOnly_LeavesCategoryMenusScrollerFieldsUnchanged()
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

        Assert.Multiple(() =>
        {
            Assert.That(instance.selectedTitleText.text, Is.EqualTo("True Kin"));
            Assert.That(instance.selectedDescriptionText.text, Is.EqualTo("Born of the ancient stock."));
            Assert.That(
                SinkObservation.GetHitCountForTests(
                    nameof(UITextSkinTranslationPatch),
                    nameof(SinkPrereqUiMethodTranslationPatch),
                    SinkObservation.ObservationOnlyDetail,
                    "True Kin",
                    "True Kin"),
                Is.GreaterThan(0));
        });
    }

    [Test]
    public void UiMethodPatch_ObservationOnly_LeavesTitledIconButtonTitleUnchanged()
    {
        WriteDictionary(("Accept", "承認"));
        Translator.SetDictionaryDirectoryForTests(tempDir);

        harmony.Patch(
            original: RequireMethod(typeof(DummyTitledIconButton), "Update"),
            postfix: new HarmonyMethod(RequireMethod(
                typeof(SinkPrereqUiMethodTranslationPatch), "Postfix")));

        var instance = new DummyTitledIconButton { Title = "Accept" };
        instance.Update();

        Assert.Multiple(() =>
        {
            Assert.That(instance.TitleText.text, Is.EqualTo("Accept"));
            Assert.That(
                SinkObservation.GetHitCountForTests(
                    nameof(UITextSkinTranslationPatch),
                    nameof(SinkPrereqUiMethodTranslationPatch),
                    SinkObservation.ObservationOnlyDetail,
                    "Accept",
                    "Accept"),
                Is.GreaterThan(0));
        });
    }

    [Test]
    public void UiMethodPatch_ObservationOnly_LeavesCyberneticsTerminalRowDescriptionUnchanged()
    {
        WriteDictionary(("Welcome to the terminal.", "ターミナルへようこそ。"));
        Translator.SetDictionaryDirectoryForTests(tempDir);

        harmony.Patch(
            original: RequireMethod(typeof(DummyCyberneticsTerminalRow), "Update"),
            postfix: new HarmonyMethod(RequireMethod(
                typeof(SinkPrereqUiMethodTranslationPatch), "Postfix")));

        var instance = new DummyCyberneticsTerminalRow { DataText = "Welcome to the terminal." };
        instance.Update();

        Assert.Multiple(() =>
        {
            Assert.That(instance.description.text, Is.EqualTo("Welcome to the terminal."));
            Assert.That(
                SinkObservation.GetHitCountForTests(
                    nameof(UITextSkinTranslationPatch),
                    nameof(SinkPrereqUiMethodTranslationPatch),
                    SinkObservation.ObservationOnlyDetail,
                    "Welcome to the terminal.",
                    "Welcome to the terminal."),
                Is.GreaterThan(0));
        });
    }

    [Test]
    public void UiMethodPatch_ObservationOnly_LeavesAbilityManagerScreenFieldsUnchanged()
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

        Assert.Multiple(() =>
        {
            Assert.That(instance.rightSideHeaderText.text, Is.EqualTo("Sprint"));
            Assert.That(instance.rightSideDescriptionArea.text, Is.EqualTo("Move quickly."));
            Assert.That(
                SinkObservation.GetHitCountForTests(
                    nameof(UITextSkinTranslationPatch),
                    nameof(SinkPrereqUiMethodTranslationPatch),
                    SinkObservation.ObservationOnlyDetail,
                    "Sprint",
                    "Sprint"),
                Is.GreaterThan(0));
        });
    }

    [Test]
    public void UiMethodPatch_ObservationOnly_LeavesMapScrollerPinItemFieldsUnchanged()
    {
        WriteDictionary(("Joppa", "ジョッパ"), ("A small village.", "小さな村。"));
        Translator.SetDictionaryDirectoryForTests(tempDir);

        harmony.Patch(
            original: RequireMethod(typeof(DummyMapScrollerPinItem), "SetData"),
            postfix: new HarmonyMethod(RequireMethod(
                typeof(SinkPrereqUiMethodTranslationPatch), "Postfix")));

        var instance = new DummyMapScrollerPinItem();
        instance.SetData(new DummyFrameworkDataElement { Title = "Joppa", Description = "A small village." });

        Assert.Multiple(() =>
        {
            Assert.That(instance.titleText.text, Is.EqualTo("Joppa"));
            Assert.That(instance.detailsText.text, Is.EqualTo("A small village."));
            Assert.That(
                SinkObservation.GetHitCountForTests(
                    nameof(UITextSkinTranslationPatch),
                    nameof(SinkPrereqUiMethodTranslationPatch),
                    SinkObservation.ObservationOnlyDetail,
                    "Joppa",
                    "Joppa"),
                Is.GreaterThan(0));
        });
    }

    [Test]
    public void UiMethodPatch_ObservationOnly_LeavesPlayerStatusBarZoneUnchanged()
    {
        WriteDictionary(("World Map", "ワールドマップ"));
        Translator.SetDictionaryDirectoryForTests(tempDir);

        harmony.Patch(
            original: RequireMethod(typeof(DummyPlayerStatusBar), "Update"),
            postfix: new HarmonyMethod(RequireMethod(
                typeof(SinkPrereqUiMethodTranslationPatch), "Postfix")));

        var instance = new DummyPlayerStatusBar { ZoneString = "World Map" };
        instance.Update();

        Assert.Multiple(() =>
        {
            Assert.That(instance.ZoneText.text, Is.EqualTo("World Map"));
            Assert.That(
                SinkObservation.GetHitCountForTests(
                    nameof(UITextSkinTranslationPatch),
                    nameof(SinkPrereqUiMethodTranslationPatch),
                    SinkObservation.ObservationOnlyDetail,
                    "World Map",
                    "World Map"),
                Is.GreaterThan(0));
        });
    }

    [Test]
    public void UiMethodPatch_ObservationOnly_LeavesTradeScreenFieldsUnchanged()
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

        Assert.Multiple(() =>
        {
            Assert.That(instance.detailsRightText.text, Is.EqualTo("a copper nugget"));
            Assert.That(instance.detailsLeftText.text, Is.EqualTo("Trade Details"));
            Assert.That(
                SinkObservation.GetHitCountForTests(
                    nameof(UITextSkinTranslationPatch),
                    nameof(SinkPrereqUiMethodTranslationPatch),
                    SinkObservation.ObservationOnlyDetail,
                    "a copper nugget",
                    "a copper nugget"),
                Is.GreaterThan(0));
        });
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
