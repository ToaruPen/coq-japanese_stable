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
        "XRL.CharacterBuilds.Qud.UI.QudGamemodeModuleWindow",
        "XRL.CharacterBuilds.Qud.UI.QudMutationsModuleWindow",
    };

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
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

            yield return method;
        }
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
