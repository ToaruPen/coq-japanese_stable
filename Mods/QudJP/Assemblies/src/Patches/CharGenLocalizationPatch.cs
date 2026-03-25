using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using HarmonyLib;
using QudJP;

namespace QudJP.Patches;

[HarmonyPatch]
public static class CharGenLocalizationPatch
{
    private static readonly string[] KnownTypeNames =
    {
        "XRL.CharacterCreation.CharacterCreationManager",
        "XRL.CharacterCreation.EmbarkModule",
        "XRL.CharacterBuilds.EmbarkBuilder",
        "QudSubtypeModule",
        "QudCallingModule",
        "QudGenotypeModule",
        "QudMutationsModule",
        "QudCyberneticsModule",
        "EmbarkBuilder",
    };

    private static readonly string[] TypeNameHints =
    {
        "CharacterCreation",
        "Embark",
        "Genotype",
        "Mutation",
        "Calling",
        "Cybernetics",
    };

    private static readonly string[] StructuralTypeHints =
    {
        "Module",
        "Builder",
        "Screen",
        "Manager",
        "Window",
    };

    private static readonly string[] ExcludedTypeHints =
    {
        "Data",
        "Entry",
        "Line",
    };

    private static readonly string[] MethodNameHints =
    {
        "Text",
        "Title",
        "Label",
        "Description",
        "Display",
        "Prompt",
        "Choice",
        "Node",
        "Calling",
        "Mutation",
        "Genotype",
        "Cybernetics",
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

            var translated = ChargenStructuredTextTranslator.Translate(__result);
            __result = ColorAwareTranslationComposer.TranslatePreservingColors(translated);
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

        if (fullName.StartsWith("XRL.CharacterCreation.", StringComparison.Ordinal))
        {
            return true;
        }

        for (var excludedIndex = 0; excludedIndex < ExcludedTypeHints.Length; excludedIndex++)
        {
            if (StringHelpers.ContainsOrdinalIgnoreCase(fullName, ExcludedTypeHints[excludedIndex]))
            {
                return false;
            }
        }

        for (var index = 0; index < TypeNameHints.Length; index++)
        {
            if (StringHelpers.ContainsOrdinalIgnoreCase(fullName, TypeNameHints[index]))
            {
                for (var structuralIndex = 0; structuralIndex < StructuralTypeHints.Length; structuralIndex++)
                {
                    if (StringHelpers.ContainsOrdinalIgnoreCase(fullName, StructuralTypeHints[structuralIndex]))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
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
        try
        {
            if (method.ReturnType != typeof(string) || method.ContainsGenericParameters)
            {
                return false;
            }
        }
        catch (FileNotFoundException ex)
        {
            Trace.TraceWarning(
                "QudJP: CharGenLocalizationPatch skipped method '{0}' on '{1}' because dependency assembly '{2}' is unavailable during target resolution.",
                method.Name,
                method.DeclaringType?.FullName,
                ex.FileName);
            return false;
        }
        catch (TypeLoadException ex)
        {
            Trace.TraceWarning(
                "QudJP: CharGenLocalizationPatch skipped method '{0}' on '{1}' because a dependent type could not be loaded: {2}",
                method.Name,
                method.DeclaringType?.FullName,
                ex.Message);
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

        if (StringHelpers.EqualsOrdinalIgnoreCase(candidateName, "Type"))
        {
            return false;
        }

        for (var index = 0; index < MethodNameHints.Length; index++)
        {
            if (StringHelpers.ContainsOrdinalIgnoreCase(candidateName, MethodNameHints[index]))
            {
                return true;
            }
        }

        return false;
    }

}
