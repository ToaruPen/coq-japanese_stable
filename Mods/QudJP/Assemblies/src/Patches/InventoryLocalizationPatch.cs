using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using QudJP;

namespace QudJP.Patches;

[HarmonyPatch]
public static class InventoryLocalizationPatch
{
    private static readonly string[] KnownTypeNames =
    {
        "XRL.UI.InventoryScreen",
        "Qud.UI.InventoryAndEquipmentStatusScreen",
        "Qud.UI.InventoryLine",
        "XRL.World.IInventoryActionsEvent",
    };

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var seen = new HashSet<MethodBase>();

        for (var index = 0; index < KnownTypeNames.Length; index++)
        {
            var resolvedType = AccessTools.TypeByName(KnownTypeNames[index]);
            if (resolvedType is null)
            {
                continue;
            }

            CollectCandidateMethods(resolvedType, targets, seen);
        }

        if (targets.Count == 0)
        {
            foreach (var type in AccessTools.AllTypes())
            {
                if (!IsInventoryType(type))
                {
                    continue;
                }

                CollectCandidateMethods(type!, targets, seen);
            }
        }

        if (targets.Count == 0)
        {
            Trace.TraceError("QudJP: Failed to resolve inventory text methods. Patch will not apply.");
        }

        return targets;
    }

    public static void Postfix(ref string __result)
    {
        try
        {
            if (string.IsNullOrEmpty(__result))
            {
                return;
            }

            __result = UITextSkinTranslationPatch.TranslatePreservingColors(__result, nameof(InventoryLocalizationPatch));
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: InventoryLocalizationPatch.Postfix failed: {0}", ex);
        }
    }

    private static bool IsInventoryType(Type? type)
    {
        if (type is null)
        {
            return false;
        }

        var fullName = type.FullName;
        if (fullName is null || fullName.Length == 0)
        {
            return false;
        }

        return (fullName.StartsWith("XRL.UI.", StringComparison.Ordinal)
                || fullName.StartsWith("Qud.UI.", StringComparison.Ordinal)
                || fullName.StartsWith("XRL.World.", StringComparison.Ordinal))
               && StringHelpers.ContainsOrdinalIgnoreCase(fullName, "Inventory");
    }

    private static void CollectCandidateMethods(Type type, ICollection<MethodBase> targets, ISet<MethodBase> seen)
    {
        var methods = AccessTools.GetDeclaredMethods(type);
        for (var index = 0; index < methods.Count; index++)
        {
            var method = methods[index];
            if (!IsTextReturningMethodCandidate(method) || !seen.Add(method))
            {
                continue;
            }

            targets.Add(method);
        }
    }

    private static bool IsTextReturningMethodCandidate(MethodInfo method)
    {
        if (method.ReturnType != typeof(string) || method.ContainsGenericParameters)
        {
            return false;
        }

        var name = method.Name;
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        if (name.StartsWith("get_", StringComparison.Ordinal))
        {
            return true;
        }

        return StringHelpers.ContainsOrdinalIgnoreCase(name, "Text")
               || StringHelpers.ContainsOrdinalIgnoreCase(name, "Label")
               || StringHelpers.ContainsOrdinalIgnoreCase(name, "Title")
               || StringHelpers.ContainsOrdinalIgnoreCase(name, "Action")
               || StringHelpers.ContainsOrdinalIgnoreCase(name, "Weight")
               || StringHelpers.ContainsOrdinalIgnoreCase(name, "Category")
               || StringHelpers.ContainsOrdinalIgnoreCase(name, "Display")
               || StringHelpers.ContainsOrdinalIgnoreCase(name, "Name");
    }

}
