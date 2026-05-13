using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class PowerEntryRequirementPopupTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DummyPopupShow.Reset();
    }

    [TestCase(
        "You may not learn this skill if you already have Shield Slam.",
        "すでにShield Slamを習得しているため、このスキルは習得できない。")]
    [TestCase(
        "You may not learn this skill if you have {{Y|Multiple Arms}}.",
        "{{Y|Multiple Arms}}を持っているため、このスキルは習得できない。")]
    [TestCase(
        "You may not learn this skill until you have Tactics.",
        "Tacticsを習得するまで、このスキルは習得できない。")]
    [TestCase(
        "You may not learn this skill until you have {{C|Teleportation}}.",
        "{{C|Teleportation}}を習得するまで、このスキルは習得できない。")]
    public void PowerEntry_TranslatesPrerequisitePopups_WhenOwnerPatched(
        string source,
        string expected)
    {
        AssertPopupMessage(source, expected, usePowerEntryRequirement: false);
    }

    [Test]
    public void PowerEntryRequirement_TranslatesAttributePrerequisitePopup_WhenOwnerPatched()
    {
        AssertPopupMessage(
            "Your Strength isn't high enough to buy Shield Slam!",
            "Shield Slamを習得するには筋力が足りない！",
            usePowerEntryRequirement: true);
    }

    [Test]
    public void PowerEntryRequirement_DoesNotTranslateTraffic_WhenOwnerAbsent()
    {
        const string source = "You may not learn this skill if you already have Shield Slam.";
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);

            DummyPopupShow.Show(source);

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void PowerEntryRequirement_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        AssertPopupMessage(
            MessageFrameTranslator.MarkDirectTranslation("You may not learn this skill until you have Tactics."),
            "You may not learn this skill until you have Tactics.",
            usePowerEntryRequirement: false);
    }

    [TestCase("")]
    [TestCase("You may not learn this skill.")]
    [TestCase("Your Strength is not high enough to buy Shield Slam!")]
    public void PowerEntryRequirement_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched(string source)
    {
        AssertPopupMessage(source, source, usePowerEntryRequirement: false);
        AssertPopupMessage(source, source, usePowerEntryRequirement: true);
    }

    private static void AssertPopupMessage(
        string source,
        string expected,
        bool usePowerEntryRequirement)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);
            PatchOwner(harmony);

            DummyPowerEntryRequirementTarget.PopupMessageToShow = source;
            if (usePowerEntryRequirement)
            {
                _ = DummyPowerEntryRequirementTarget.MeetsRequirement();
            }
            else
            {
                _ = DummyPowerEntryRequirementTarget.MeetsRequirements();
            }

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
        }
        finally
        {
            DummyPowerEntryRequirementTarget.Reset();
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void PatchPopupShow(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.Show)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));
    }

    private static void PatchOwner(Harmony harmony)
    {
        foreach (var methodName in new[]
        {
            nameof(DummyPowerEntryRequirementTarget.MeetsRequirements),
            nameof(DummyPowerEntryRequirementTarget.MeetsRequirement),
        })
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPowerEntryRequirementTarget), methodName),
                prefix: new HarmonyMethod(RequireMethod(typeof(PowerEntryRequirementPopupTranslationPatch), nameof(PowerEntryRequirementPopupTranslationPatch.Prefix))),
                finalizer: new HarmonyMethod(RequireMethod(typeof(PowerEntryRequirementPopupTranslationPatch), nameof(PowerEntryRequirementPopupTranslationPatch.Finalizer), typeof(Exception))));
        }
    }

    private static MethodInfo RequireMethod(Type type, string methodName, params Type[] parameterTypes)
    {
        if (parameterTypes.Length == 0)
        {
            return type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
        }

        return AccessTools.Method(type, methodName, parameterTypes)
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private static string CreateHarmonyId()
    {
        return $"qudjp.tests.{Guid.NewGuid():N}";
    }

    private static class DummyPowerEntryRequirementTarget
    {
        public static string PopupMessageToShow { get; set; } = string.Empty;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool MeetsRequirements()
        {
            return ShowPopup(result: false);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool MeetsRequirement()
        {
            if (ShowPopup(result: true))
            {
                return false;
            }

            return true;
        }

        public static void Reset()
        {
            PopupMessageToShow = string.Empty;
        }

        private static bool ShowPopup(bool result)
        {
            DummyPopupShow.Show(PopupMessageToShow);
            return result;
        }
    }
}
