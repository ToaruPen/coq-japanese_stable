using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class CharGenLocalizationPatch
{
    private static readonly (string TypeName, string MethodName)[] TargetMethodNames =
    {
        ("XRL.CharacterBuilds.Qud.QudAttributesModule", "DataWarnings"),
        ("XRL.CharacterBuilds.Qud.QudAttributesModule", "DataErrors"),
        ("XRL.CharacterBuilds.Qud.QudMutationsModule", "DataWarnings"),
        ("XRL.CharacterBuilds.Qud.QudMutationsModule", "DataErrors"),
        ("XRL.CharacterBuilds.Qud.QudCyberneticsModule", "DataErrors"),
    };

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        for (var index = 0; index < TargetMethodNames.Length; index++)
        {
            var (typeName, methodName) = TargetMethodNames[index];
            var method = ResolveTargetMethod(typeName, methodName);
            if (method is not null)
            {
                yield return method;
            }
        }
    }

    private static MethodBase? ResolveTargetMethod(string typeName, string methodName)
    {
        var resolvedType = AccessTools.TypeByName(typeName);
        if (resolvedType is null)
        {
            Trace.TraceError("QudJP: CharGenLocalizationPatch target type not found: {0}.", typeName);
            return null;
        }

        var method = AccessTools.Method(resolvedType, methodName, Type.EmptyTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: CharGenLocalizationPatch target method not found: {0}.{1}().", typeName, methodName);
        }

        return method;
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
}
