using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class FirefightingTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        DummyPopupShow.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        DummyPopupShow.Reset();
        MessageFrameTranslator.ResetForTests();
        DynamicTextObservability.ResetForTests();
    }

    [TestCase("You cannot reach the 熊!", "熊に手が届かない。")]
    [TestCase("You cannot reach {{Y|the mechanical cherub}}!", "{{Y|mechanical cherub}}に手が届かない。")]
    public void AttemptFirefightingCore_TranslatesCannotReachPopup_WhenOwnerPatched(
        string source,
        string expected)
    {
        WithPatchedOwner(() =>
        {
            new DummyFirefightingProducer
            {
                MessageToShow = source,
            }.AttemptFirefightingCore();

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
                Assert.That(HitCount("CannotReachSubject"), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void AttemptFirefightingCore_DoesNotClaimPopupOnlyTraffic_WhenOwnerAbsent()
    {
        const string source = "You cannot reach the 熊!";

        OwnerPopupRouteTestHarness.WithPatchedPopupOnly(
            () => DummyPopupShow.ShowFail(source));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
            Assert.That(HitCount("CannotReachSubject"), Is.Zero);
        });
    }

    [Test]
    public void AttemptFirefightingCore_DoesNotRetranslateDirectMarkedShowFail_WhenOwnerPatched()
    {
        const string source = "You cannot reach the 熊!";

        WithPatchedOwner(() =>
        {
            new DummyFirefightingProducer
            {
                MessageToShow = MessageFrameTranslator.MarkDirectTranslation(source),
            }.AttemptFirefightingCore();

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
                Assert.That(HitCount("CannotReachSubject"), Is.Zero);
            });
        });
    }

    [TestCase("")]
    [TestCase("You have no hands to beat at the flames with!")]
    [TestCase("You have no hands to beat at the flames with, and cannot roll on the ground because you are flying!")]
    [TestCase("You have no hands to beat at the flames with, and cannot roll on the ground because you are phased out!")]
    [TestCase("You have no hands to beat at the flames with. Do you want to roll on the ground to try to put them out?")]
    public void AttemptFirefightingCore_DoesNotClaimFixedOrEmptyPopups_WhenOwnerPatched(string source)
    {
        WithPatchedOwner(() =>
        {
            new DummyFirefightingProducer
            {
                MessageToShow = source,
            }.AttemptFirefightingCore();

            Assert.That(HitCount("CannotReachSubject"), Is.Zero);
        });
    }

    private static void WithPatchedOwner(Action action)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(FirefightingTranslationPatch),
            RequireOwnerMethod(),
            action);
    }

    private static MethodInfo RequireOwnerMethod()
    {
        return AccessTools.Method(
                   typeof(DummyFirefightingProducer),
                   nameof(DummyFirefightingProducer.AttemptFirefightingCore))
               ?? throw new MissingMethodException(
                   typeof(DummyFirefightingProducer).FullName,
                   nameof(DummyFirefightingProducer.AttemptFirefightingCore));
    }

    private static int HitCount(string detail)
    {
        return OwnerPopupRouteTestHarness.RouteHitCount(typeof(FirefightingTranslationPatch), detail);
    }

    private sealed class DummyFirefightingProducer
    {
        public string MessageToShow { get; set; } = string.Empty;

        public void AttemptFirefightingCore()
        {
            DummyPopupShow.ShowFail(MessageToShow);
        }
    }
}
