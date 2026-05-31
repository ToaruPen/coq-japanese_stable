using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class RandomAltarBaetylTranslationPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-baetyl-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        File.WriteAllText(Path.Combine(tempDirectory, "empty.ja.json"), "{\"entries\":[]}");
        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        DynamicTextObservability.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        PopupTranslatedMessageHandoff.ResetForTests();
        DummyPopupShow.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        DummyPopupShow.Reset();
        PopupTranslatedMessageHandoff.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        Translator.ResetForTests();
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void Patch_TranslatesRewardPopup_WhenOwnerPatched()
    {
        WriteDictionaryFile("ui-displayname-atomic.ja.json", ("carbide dagger", "カーバイドの短剣"));

        AssertOwnerPopup(
            "I ACCEPT YOUR OFFERING!\n\nThe sparking baetyl gives you {{Y|a carbide dagger}}!",
            "捧げ物を受け取った！\n\n火花を散らすベテルは{{Y|カーバイドの短剣}}を授けた！",
            expectedHits: 1);
    }

    [Test]
    public void Patch_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        const string source = "I ACCEPT YOUR OFFERING!\n\nThe sparking baetyl gives you a carbide dagger!";

        OwnerPopupRouteTestHarness.WithPatchedPopupOnly(() => DummyPopupShow.Show(source));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
            Assert.That(HitCount(), Is.Zero);
        });
    }

    [Test]
    public void Patch_TranslatesDemandPopup_WhenOwnerPatched()
    {
        WriteDictionaryFile(
            "ui-displayname-atomic.ja.json",
            ("gravity grenade MK II", "重力グレネード MK II"),
            ("mighty weapon", "強大なる武器"));

        const string source = "PETTY MORTAL! BRING ME 5 gravity grenades MK II, AND I SHALL REWARD YOU WITH a mighty weapon.";
        const string expected = "矮小なる凡人よ！重力グレネード MK II x5を持ってこい。そうすれば強大なる武器を授けよう。";

        AssertOwnerPopup(source, expected, expectedHits: 1);
    }

    [Test]
    public void Patch_TranslatesDynamicQuantityDemandPopup_WhenOwnerPatched()
    {
        WriteDictionaryFile(
            "ui-displayname-atomic.ja.json",
            ("gravity grenade MK II", "重力グレネード MK II"),
            ("mighty weapon", "強大なる武器"));

        const string source = "PETTY MORTAL! BRING ME 7 gravity grenades MK II, AND I SHALL REWARD YOU WITH a mighty weapon.";
        const string expected = "矮小なる凡人よ！重力グレネード MK II x7を持ってこい。そうすれば強大なる武器を授けよう。";

        AssertOwnerPopup(source, expected, expectedHits: 1);
    }

    [Test]
    public void Patch_TranslatesDemandOfferConfirmation_WhenOwnerPatched()
    {
        WriteDictionaryFile(
            "ui-displayname-atomic.ja.json",
            ("gravity grenade MK II", "重力グレネード MK II"),
            ("mighty weapon", "強大なる武器"),
            ("gravity grenade MK II x8", "重力グレネード MK II x8"));

        const string source =
            "PETTY MORTAL! BRING ME 5 gravity grenades MK II, AND I SHALL REWARD YOU WITH a mighty weapon.\n\nOffer the sparking baetyl 5 out of {{Y|gravity grenade MK II x8}} nearby?";
        const string expected =
            "矮小なる凡人よ！重力グレネード MK II x5を持ってこい。そうすれば強大なる武器を授けよう。\n\n近くの{{Y|重力グレネード MK II x8}}のうち5個を火花を散らすベテルに捧げますか？";

        AssertOwnerPopup(
            source,
            expected,
            expectedHits: 1,
            popupMethod: nameof(DummyPopupShow.ShowYesNo));
    }

    [Test]
    public void Patch_TranslatesMixedNearbyAndInventoryOfferConfirmation_WhenOwnerPatched()
    {
        WriteDictionaryFile(
            "ui-displayname-atomic.ja.json",
            ("gravity grenade MK II", "重力グレネード MK II"),
            ("mighty weapon", "強大なる武器"),
            ("gravity grenade MK II x8", "重力グレネード MK II x8"),
            ("chem cell", "ケムセル"));

        const string source =
            "PETTY MORTAL! BRING ME 5 gravity grenades MK II, AND I SHALL REWARD YOU WITH a mighty weapon.\n\nOffer the sparking baetyl 2 out of the {{Y|gravity grenade MK II x8}} nearby and your {{C|chem cell}}?";
        const string expected =
            "矮小なる凡人よ！重力グレネード MK II x5を持ってこい。そうすれば強大なる武器を授けよう。\n\n近くの{{Y|重力グレネード MK II x8}}と{{C|ケムセル}}のうち2個を火花を散らすベテルに捧げますか？";

        AssertOwnerPopup(
            source,
            expected,
            expectedHits: 1,
            popupMethod: nameof(DummyPopupShow.ShowYesNo));
    }

    [Test]
    public void Patch_DoesNotHandoffDemandPopupTranslationToMessageLog()
    {
        const string source = "PETTY MORTAL! BRING ME 5 重力グレネード MK II, AND I SHALL REWARD YOU WITH 強大なる武器.";

        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(RandomAltarBaetylTranslationPatch),
            RequireOwnerMethod(),
            () =>
            {
                new DummyRandomAltarBaetylProducer
                {
                    PopupMessageToShow = source,
                }.BaetylWantsSacrifice();

                var message = source;
                _ = MessageLogPatch.Prefix(ref message, "&W", Capitalize: false);

                Assert.That(message, Is.EqualTo(source));
            });
    }

    [Test]
    public void Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        const string source = "I ACCEPT YOUR OFFERING!\n\nThe sparking baetyl gives you a carbide dagger!";

        AssertOwnerPopup(MessageFrameTranslator.MarkDirectTranslation(source), source, expectedHits: 0);
    }

    [TestCase("")]
    [TestCase("I AM SATED, MORTAL. BEGONE.")]
    public void Patch_DoesNotClaimFixedRuntimeOrEmptyPopup_WhenOwnerPatched(string source)
    {
        AssertOwnerPopup(source, source, expectedHits: 0);
    }

    private static void AssertOwnerPopup(
        string source,
        string expected,
        int expectedHits,
        string popupMethod = nameof(DummyPopupShow.Show))
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(RandomAltarBaetylTranslationPatch),
            RequireOwnerMethod(),
            () =>
            {
                new DummyRandomAltarBaetylProducer
                {
                    PopupMessageToShow = source,
                    PopupMethod = popupMethod,
                }.BaetylWantsSacrifice();

                Assert.Multiple(() =>
                {
                    Assert.That(LastPopupMessage(popupMethod), Is.EqualTo(expected));
                    Assert.That(HitCount(), Is.EqualTo(expectedHits));
                });
            });
    }

    private static MethodInfo RequireOwnerMethod()
    {
        return AccessTools.Method(
                   typeof(DummyRandomAltarBaetylProducer),
                   nameof(DummyRandomAltarBaetylProducer.BaetylWantsSacrifice),
                   [])
               ?? throw new MissingMethodException(
                   typeof(DummyRandomAltarBaetylProducer).FullName,
                   nameof(DummyRandomAltarBaetylProducer.BaetylWantsSacrifice));
    }

    private static int HitCount()
    {
        return OwnerPopupRouteTestHarness.RouteHitCount(
                   typeof(RandomAltarBaetylTranslationPatch),
                   "RandomAltarBaetylRewardPopup")
               + OwnerPopupRouteTestHarness.RouteHitCount(
                   typeof(RandomAltarBaetylTranslationPatch),
                   "RandomAltarBaetylDemandPopup");
    }

    private static string? LastPopupMessage(string popupMethod)
    {
        return popupMethod switch
        {
            nameof(DummyPopupShow.ShowYesNo) => DummyPopupShow.LastShowYesNoMessage,
            _ => DummyPopupShow.LastShowMessage,
        };
    }

    private void WriteDictionaryFile(string fileName, params (string key, string text)[] entries)
    {
        var contents = "{\"entries\":["
            + string.Join(
                ",",
                entries.Select(entry => $"{{\"key\":\"{entry.key}\",\"text\":\"{entry.text}\"}}"))
            + "]}";
        File.WriteAllText(Path.Combine(tempDirectory, fileName), contents);
        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
    }

    private sealed class DummyRandomAltarBaetylProducer
    {
        public string PopupMessageToShow { get; set; } = string.Empty;
        public string PopupMethod { get; set; } = nameof(DummyPopupShow.Show);

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void BaetylWantsSacrifice()
        {
            if (string.Equals(PopupMethod, nameof(DummyPopupShow.ShowYesNo), StringComparison.Ordinal))
            {
                _ = DummyPopupShow.ShowYesNo(PopupMessageToShow);
                return;
            }

            DummyPopupShow.Show(PopupMessageToShow);
        }
    }
}
