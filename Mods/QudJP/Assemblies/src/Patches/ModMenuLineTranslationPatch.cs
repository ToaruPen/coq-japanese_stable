using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class ModMenuLineTranslationPatch
{
    private const string Context = nameof(ModMenuLineTranslationPatch);
    private const string TargetTypeName = "Qud.UI.ModMenuLine";
    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName(TargetTypeName);
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type '{1}' not found.", Context, TargetTypeName);
            return null;
        }

        var updateMethod = AccessTools.Method(targetType, "Update", Type.EmptyTypes);
        if (updateMethod is null)
        {
            Trace.TraceError("QudJP: {0}.Update() not found on '{1}'.", Context, TargetTypeName);
        }

        return updateMethod;
    }

    public static void Postfix(MethodBase? __originalMethod, object? __instance)
    {
        try
        {
            if (!string.Equals(__originalMethod?.Name, "Update", StringComparison.Ordinal)
                || __instance is null)
            {
                return;
            }

            var authorText = UiBindingTranslationHelpers.GetMemberValue(__instance, "authorText");
            var current = UITextSkinReflectionAccessor.GetCurrentText(authorText, Context);
            if (string.IsNullOrEmpty(current))
            {
                return;
            }

            var translated = ModManagementSemanticPipeline.TranslateAuthorLabel(current!);
            if (!string.Equals(translated, current, StringComparison.Ordinal))
            {
                DynamicTextObservability.RecordTransform(Context, "ModMenuLine.AuthorText", current!, translated);
                OwnerTextSetter.SetTranslatedText(authorText, current!, translated, Context, typeof(ModMenuLineTranslationPatch));
            }

            TranslateTags(__instance);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }

    private static void TranslateTags(object instance)
    {
        if (UiBindingTranslationHelpers.GetMemberValue(instance, "tags") is not IList tags)
        {
            return;
        }

        for (var index = 0; index < tags.Count; index++)
        {
            if (tags[index] is not string source || string.IsNullOrEmpty(source))
            {
                continue;
            }

            var translated = ModManagementSemanticPipeline.TranslateTagText(source);
            if (!string.Equals(translated, source, StringComparison.Ordinal))
            {
                DynamicTextObservability.RecordTransform(Context, "ModMenuLine.TagText", source, translated);
                tags[index] = translated;
            }
        }
    }

    internal static string TranslateTagText(string source) =>
        ModManagementSemanticPipeline.TranslateTagText(source);

    internal static string TranslateAuthorLabel(string source) =>
        ModManagementSemanticPipeline.TranslateAuthorLabel(source);
}
