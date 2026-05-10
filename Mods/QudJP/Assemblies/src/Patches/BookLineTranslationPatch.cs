using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class BookLineTranslationPatch
{
    private const string Context = nameof(BookLineTranslationPatch);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        return FrameworkDataElementSetDataTargetResolver.Resolve(Context, "Qud.UI.BookLine", "BookLine");
    }

    public static bool Prefix(object? __instance, object? data)
    {
        try
        {
            if (__instance is null || data is null)
            {
                return true;
            }

            var source = GetStringMemberValue(data, "text");
            if (source is null)
            {
                return true;
            }

            SetContextData(__instance, data);

            var route = ObservabilityHelpers.ComposeContext(Context, "field=text");
            var translated = TranslateVisibleText(source, route, "Book.LineText");
            var textSkin = GetMemberValue(__instance, "text");
            OwnerTextSetter.SetTranslatedText(
                textSkin,
                source,
                translated,
                Context,
                typeof(BookLineTranslationPatch));
#if HAS_TMP && QUDJP_DEV_BUILD
            if (BookLineGeometryObservability.TryBuildSnapshot(__instance, source, translated, out var logLine))
            {
                RuntimeDiagnostics.LogVerboseProbe(() => logLine!);
            }

            DelayedBookLineGeometryProbeScheduler.ScheduleSnapshot(__instance, source, translated);
#endif
            return false;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: BookLineTranslationPatch.Prefix failed: {0}", ex);
            return true;
        }
    }

    private static string TranslateVisibleText(string source, string route, string family) => UiBindingTranslationHelpers.TranslateVisibleText(source, route, family);

    private static void SetContextData(object instance, object data)
    {
        var context = GetMemberValue(instance, "context");
        if (context is not null)
        {
            SetMemberValue(context, "data", data);
        }
    }

    private static object? GetMemberValue(object instance, string memberName) => UiBindingTranslationHelpers.GetMemberValue(instance, memberName);

    private static string? GetStringMemberValue(object instance, string memberName) => UiBindingTranslationHelpers.GetStringMemberValue(instance, memberName);

    private static void SetMemberValue(object instance, string memberName, object? value) => UiBindingTranslationHelpers.SetMemberValue(instance, memberName, value);
}
