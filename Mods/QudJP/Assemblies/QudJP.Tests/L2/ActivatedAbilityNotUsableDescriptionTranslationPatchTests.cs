using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class ActivatedAbilityNotUsableDescriptionTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        DummyMessageQueue.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        DummyMessageQueue.Reset();
        MessageFrameTranslator.ResetForTests();
        SinkObservation.ResetForTests();
        DynamicTextObservability.ResetForTests();
    }

    [Test]
    public void NotUsableDescription_TranslatesCooldownBeforeQueuedMessageRuns_WhenPatched()
    {
        var harmonyId = "qudjp.tests.activated-ability-not-usable." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(
                    typeof(DummyActivatedAbilityEntry),
                    "get_" + nameof(DummyActivatedAbilityEntry.NotUsableDescription)),
                postfix: new HarmonyMethod(RequireMethod(
                    typeof(ActivatedAbilityNotUsableDescriptionTranslationPatch),
                    nameof(ActivatedAbilityNotUsableDescriptionTranslationPatch.Postfix),
                    typeof(string).MakeByRefType())));
            harmony.Patch(
                original: RequireMethod(
                    typeof(DummyMessageQueue),
                    nameof(DummyMessageQueue.AddPlayerMessage),
                    typeof(string),
                    typeof(string),
                    typeof(bool)),
                prefix: new HarmonyMethod(RequireMethod(
                    typeof(MessageLogPatch),
                    nameof(MessageLogPatch.Prefix),
                    typeof(string).MakeByRefType(),
                    typeof(string),
                    typeof(bool))));

            var entry = new DummyActivatedAbilityEntry
            {
                SourceNotUsableDescription = "You must wait {{C|1 round}} before using {{C|凍結線}}.",
            };

            var captured = entry.NotUsableDescription;
            DummyMessageQueue.AddPlayerMessage(captured!, null, Capitalize: false);

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("{{C|凍結線}}を使うには{{C|1ラウンド}}待つ必要がある。"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(ActivatedAbilityNotUsableDescriptionTranslationPatch),
                        ActivatedAbilityNotUsableDescriptionTranslationPatch.Family),
                    Is.EqualTo(1));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(MessageLogPatch),
                        nameof(MessageLogPatch),
                        SinkObservation.ObservationOnlyDetail,
                        "You must wait {{C|1 round}} before using {{C|凍結線}}.",
                        "You must wait 1 round before using 凍結線."),
                    Is.EqualTo(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void NotUsableDescription_LeavesUnknownTextUnchanged_WhenPatched()
    {
        var source = "{{C|凍結線}} can't be used at this time.";
        var harmonyId = "qudjp.tests.activated-ability-not-usable." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(
                    typeof(DummyActivatedAbilityEntry),
                    "get_" + nameof(DummyActivatedAbilityEntry.NotUsableDescription)),
                postfix: new HarmonyMethod(RequireMethod(
                    typeof(ActivatedAbilityNotUsableDescriptionTranslationPatch),
                    nameof(ActivatedAbilityNotUsableDescriptionTranslationPatch.Postfix),
                    typeof(string).MakeByRefType())));

            var entry = new DummyActivatedAbilityEntry { SourceNotUsableDescription = source };

            Assert.That(entry.NotUsableDescription, Is.EqualTo(source));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void NotUsableDescription_StripsDirectMarker_WhenPatched()
    {
        var harmonyId = "qudjp.tests.activated-ability-not-usable." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(
                    typeof(DummyActivatedAbilityEntry),
                    "get_" + nameof(DummyActivatedAbilityEntry.NotUsableDescription)),
                postfix: new HarmonyMethod(RequireMethod(
                    typeof(ActivatedAbilityNotUsableDescriptionTranslationPatch),
                    nameof(ActivatedAbilityNotUsableDescriptionTranslationPatch.Postfix),
                    typeof(string).MakeByRefType())));

            var entry = new DummyActivatedAbilityEntry
            {
                SourceNotUsableDescription = MessageFrameTranslator.MarkDirectTranslation("Already translated."),
            };

            Assert.That(entry.NotUsableDescription, Is.EqualTo("Already translated."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static MethodInfo RequireMethod(Type type, string name, params Type[] parameters)
    {
        return AccessTools.Method(type, name, parameters)
            ?? throw new MissingMethodException(type.FullName, name);
    }

    private sealed class DummyActivatedAbilityEntry
    {
        public string? SourceNotUsableDescription { get; init; }

        public string? NotUsableDescription => SourceNotUsableDescription;
    }
}
