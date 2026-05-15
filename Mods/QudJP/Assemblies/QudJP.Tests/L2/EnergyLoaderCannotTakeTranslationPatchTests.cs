using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class EnergyLoaderCannotTakeTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DummyPopupShow.Reset();
    }

    [TestCase(
        nameof(DummyEnergyLoaderCannotTakeTarget.ElectricalDischargeLoaderFireEvent),
        "The {{Y|electrifuge}} cannot take the {{Y|chem cell}}.",
        "{{Y|electrifuge}}に{{Y|chem cell}}を装填できない。")]
    [TestCase(
        nameof(DummyEnergyLoaderCannotTakeTarget.EnergyAmmoLoaderFireEvent),
        "{{Y|eigenpistol}} cannot take {{Y|chem cell}}.",
        "{{Y|eigenpistol}}に{{Y|chem cell}}を装填できない。")]
    public void EnergyLoaderCannotTake_TranslatesPopup_WhenOwnerPatched(
        string methodName,
        string source,
        string expected)
    {
        AssertPopupMessage(methodName, source, expected);
    }

    [Test]
    public void EnergyLoaderCannotTake_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        const string source = "The {{Y|electrifuge}} cannot take the {{Y|chem cell}}.";
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
    public void EnergyLoaderCannotTake_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        const string source = "The electrifuge cannot take the chem cell.";
        AssertPopupMessage(
            nameof(DummyEnergyLoaderCannotTakeTarget.ElectricalDischargeLoaderFireEvent),
            MessageFrameTranslator.MarkDirectTranslation(source),
            source);
    }

    [TestCase("")]
    [TestCase("The {{Y|electrifuge}} takes the {{Y|chem cell}}.")]
    [TestCase("The {{Y|electrifuge}} cannot accept the {{Y|chem cell}}.")]
    public void EnergyLoaderCannotTake_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched(string source)
    {
        AssertPopupMessage(nameof(DummyEnergyLoaderCannotTakeTarget.ElectricalDischargeLoaderFireEvent), source, source);
        AssertPopupMessage(nameof(DummyEnergyLoaderCannotTakeTarget.EnergyAmmoLoaderFireEvent), source, source);
    }

    private static void AssertPopupMessage(string methodName, string source, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);
            PatchOwner(harmony);

            DummyEnergyLoaderCannotTakeTarget.PopupMessageToShow = source;
            InvokeOwnerMethod(methodName);

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
        }
        finally
        {
            DummyEnergyLoaderCannotTakeTarget.Reset();
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
            nameof(DummyEnergyLoaderCannotTakeTarget.ElectricalDischargeLoaderFireEvent),
            nameof(DummyEnergyLoaderCannotTakeTarget.EnergyAmmoLoaderFireEvent),
        })
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyEnergyLoaderCannotTakeTarget), methodName, typeof(object)),
                prefix: new HarmonyMethod(RequireMethod(typeof(EnergyLoaderCannotTakeTranslationPatch), nameof(EnergyLoaderCannotTakeTranslationPatch.Prefix))),
                finalizer: new HarmonyMethod(RequireMethod(typeof(EnergyLoaderCannotTakeTranslationPatch), nameof(EnergyLoaderCannotTakeTranslationPatch.Finalizer), typeof(Exception))));
        }
    }

    private static void InvokeOwnerMethod(string methodName)
    {
        _ = RequireMethod(typeof(DummyEnergyLoaderCannotTakeTarget), methodName, typeof(object))
            .Invoke(null, new object[] { new object() });
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

    private static class DummyEnergyLoaderCannotTakeTarget
    {
        public static string PopupMessageToShow { get; set; } = string.Empty;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool ElectricalDischargeLoaderFireEvent(object e)
        {
            _ = e;
            DummyPopupShow.Show(PopupMessageToShow);
            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool EnergyAmmoLoaderFireEvent(object e)
        {
            _ = e;
            _ = nameof(EnergyAmmoLoaderFireEvent);
            DummyPopupShow.Show(PopupMessageToShow);
            return true;
        }

        public static void Reset()
        {
            PopupMessageToShow = string.Empty;
        }
    }
}
