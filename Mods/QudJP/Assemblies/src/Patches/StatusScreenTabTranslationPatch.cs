using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class StatusScreenTabTranslationPatch
{
    private delegate void TabPostfix(ref string result);

    private static readonly (string QualifiedName, string FallbackName, TabPostfix Postfix)[] Targets =
    {
        ("Qud.UI.JournalStatusScreen", "JournalStatusScreen", JournalStatusScreenTabTranslationPatch.Postfix),
        ("Qud.UI.MessageLogStatusScreen", "MessageLogStatusScreen", MessageLogStatusScreenTranslationPatch.Postfix),
        ("Qud.UI.QuestsStatusScreen", "QuestsStatusScreen", QuestsStatusScreenTabTranslationPatch.Postfix),
    };

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        for (var index = 0; index < Targets.Length; index++)
        {
            var (qualifiedName, fallbackName, _) = Targets[index];
            var targetType = GameTypeResolver.FindType(qualifiedName, fallbackName);
            if (targetType is null)
            {
                Trace.TraceError($"QudJP: StatusScreenTabTranslationPatch target type not found: {qualifiedName}.");
                continue;
            }

            var method = AccessTools.Method(targetType, "GetTabString", Type.EmptyTypes);
            if (method is null)
            {
                Trace.TraceError($"QudJP: StatusScreenTabTranslationPatch.GetTabString() not found: {qualifiedName}.");
                continue;
            }

            yield return method;
        }
    }

    public static void Postfix(ref string __result, MethodBase __originalMethod)
    {
        try
        {
            var declaringType = __originalMethod.DeclaringType;
            var fullName = declaringType?.FullName;
            var simpleName = declaringType?.Name;
            for (var index = 0; index < Targets.Length; index++)
            {
                if (Targets[index].QualifiedName == fullName || Targets[index].FallbackName == simpleName)
                {
                    Targets[index].Postfix(ref __result);
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: StatusScreenTabTranslationPatch.Postfix failed: {0}", ex);
        }
    }
}
