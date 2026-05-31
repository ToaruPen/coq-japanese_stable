using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class TinkeringModPopupTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        TinkeringModPopupTranslationPatch.ResetForTests();
        DummyPopupShow.Reset();
        DummyTinkeringModTarget.PopupMessageToShow = string.Empty;
        DummyTinkeringModTarget.PopupMethod = nameof(DummyPopupShow.Show);
        DynamicTextObservability.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        UseRepositoryVerbDictionary();
    }

    [TearDown]
    public void TearDown()
    {
        TinkeringModPopupTranslationPatch.ResetForTests();
        DynamicTextObservability.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        Translator.ResetForTests();
    }

    [TestCase(
        nameof(DummyPopupShow.ShowFail),
        "Your {{Y|unstable wire}} is too unstable to craft with.",
        "あなたの{{Y|unstable wire}}は不安定すぎて工作に使えない。",
        "UnstableIngredient")]
    [TestCase(
        nameof(DummyPopupShow.ShowFail),
        "{{Y|Stopsvalinn}} is too unstable to craft with.",
        "{{Y|Stopsvalinn}}は不安定すぎて工作に使えない。",
        "UnstableIngredient")]
    [TestCase(
        nameof(DummyPopupShow.ShowFail),
        "You don't have the required ingredient: {{Y|銅線}} or {{C|電池}}!",
        "必要な材料が足りない: {{Y|銅線}}または{{C|電池}}！",
        "MissingIngredient")]
    [TestCase(
        nameof(DummyPopupShow.ShowFail),
        "You don't have the required <ABCD> bits! You have:\n\n AB",
        "必要な<ABCD>ビットが足りない！所持ビット:\n\n AB",
        "MissingBits")]
    [TestCase(
        nameof(DummyPopupShow.ShowFail),
        "You don't have the required <ABCD> bits! You have:\n\n A x2 - scrap power systems\nB x1 - scrap crystal\nZ x9 - unknown bit",
        "必要な<ABCD>ビットが足りない！所持ビット:\n\n A x2 - スクラップ動力系\nB x1 - スクラップ結晶\nZ x9 - unknown bit",
        "MissingBits")]
    [TestCase(
        nameof(DummyPopupShow.ShowFail),
        "You can't unequip {{Y|steel boots}}.",
        "{{Y|steel boots}}を外せない。",
        "CantUnequip")]
    [TestCase(
        nameof(DummyPopupShow.ShowFail),
        "You cannot use the ingredient!",
        "その材料は使えない！",
        "CannotUseIngredient")]
    [TestCase(
        nameof(DummyPopupShow.Show),
        "You mod {{Y|steel boots}} to be {{C|spring-loaded}}.",
        "{{Y|steel boots}}を{{C|spring-loaded}}に改造した。",
        "Success")]
    [TestCase(
        nameof(DummyPopupShow.Show),
        "You mod your ナインフォールドのブーツ to be {{C|バネ仕掛け}}.",
        "ナインフォールドのブーツを{{C|バネ仕掛け}}に改造した。",
        "Success")]
    public void PerformUITinkerMod_TranslatesPopupMessages_WhenOwnerPatched(
        string popupMethod,
        string source,
        string expected,
        string detail)
    {
        AssertPopupMessage(popupMethod, source, expected);

        Assert.That(TinkeringModHitCount(detail), Is.EqualTo(1));
    }

    [TestCase(
        "Do you want to play a game of Sifrah to mod {{Y|steel boots}}? You can potentially improve the mod's performance and add capabilities to the item, and the cost of playing Sifrah will replace the normal modding cost.",
        "{{Y|steel boots}}に改造を施すためにシフラーのゲームをプレイしますか？シフラーで改造の性能を向上させたり、アイテムに能力を追加したりできることがあります。シフラーのプレイコストは通常の改造コストの代わりになります。")]
    [TestCase(
        "Do you want to play a game of Sifrah to mod {{Y|steel boots}}? You can potentially improve the mod's performance and add capabilities to the item, and the cost of playing Sifrah will replace the normal modding cost. You do not have the required <ABCD bits to perform the mod normally.",
        "{{Y|steel boots}}に改造を施すためにシフラーのゲームをプレイしますか？シフラーで改造の性能を向上させたり、アイテムに能力を追加したりできることがあります。シフラーのプレイコストは通常の改造コストの代わりになります。通常の改造を行うために必要な<ABCDビットが足りません。")]
    public void PerformUITinkerMod_TranslatesSifrahPrompt_WhenOwnerPatched(string source, string expected)
    {
        AssertPopupMessage(nameof(DummyPopupShow.ShowYesNoCancel), source, expected);

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowYesNoCancelMessage, Is.EqualTo(expected));
            Assert.That(TinkeringModHitCount("SifrahPrompt"), Is.EqualTo(1));
        });
    }

    [Test]
    public void PerformUITinkerMod_TranslatesMarkedBestowalPopup_WhenOwnerPatched()
    {
        var doesFragment = DoesVerbRouteTranslator.MarkDoesFragment(
            "The steel boots seem",
            "seem",
            "The steel boots".Length,
            null);
        var source = doesFragment + " to have taken on new qualities.";

        AssertPopupMessage(nameof(DummyPopupShow.Show), source, "steel bootsは新たな特質を帯びたようだ");

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage!.IndexOf('\u0002'), Is.EqualTo(-1));
            Assert.That(DummyPopupShow.LastShowMessage!.IndexOf('\u001f'), Is.EqualTo(-1));
            Assert.That(DummyPopupShow.LastShowMessage!.IndexOf('\u0003'), Is.EqualTo(-1));
            Assert.That(TinkeringModHitCount("DoesVerb"), Is.EqualTo(1));
        });
    }

    [Test]
    public void PerformUITinkerMod_DoesNotClaimPopupOnlyTraffic_WhenOwnerAbsent()
    {
        const string source = "You mod {{Y|steel boots}} to be {{C|spring-loaded}}.";

        WithPatchedPopupOnly(() => DummyPopupShow.Show(source));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
            Assert.That(TinkeringModHitCount("Success"), Is.Zero);
        });
    }

    [Test]
    public void PerformUITinkerMod_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        const string source = "You cannot use the ingredient!";

        AssertPopupMessage(
            nameof(DummyPopupShow.ShowFail),
            MessageFrameTranslator.MarkDirectTranslation(source),
            source);

        Assert.That(TinkeringModHitCount("CannotUseIngredient"), Is.Zero);
    }

    [Test]
    public void PerformUITinkerMod_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        AssertPopupMessage(nameof(DummyPopupShow.Show), string.Empty, string.Empty);

        Assert.That(TinkeringModHitCount("Success"), Is.Zero);
    }

    private static void AssertPopupMessage(string popupMethod, string source, string expected)
    {
        WithPatchedOwner(() =>
        {
            DummyTinkeringModTarget.PopupMethod = popupMethod;
            DummyTinkeringModTarget.PopupMessageToShow = source;
            DummyTinkeringModTarget.PerformUITinkerMod();
        });

        var actual = string.Equals(popupMethod, nameof(DummyPopupShow.ShowYesNoCancel), StringComparison.Ordinal)
            ? DummyPopupShow.LastShowYesNoCancelMessage
            : DummyPopupShow.LastShowMessage;
        Assert.That(actual, Is.EqualTo(expected));
    }

    private static void WithPatchedOwner(Action action)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupMethods(harmony);
            PatchOwner(harmony);
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void WithPatchedPopupOnly(Action action)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupMethods(harmony);
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void PatchPopupMethods(Harmony harmony)
    {
        var prefix = new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix)));
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.Show)),
            prefix: prefix);
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.ShowYesNoCancel)),
            prefix: prefix);
    }

    private static void PatchOwner(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyTinkeringModTarget), nameof(DummyTinkeringModTarget.PerformUITinkerMod)),
            prefix: new HarmonyMethod(RequireMethod(typeof(TinkeringModPopupTranslationPatch), nameof(TinkeringModPopupTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(
                typeof(TinkeringModPopupTranslationPatch),
                nameof(TinkeringModPopupTranslationPatch.Finalizer),
                typeof(Exception))));
    }

    private static MethodInfo RequireMethod(Type type, string name, params Type[] parameters)
    {
        return parameters.Length == 0
            ? type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
              ?? throw new InvalidOperationException($"{type.FullName}.{name} not found")
            : AccessTools.Method(type, name, parameters)
              ?? throw new InvalidOperationException($"{type.FullName}.{name} not found");
    }

    private static int TinkeringModHitCount(string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(PopupShowTranslationPatch),
            "Popup.ProducerText." + nameof(TinkeringModPopupTranslationPatch) + "." + detail);
    }

    private static void UseRepositoryVerbDictionary()
    {
        var repositoryRoot = QudJP.Tests.L1.TestProjectPaths.GetRepositoryRoot();
        Translator.SetDictionaryDirectoryForTests(Path.Combine(
            repositoryRoot,
            "Mods",
            "QudJP",
            "Localization",
            "Dictionaries"));
        var repositoryDictionaryPath = Path.Combine(
            repositoryRoot,
            "Mods",
            "QudJP",
            "Localization",
            "MessageFrames",
            "verbs.ja.json");
        MessageFrameTranslator.SetDictionaryPathForTests(repositoryDictionaryPath);
    }

    private static string CreateHarmonyId() => $"qudjp.tests.{Guid.NewGuid():N}";
}
