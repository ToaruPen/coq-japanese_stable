using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class MessageLogLineTranslationPatch
{
    private const string Context = nameof(MessageLogLineTranslationPatch);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("Qud.UI.MessageLogLine", "MessageLogLine");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: MessageLogLineTranslationPatch target type not found.");
            return null;
        }

        var frameworkDataElementType = GameTypeResolver.FindType("XRL.UI.Framework.FrameworkDataElement", "FrameworkDataElement");
        var method = frameworkDataElementType is null
            ? null
            : AccessTools.Method(targetType, "setData", new[] { frameworkDataElementType });
        if (method is null)
        {
            Trace.TraceError("QudJP: MessageLogLineTranslationPatch.setData(FrameworkDataElement) not found.");
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

            TranslateMenuOptions(__instance.GetType(), "categoryExpandOptions");
            TranslateMenuOptions(__instance.GetType(), "categoryCollapseOptions");
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: MessageLogLineTranslationPatch.Postfix failed: {0}", ex);
        }
    }

    private static void TranslateMenuOptions(Type targetType, string memberName)
    {
        var field = AccessTools.Field(targetType, memberName);
        if (field?.GetValue(null) is not IEnumerable options)
        {
            return;
        }

        var index = 0;
        foreach (var option in options)
        {
            if (option is null)
            {
                index++;
                continue;
            }

            var current = UiBindingTranslationHelpers.GetStringMemberValue(option, "Description");
            if (string.IsNullOrEmpty(current))
            {
                index++;
                continue;
            }

            var route = ObservabilityHelpers.ComposeContext(Context, memberName + "[" + index + "].Description");
            var translated = UiBindingTranslationHelpers.TranslateVisibleText(current!, route, "MessageLogLine.MenuOption");
            if (!string.Equals(translated, current, StringComparison.Ordinal))
            {
                UiBindingTranslationHelpers.SetMemberValue(option, "Description", translated);
            }

            index++;
        }
    }
}
