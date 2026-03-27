using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class CharGenBreadcrumbTranslationPatch
{
    private const string Context = nameof(CharGenBreadcrumbTranslationPatch);

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
        foreach (var typeName in TargetTypeNames)
        {
            var type = AccessTools.TypeByName(typeName);
            if (type is null)
            {
                Trace.TraceWarning("QudJP: {0} target type '{1}' not found.", Context, typeName);
                continue;
            }

            var method = AccessTools.Method(type, "GetBreadcrumb", Type.EmptyTypes);
            if (method is null)
            {
                Trace.TraceWarning("QudJP: {0} method 'GetBreadcrumb()' not found on '{1}'.", Context, typeName);
                continue;
            }

            yield return method;
        }
    }

    public static void Postfix(object __result)
    {
        try
        {
            if (__result is null)
            {
                return;
            }

            CharGenProducerTranslationHelpers.TranslateStringMember(__result, "Title", Context);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }
}
