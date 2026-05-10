using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class CharGenBreadcrumbTranslationPatch
{
    private const string Context = nameof(CharGenBreadcrumbTranslationPatch);

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var type = AccessTools.TypeByName("XRL.CharacterBuilds.EmbarkBuilder");
        if (type is null)
        {
            Trace.TraceWarning("QudJP: {0} target type 'XRL.CharacterBuilds.EmbarkBuilder' not found.", Context);
            yield break;
        }

        var method = AccessTools.Method(type, "GetBreadcrumbs", Type.EmptyTypes);
        if (method is null)
        {
            Trace.TraceWarning("QudJP: {0} method 'GetBreadcrumbs()' not found on '{1}'.", Context, type.FullName);
            yield break;
        }

        yield return method;
    }

    public static IEnumerable? Postfix(IEnumerable? __result)
    {
        try
        {
            return CharGenTextSurface.MaterializeTranslatedBreadcrumbs(__result, Context);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
            return __result;
        }
    }
}
