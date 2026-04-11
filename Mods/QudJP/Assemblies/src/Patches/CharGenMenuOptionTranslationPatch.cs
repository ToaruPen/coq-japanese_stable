using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class CharGenMenuOptionTranslationPatch
{
    private const string Context = nameof(CharGenMenuOptionTranslationPatch);

    private static readonly string[] TargetTypeNames =
    {
        "XRL.CharacterBuilds.Qud.UI.QudAttributesModuleWindow",
        "XRL.CharacterBuilds.Qud.UI.QudBuildLibraryModuleWindow",
        "XRL.CharacterBuilds.Qud.UI.QudBuildSummaryModuleWindow",
        "XRL.CharacterBuilds.Qud.UI.QudChartypeModuleWindow",
        "XRL.CharacterBuilds.Qud.UI.QudChooseStartingLocationModuleWindow",
        "XRL.CharacterBuilds.Qud.UI.QudCustomizeCharacterModuleWindow",
        "XRL.CharacterBuilds.Qud.UI.QudCyberneticsModuleWindow",
        "XRL.CharacterBuilds.Qud.UI.QudGamemodeModuleWindow",
        "XRL.CharacterBuilds.Qud.UI.QudGenotypeModuleWindow",
        "XRL.CharacterBuilds.Qud.UI.QudMutationsModuleWindow",
        "XRL.CharacterBuilds.Qud.UI.QudPregenModuleWindow",
        "XRL.CharacterBuilds.Qud.UI.QudSubtypeModuleWindow",
    };

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var typeName in TargetTypeNames)
        {
            var type = AccessTools.TypeByName(typeName);
            if (type is null)
            {
                Trace.TraceWarning("QudJP: {0} target type '{1}' not found.", Context, typeName);
                continue;
            }

            var method = AccessTools.Method(type, "GetKeyMenuBar", Type.EmptyTypes);
            if (method is null)
            {
                Trace.TraceWarning("QudJP: {0} method 'GetKeyMenuBar()' not found on '{1}'.", Context, typeName);
                continue;
            }

            if (seen.Add(BuildMethodKey(method)))
            {
                yield return method;
            }
        }
    }

    private static string BuildMethodKey(MethodBase method)
    {
        return (method.DeclaringType?.FullName ?? string.Empty)
            + "|"
            + method.Name
            + "|"
            + string.Join(",", Array.ConvertAll(method.GetParameters(), static parameter => parameter.ParameterType.FullName ?? string.Empty));
    }

    public static IEnumerable? Postfix(IEnumerable? values)
    {
        try
        {
            return values is null
                ? values
                : CharGenProducerTranslationHelpers.MaterializeTranslatedEnumerable(values, "Description", Context);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
            return values;
        }
    }
}
