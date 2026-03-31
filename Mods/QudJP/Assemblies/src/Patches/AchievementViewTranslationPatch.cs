using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class AchievementViewTranslationPatch
{
    private const string Context = nameof(AchievementViewTranslationPatch);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("Qud.UI.AchievementView", "AchievementView");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: AchievementViewTranslationPatch target type not found.");
            return null;
        }

        var method = AccessTools.Method(targetType, "UpdateMenuBars", Type.EmptyTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: AchievementViewTranslationPatch.UpdateMenuBars() not found.");
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

            var hotkeyBar = UiBindingTranslationHelpers.GetMemberValue(__instance, "HotkeyBar");
            var choices = hotkeyBar is null ? null : UiBindingTranslationHelpers.GetMemberValue(hotkeyBar, "choices");
            if (choices is null || choices is string || choices is not IEnumerable enumerable)
            {
                return;
            }

            var index = 0;
            foreach (var choice in enumerable)
            {
                if (choice is null)
                {
                    index++;
                    continue;
                }

                var current = UiBindingTranslationHelpers.GetStringMemberValue(choice, "Description");
                if (string.IsNullOrEmpty(current))
                {
                    index++;
                    continue;
                }

                var route = ObservabilityHelpers.ComposeContext(Context, "field=HotkeyBar.choices[" + index + "].Description");
                var translated = UiBindingTranslationHelpers.TranslateVisibleText(current!, route, "AchievementView.HotkeyBar");
                if (!string.Equals(translated, current, StringComparison.Ordinal))
                {
                    UiBindingTranslationHelpers.SetMemberValue(choice, "Description", translated);
                }

                index++;
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: AchievementViewTranslationPatch.Postfix failed: {0}", ex);
        }
    }
}
