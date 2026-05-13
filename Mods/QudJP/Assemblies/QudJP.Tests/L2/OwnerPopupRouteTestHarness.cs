using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

internal static class OwnerPopupRouteTestHarness
{
    public static void WithPatchedPopupOwner(Type patchType, MethodInfo ownerMethod, Action action)
    {
        WithPatchedPopupOwners(patchType, [ownerMethod], action);
    }

    public static void WithPatchedPopupOwners(Type patchType, MethodInfo[] ownerMethods, Action action)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShow(harmony);
            foreach (var ownerMethod in ownerMethods)
            {
                PatchOwner(harmony, patchType, ownerMethod);
            }

            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    public static void WithPatchedPopupOnly(Action action)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShow(harmony);
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    public static MethodInfo RequireMethod(Type type, string methodName, params Type[] parameterTypes)
    {
        if (parameterTypes.Length == 0)
        {
            return type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
                   ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
        }

        return AccessTools.Method(type, methodName, parameterTypes)
               ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    public static int RouteHitCount(Type patchType, string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(PopupShowTranslationPatch),
            "Popup.ProducerText." + patchType.Name + "." + detail);
    }

    private static void PatchPopupShow(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.Show)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.ShowYesNo)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.ShowYesNoCancel)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));
    }

    private static void PatchOwner(Harmony harmony, Type patchType, MethodInfo original)
    {
        var prefix = AccessTools.Method(patchType, "Prefix", [typeof(MethodBase)])
                     ?? RequireMethod(patchType, "Prefix");
        harmony.Patch(
            original: original,
            prefix: new HarmonyMethod(prefix),
            finalizer: new HarmonyMethod(
                AccessTools.Method(patchType, "Finalizer", [typeof(Exception)])
                ?? RequireMethod(patchType, "Finalizer")));
    }

    private static string CreateHarmonyId()
    {
        return $"qudjp.tests.{Guid.NewGuid():N}";
    }
}
