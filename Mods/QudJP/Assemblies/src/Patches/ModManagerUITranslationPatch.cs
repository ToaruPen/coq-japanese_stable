using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class ModManagerUITranslationPatch
{
    private const string Context = nameof(ModManagerUITranslationPatch);
    private const string TargetTypeName = "Qud.UI.ModManagerUI";

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName(TargetTypeName);
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type '{1}' not found.", Context, TargetTypeName);
            return null;
        }

        var method = AccessTools.Method(targetType, "OnSelect");
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.OnSelect(...) not found on '{1}'.", Context, TargetTypeName);
        }

        return method;
    }

    public static void Postfix(object? __instance)
    {
        try
        {
            if (__instance is null)
            {
                return;
            }

            var authorText = UiBindingTranslationHelpers.GetMemberValue(__instance, "SelectedModAuthor");
            var current = UITextSkinReflectionAccessor.GetCurrentText(authorText, Context);
            if (string.IsNullOrEmpty(current))
            {
                return;
            }

            var translated = ModMenuLineTranslationPatch.TranslateAuthorLabel(current!);
            if (!string.Equals(translated, current, StringComparison.Ordinal))
            {
                DynamicTextObservability.RecordTransform(Context, "ModManagerUI.AuthorText", current!, translated);
                OwnerTextSetter.SetTranslatedText(authorText, current!, translated, Context, typeof(ModManagerUITranslationPatch));
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }
}
