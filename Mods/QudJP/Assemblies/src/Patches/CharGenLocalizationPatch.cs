using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class CharGenLocalizationPatch
{
    private static readonly string[] KnownTypeNames =
    {
        "XRL.CharacterCreation.CharacterCreationManager",
        "XRL.CharacterCreation.EmbarkModule",
        "XRL.CharacterBuilds.EmbarkBuilder",
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
                if (!IsCharGenType(type))
                {
                    continue;
                }

                CollectCandidateMethods(type!, targets, seen);
            }
        }

        if (targets.Count == 0)
        {
            Trace.TraceError("QudJP: Failed to resolve CharGen text methods. Patch will not apply.");
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

            __result = UITextSkinTranslationPatch.TranslatePreservingColors(__result, nameof(CharGenLocalizationPatch));
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: CharGenLocalizationPatch.Postfix failed: {0}", ex);
        }
    }

    private static bool IsCharGenType(Type? type)
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

        return fullName.StartsWith("XRL.CharacterCreation.", StringComparison.Ordinal)
               || ContainsOrdinalIgnoreCase(fullName, "CharacterCreation")
               || ContainsOrdinalIgnoreCase(fullName, "Embark");
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

        var candidateName = name.StartsWith("get_", StringComparison.Ordinal)
            ? name.Substring("get_".Length)
            : name;

        return ContainsOrdinalIgnoreCase(candidateName, "Text")
               || ContainsOrdinalIgnoreCase(candidateName, "Title")
               || ContainsOrdinalIgnoreCase(candidateName, "Label")
               || ContainsOrdinalIgnoreCase(candidateName, "Description")
               || ContainsOrdinalIgnoreCase(candidateName, "Display")
               || ContainsOrdinalIgnoreCase(candidateName, "Prompt");
    }

    private static bool ContainsOrdinalIgnoreCase(string source, string value)
    {
#if NET48
        return source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
#else
        return source.Contains(value, StringComparison.OrdinalIgnoreCase);
#endif
    }
}
