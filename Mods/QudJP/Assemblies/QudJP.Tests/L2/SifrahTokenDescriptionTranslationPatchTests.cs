using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class SifrahTokenDescriptionTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        var localizationRoot = Path.Combine(QudJP.Tests.L1.TestProjectPaths.GetRepositoryRoot(), "Mods", "QudJP", "Localization");
        Translator.ResetForTests();
        ScopedDictionaryLookup.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(Path.Combine(localizationRoot, "Dictionaries"));
        LocalizationAssetResolver.SetLocalizationRootForTests(localizationRoot);
        DynamicTextObservability.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        LocalizationAssetResolver.SetLocalizationRootForTests(null);
        ScopedDictionaryLookup.ResetForTests();
        Translator.ResetForTests();
        DynamicTextObservability.ResetForTests();
    }

    [TestCase(
        nameof(DummySifrahTokenDescriptionTarget.BuildLiquid),
        "use water",
        "水を使う",
        "UseNamedLiquid")]
    [TestCase(
        nameof(DummySifrahTokenDescriptionTarget.BuildAttributeSacrifice),
        "sacrifice a point of Agility",
        "Agilityを1ポイント捧げる",
        "SacrificeNamedAttribute")]
    [TestCase(
        nameof(DummySifrahTokenDescriptionTarget.SetBeing),
        "invoke Shekhinah, in the manner of {{W|Mechanimists}}",
        "{{W|Mechanimists}}流にShekhinahを呼び出す",
        "InvokeBeingManner")]
    [TestCase(
        nameof(DummySifrahTokenDescriptionTarget.BuildFixedNoArgumentToken),
        "display a Barathrumite token",
        "バラサルム派のしるしを見せる",
        "Exact.DisplayBarathrumiteToken")]
    [TestCase(
        nameof(DummySifrahTokenDescriptionTarget.BuildLiquid),
        "accept a 35% chance of becoming {{C|dazed}}",
        "35%の確率で{{C|朦朧}}状態になることを受け入れる",
        "AcceptChance")]
    [TestCase(
        nameof(DummySifrahTokenDescriptionTarget.BuildLiquid),
        "apply {{C|3}} units of compute power",
        "{{C|3}}ユニットの計算力を使う",
        "ApplyComputePowerAmount")]
    [TestCase(
        nameof(DummySifrahTokenDescriptionTarget.BuildLiquid),
        "apply knowledge of the manufacture of phase cannons",
        "phase cannonsの製造知識を使う",
        "ApplyCreationKnowledgeItem")]
    [TestCase(
        nameof(DummySifrahTokenDescriptionTarget.BuildLiquid),
        "interpret structural scan",
        "structural scanを解釈する",
        "InterpretScanSubject")]
    [TestCase(
        nameof(DummySifrahTokenDescriptionTarget.BuildLiquid),
        "use scrap bit",
        "scrap bitを使う",
        "UseNamedBit")]
    [TestCase(
        nameof(DummySifrahTokenDescriptionTarget.BuildLiquid),
        "offer copper nugget",
        "銅塊を差し出す",
        "OfferNamedItem")]
    public void OwnerPatch_TranslatesDescriptionAssignment_WhenPatched(
        string ownerMethodName,
        string source,
        string expected,
        string detail)
    {
        WithPatchedOwner(ownerMethodName, () =>
        {
            var target = new DummySifrahTokenDescriptionTarget(source);

            InvokeOwner(target, ownerMethodName);

            Assert.Multiple(() =>
            {
                Assert.That(target.Description, Is.EqualTo(expected));
                Assert.That(HitCount(detail), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void OwnerPatch_LeavesUnsupportedDescriptionUnchanged_WhenPatched()
    {
        WithPatchedOwner(nameof(DummySifrahTokenDescriptionTarget.BuildLiquid), () =>
        {
            var target = new DummySifrahTokenDescriptionTarget("use a strange resource");

            target.BuildLiquid();

            Assert.Multiple(() =>
            {
                Assert.That(target.Description, Is.EqualTo("use a strange resource"));
                Assert.That(HitCount("UseNamedLiquid"), Is.Zero);
            });
        });
    }

    [Test]
    public void OwnerPatch_StripsDirectMarkedDescriptionWithoutRetranslating_WhenPatched()
    {
        WithPatchedOwner(nameof(DummySifrahTokenDescriptionTarget.BuildLiquid), () =>
        {
            var target = new DummySifrahTokenDescriptionTarget("\u0001use liquid");

            target.BuildLiquid();

            Assert.Multiple(() =>
            {
                Assert.That(target.Description, Is.EqualTo("use liquid"));
                Assert.That(HitCount("UseNamedLiquid"), Is.Zero);
            });
        });
    }

    [Test]
    public void GetDescriptionPostfix_TranslatesDisplayedAvailabilitySuffix_WhenPatched()
    {
        WithPatchedGetDescription(() =>
        {
            var target = new DummySifrahTokenDescriptionTarget("use {{B|water}}");

            var translated = target.GetDisplayedLiquidDescription();

            Assert.That(translated, Is.EqualTo("{{B|水}}を使う [所持: {{C|2}}ドラム]"));
        });
    }

    [Test]
    public void GetDescriptionPostfix_TranslatesBareAvailabilitySuffix_WhenPatched()
    {
        WithPatchedGetDescription(nameof(DummySifrahTokenDescriptionTarget.GetDisplayedCountDescription), () =>
        {
            var target = new DummySifrahTokenDescriptionTarget("tell a secret");

            var translated = target.GetDisplayedCountDescription();

            Assert.That(translated, Is.EqualTo("秘密を話す [所持: {{C|2}}]"));
        });
    }

    [Test]
    public void GetDescriptionPostfix_TranslatesDynamicElectricalGenerationCharge_WhenPatched()
    {
        WithPatchedGetDescription(nameof(DummySifrahTokenDescriptionTarget.GetDisplayedElectricalGenerationChargeDescription), () =>
        {
            var target = new DummySifrahTokenDescriptionTarget("use {{C|10000}} charge");

            var translated = target.GetDisplayedElectricalGenerationChargeDescription();

            Assert.That(translated, Is.EqualTo("電気生成で{{C|10000}}チャージを使う"));
        });
    }

    private static void WithPatchedOwner(string ownerMethodName, Action action)
    {
        var harmonyId = "qudjp.tests.sifrah-token-description." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummySifrahTokenDescriptionTarget), ownerMethodName),
                postfix: new HarmonyMethod(RequireMethod(
                    typeof(SifrahTokenDescriptionTranslationPatch),
                    nameof(SifrahTokenDescriptionTranslationPatch.Postfix),
                    typeof(object))));
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void WithPatchedGetDescription(Action action)
    {
        WithPatchedGetDescription(nameof(DummySifrahTokenDescriptionTarget.GetDisplayedLiquidDescription), action);
    }

    private static void WithPatchedGetDescription(string ownerMethodName, Action action)
    {
        var harmonyId = "qudjp.tests.sifrah-token-get-description." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummySifrahTokenDescriptionTarget), ownerMethodName),
                postfix: new HarmonyMethod(RequireMethod(
                    typeof(SifrahTokenGetDescriptionTranslationPatch),
                    nameof(SifrahTokenGetDescriptionTranslationPatch.Postfix),
                    typeof(string).MakeByRefType())));
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void InvokeOwner(DummySifrahTokenDescriptionTarget target, string ownerMethodName)
    {
        _ = RequireMethod(typeof(DummySifrahTokenDescriptionTarget), ownerMethodName).Invoke(target, null);
    }

    private static int HitCount(string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            SifrahTokenDescriptionTranslationPatch.Context,
            SifrahTokenDescriptionTranslationPatch.Family + "." + detail);
    }

    private static MethodInfo RequireMethod(Type type, string name, params Type[] parameterTypes)
    {
        var method = AccessTools.Method(type, name, parameterTypes);
        Assert.That(method, Is.Not.Null, $"{type.FullName}.{name} not found");
        return method!;
    }
}

internal sealed class DummySifrahTokenDescriptionTarget
{
    private readonly string sourceDescription;

    public DummySifrahTokenDescriptionTarget(string sourceDescription)
    {
        this.sourceDescription = sourceDescription;
    }

    public string Description { get; private set; } = string.Empty;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void BuildLiquid()
    {
        Description = sourceDescription;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void BuildAttributeSacrifice()
    {
        Description = sourceDescription;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void SetBeing()
    {
        Description = sourceDescription;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void BuildFixedNoArgumentToken()
    {
        Description = sourceDescription;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public string GetDisplayedLiquidDescription()
    {
        return sourceDescription + " [have {{C|2}} drams]";
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public string GetDisplayedCountDescription()
    {
        return sourceDescription + " [have {{C|2}}]";
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public string GetDisplayedElectricalGenerationChargeDescription()
    {
        return sourceDescription + " via Electrical Generation";
    }
}
