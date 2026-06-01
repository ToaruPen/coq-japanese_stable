using System;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class LeftSideCategoryTranslationPatch
{
    private const string Context = nameof(LeftSideCategoryTranslationPatch);
    private static readonly Regex DuplicateWholeColorWrapperPattern = new(
        "^\\{\\{(?<tag>[^|{}]+)\\|\\{\\{\\k<tag>\\|(?<inner>.*)\\}\\}\\}\\}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        return FrameworkDataElementSetDataTargetResolver.Resolve(
            Context,
            "Qud.UI.LeftSideCategory",
            "LeftSideCategory");
    }

    public static void Postfix(object? __instance)
    {
        try
        {
            if (__instance is null)
            {
                return;
            }

            var textSkin = UiBindingTranslationHelpers.GetMemberValue(__instance, "text");
            var current = UITextSkinReflectionAccessor.GetCurrentText(textSkin, Context);
            if (string.IsNullOrEmpty(current))
            {
                return;
            }

            var (stripped, spans) = ColorAwareTranslationComposer.Strip(current);
            if (MessageFrameTranslator.TryStripDirectTranslationMarker(stripped, out var markedText))
            {
                _ = UITextSkinReflectionAccessor.SetCurrentText(
                    textSkin,
                    ColorAwareTranslationComposer.Restore(markedText, spans),
                    Context);
                return;
            }

            var route = ObservabilityHelpers.ComposeContext(Context, "field=text");
            var translated = UiBindingTranslationHelpers.TranslateVisibleText(
                current!,
                route,
                "LeftSideCategory.Text");
            translated = CollapseDuplicateWholeColorWrappers(translated);
            if (string.Equals(translated, current, StringComparison.Ordinal))
            {
                return;
            }

            OwnerTextSetter.SetTranslatedText(
                textSkin,
                current!,
                translated,
                Context,
                typeof(LeftSideCategoryTranslationPatch));
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }

    private static string CollapseDuplicateWholeColorWrappers(string source)
    {
        var collapsed = source;
        while (true)
        {
            var match = DuplicateWholeColorWrapperPattern.Match(collapsed);
            if (!match.Success)
            {
                return collapsed;
            }

            collapsed = "{{" + match.Groups["tag"].Value + "|" + match.Groups["inner"].Value + "}}";
        }
    }
}
