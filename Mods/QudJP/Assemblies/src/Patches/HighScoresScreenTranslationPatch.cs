using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class HighScoresScreenTranslationPatch
{
    private const string Context = nameof(HighScoresScreenTranslationPatch);
    private const string FriendsOnlySuffix = " (friends only)";

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("Qud.UI.HighScoresScreen", "HighScoresScreen");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: HighScoresScreenTranslationPatch target type not found.");
            return null;
        }

        var method = AccessTools.Method(targetType, "Show", Type.EmptyTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: HighScoresScreenTranslationPatch.Show() not found.");
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

            TranslateTitleText(__instance);

            var instanceType = __instance.GetType();
            TranslateStaticMenuOption(instanceType, "ACHIEVEMENTS");
            TranslateStaticMenuOption(instanceType, "LOCAL_SCORES");
            TranslateStaticMenuOption(instanceType, "GLOBAL_DAILY");
            TranslateStaticMenuOption(instanceType, "FRIENDS_DAILY");
            TranslateStaticMenuOption(instanceType, "PREVIOUS_DAY");
            TranslateStaticMenuOption(instanceType, "NEXT_DAY");
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: HighScoresScreenTranslationPatch.Postfix failed: {0}", ex);
        }
    }

    private static void TranslateTitleText(object instance)
    {
        var titleText = UiBindingTranslationHelpers.GetMemberValue(instance, "titleText");
        var current = UITextSkinReflectionAccessor.GetCurrentText(titleText, Context);
        if (string.IsNullOrEmpty(current))
        {
            return;
        }

        var route = ObservabilityHelpers.ComposeContext(Context, "field=titleText");
        var translated = TranslateTitleTextValue(current!, route);
        if (string.Equals(translated, current, StringComparison.Ordinal))
        {
            return;
        }

        OwnerTextSetter.SetTranslatedText(titleText, current!, translated, Context, typeof(HighScoresScreenTranslationPatch));
    }

    private static string TranslateTitleTextValue(string source, string route)
    {
        var exact = StringHelpers.TranslateExactOrLowerAscii(source);
        if (!string.IsNullOrEmpty(exact) && !string.Equals(exact, source, StringComparison.Ordinal))
        {
            DynamicTextObservability.RecordTransform(route, "HighScoresScreen.TitleText", source, exact);
            return exact!;
        }

        var translated = ColorAwareTranslationComposer.TranslatePreservingColors(
            source,
            static visible => TranslateTitleVisibleText(visible));
        if (!string.Equals(translated, source, StringComparison.Ordinal))
        {
            DynamicTextObservability.RecordTransform(route, "HighScoresScreen.TitleText", source, translated);
        }

        return translated;
    }

    private static string TranslateTitleVisibleText(string source)
    {
        if (StringHelpers.TryGetTranslationExactOrLowerAscii(source, out var exact))
        {
            return exact;
        }

        if (source.EndsWith(FriendsOnlySuffix, StringComparison.Ordinal))
        {
            var translatedSuffix = StringHelpers.TranslateExactOrLowerAscii(FriendsOnlySuffix);
            if (!string.IsNullOrEmpty(translatedSuffix))
            {
                return source.Substring(0, source.Length - FriendsOnlySuffix.Length) + translatedSuffix;
            }
        }

        return source;
    }

    private static void TranslateStaticMenuOption(Type instanceType, string fieldName)
    {
        var field = AccessTools.Field(instanceType, fieldName);
        var menuOption = field?.GetValue(null);
        if (menuOption is null)
        {
            return;
        }

        TranslateMenuOptionMember(menuOption, "Description", fieldName + ".Description");
    }

    private static void TranslateMenuOptionMember(object menuOption, string memberName, string routeSuffix)
    {
        var current = UiBindingTranslationHelpers.GetStringMemberValue(menuOption, memberName);
        if (string.IsNullOrEmpty(current))
        {
            return;
        }

        var route = ObservabilityHelpers.ComposeContext(Context, "field=" + routeSuffix);
        var translated = UiBindingTranslationHelpers.TranslateVisibleText(current!, route, "HighScoresScreen.MenuOption");
        if (!string.Equals(translated, current, StringComparison.Ordinal))
        {
            UiBindingTranslationHelpers.SetMemberValue(menuOption, memberName, translated);
        }
    }
}
