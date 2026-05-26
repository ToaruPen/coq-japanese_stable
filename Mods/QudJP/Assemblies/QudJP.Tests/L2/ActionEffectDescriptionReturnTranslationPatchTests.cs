using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class ActionEffectDescriptionReturnTranslationPatchTests
{
    private string localizationRoot = null!;

    [SetUp]
    public void SetUp()
    {
        localizationRoot = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../../Localization"));
        LocalizationAssetResolver.SetLocalizationRootForTests(localizationRoot);
        Translator.ResetForTests();
        DynamicTextObservability.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        DynamicTextObservability.ResetForTests();
        Translator.ResetForTests();
        LocalizationAssetResolver.SetLocalizationRootForTests(null);
    }

    [TestCase(nameof(DummyActionEffectDescriptionTarget.KillGetDetails), "Player", "プレイヤー", "Player")]
    [TestCase(nameof(DummyActionEffectDescriptionTarget.DisassemblyGetDescription), "disassembling", "分解中", "Disassembling")]
    [TestCase(nameof(DummyActionEffectDescriptionTarget.OngoingActionGetDescription), "acting", "行動中", "Acting")]
    [TestCase(
        nameof(DummyActionEffectDescriptionTarget.MetamorphedGetDetails),
        "Assuming another creature's form.",
        "別の生物の姿をとっている。",
        "MetamorphedDetails")]
    [TestCase(
        nameof(DummyActionEffectDescriptionTarget.IStingerPropertiesGetDescription),
        "You bear a tail with a stinger that delivers poisonous venom to your enemies.",
        "臀部の毒針を持つ。",
        "StingerDescription")]
    public void Postfix_TranslatesCoveredOwnerReturnValue_WhenPatched(
        string ownerMethodName,
        string source,
        string expected,
        string detail)
    {
        WithPatchedOwner(ownerMethodName, () =>
        {
            var target = new DummyActionEffectDescriptionTarget(source);

            var translated = InvokeOwner(target, ownerMethodName);

            Assert.Multiple(() =>
            {
                Assert.That(translated, Is.EqualTo(expected));
                Assert.That(HitCount(detail), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void Postfix_LeavesUnsupportedReturnValueUnchanged_WhenPatched()
    {
        WithPatchedOwner(nameof(DummyActionEffectDescriptionTarget.OngoingActionGetDescription), () =>
        {
            var target = new DummyActionEffectDescriptionTarget("Debug Target");

            var translated = target.OngoingActionGetDescription();

            Assert.Multiple(() =>
            {
                Assert.That(translated, Is.EqualTo("Debug Target"));
                Assert.That(HitCount("Acting"), Is.Zero);
            });
        });
    }

    private static void WithPatchedOwner(string ownerMethodName, Action action)
    {
        var harmonyId = "qudjp.tests.action-effect-description-return." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyActionEffectDescriptionTarget), ownerMethodName),
                postfix: new HarmonyMethod(RequireMethod(
                    typeof(ActionEffectDescriptionReturnTranslationPatch),
                    nameof(ActionEffectDescriptionReturnTranslationPatch.Postfix),
                    typeof(string).MakeByRefType())));
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static string InvokeOwner(DummyActionEffectDescriptionTarget target, string ownerMethodName)
    {
        return (string)RequireMethod(typeof(DummyActionEffectDescriptionTarget), ownerMethodName).Invoke(target, null)!;
    }

    private static int HitCount(string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            ActionEffectDescriptionReturnTranslationPatch.Context,
            ActionEffectDescriptionReturnTranslationPatch.Family + "." + detail);
    }

    private static MethodInfo RequireMethod(Type type, string name, params Type[] parameterTypes)
    {
        var method = AccessTools.Method(type, name, parameterTypes);
        Assert.That(method, Is.Not.Null, $"{type.FullName}.{name} not found");
        return method!;
    }
}

internal sealed class DummyActionEffectDescriptionTarget
{
    private readonly string source;

    public DummyActionEffectDescriptionTarget(string source)
    {
        this.source = source;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public string KillGetDetails() => source;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public string DisassemblyGetDescription() => source;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public string OngoingActionGetDescription() => source;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public string MetamorphedGetDetails() => source;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public string IStingerPropertiesGetDescription() => source;
}
