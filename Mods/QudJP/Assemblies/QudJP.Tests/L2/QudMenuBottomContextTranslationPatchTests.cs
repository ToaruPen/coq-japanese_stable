using System.Collections;
using System.Reflection;
using HarmonyLib;
using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class QudMenuBottomContextTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        ResetTestState();
    }

    [TearDown]
    public void TearDown()
    {
        ResetTestState();
    }

    [Test]
    public void Prefix_ObservationOnly_LeavesMenuItemTextUnchanged()
    {
        var context = new DummyQudMenuBottomContext("Inspect");
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyQudMenuBottomContext), nameof(DummyQudMenuBottomContext.RefreshButtons)),
                prefix: new HarmonyMethod(RequireMethod(typeof(QudMenuBottomContextTranslationPatch), nameof(QudMenuBottomContextTranslationPatch.Prefix))));

            context.RefreshButtons();

            Assert.That(((DummyMenuItem)context.items[0]!).text, Is.EqualTo("Inspect"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_ObservationOnly_LogsUnclaimedMenuItemText()
    {
        var context = new DummyQudMenuBottomContext("Inspect");
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyQudMenuBottomContext), nameof(DummyQudMenuBottomContext.RefreshButtons)),
                prefix: new HarmonyMethod(RequireMethod(typeof(QudMenuBottomContextTranslationPatch), nameof(QudMenuBottomContextTranslationPatch.Prefix))));

            context.RefreshButtons();

            const string source = "Inspect";
            Assert.That(
                SinkObservation.GetHitCountForTests(
                    nameof(PopupTranslationPatch),
                    nameof(QudMenuBottomContextTranslationPatch),
                    SinkObservation.ObservationOnlyDetail,
                    source,
                    source),
                Is.GreaterThan(0));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_StripsDirectTranslationMarker_FromMenuItemText()
    {
        var context = new DummyQudMenuBottomContext("\u0001調べる");
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyQudMenuBottomContext), nameof(DummyQudMenuBottomContext.RefreshButtons)),
                prefix: new HarmonyMethod(RequireMethod(typeof(QudMenuBottomContextTranslationPatch), nameof(QudMenuBottomContextTranslationPatch.Prefix))));

            context.RefreshButtons();

            Assert.That(((DummyMenuItem)context.items[0]!).text, Is.EqualTo("調べる"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static string CreateHarmonyId()
    {
        return $"qudjp.tests.{Guid.NewGuid():N}";
    }

    private static MethodInfo RequireMethod(Type type, string methodName)
    {
        return AccessTools.Method(type, methodName)
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private static void ResetTestState()
    {
        Translator.ResetForTests();
        SinkObservation.ResetForTests();
    }

    private sealed class DummyQudMenuBottomContext
    {
        public IList items;

        public DummyQudMenuBottomContext(string text)
        {
            items = new ArrayList { new DummyMenuItem(text) };
        }

        public void RefreshButtons()
        {
        }
    }

    private sealed class DummyMenuItem
    {
        public string text;

        public DummyMenuItem(string text)
        {
            this.text = text;
        }
    }
}
