using System;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class CharacterMutationLineTranslationPatch
{
    private const string Context = nameof(CharacterMutationLineTranslationPatch);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("Qud.UI.CharacterMutationLine", "CharacterMutationLine");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: CharacterMutationLineTranslationPatch target type not found.");
            return null;
        }

        var frameworkDataElementType = GameTypeResolver.FindType("XRL.UI.Framework.FrameworkDataElement", "FrameworkDataElement");
        var method = frameworkDataElementType is null
            ? null
            : AccessTools.Method(targetType, "setData", new[] { frameworkDataElementType });
        if (method is null)
        {
            Trace.TraceError("QudJP: CharacterMutationLineTranslationPatch.setData(FrameworkDataElement) not found.");
        }

        return method;
    }

    public static bool Prefix(object? __instance, object? data)
    {
        try
        {
            if (__instance is null || data is null)
            {
                return true;
            }

            var mutation = GetMemberValue(data, "mutation");
            if (mutation is null)
            {
                return true;
            }

            SetContextData(__instance, data);

            var displayName = InvokeStringMethod(mutation, "GetDisplayName");
            if (displayName is null)
            {
                displayName = string.Empty;
            }
            var source = BuildMutationText(mutation, displayName);
            var route = ObservabilityHelpers.ComposeContext(Context, "field=text");
            var translated = BuildTranslatedMutationText(mutation, displayName, route);
            OwnerTextSetter.SetTranslatedText(
                GetMemberValue(__instance, "text"),
                source,
                translated,
                Context,
                typeof(CharacterMutationLineTranslationPatch));
            return false;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: CharacterMutationLineTranslationPatch.Prefix failed: {0}", ex);
            return true;
        }
    }

    private static string BuildTranslatedMutationText(object mutation, string displayName, string route)
    {
        var translatedName = displayName;
        if (CharacterStatusScreenTextTranslator.TryTranslateUiText(displayName, route, out var exactCandidate))
        {
            translatedName = exactCandidate;
        }

        if (!InvokeBoolMethod(mutation, "ShouldShowLevel"))
        {
            return "{{y|" + translatedName + "}}";
        }

        var currentLevel = GetIntMemberValue(mutation, "Level");
        var visibleSource = $"{displayName} ({currentLevel.ToString(CultureInfo.InvariantCulture)})";
        var visibleTranslated = visibleSource;
        if (CharacterStatusScreenTextTranslator.TryTranslateUiText(visibleSource, route, out var candidate))
        {
            visibleTranslated = candidate;
        }
        else if (!string.Equals(translatedName, displayName, StringComparison.Ordinal))
        {
            visibleTranslated = $"{translatedName} ({currentLevel.ToString(CultureInfo.InvariantCulture)})";
            DynamicTextObservability.RecordTransform(route, "CharacterStatus.MutationLine", visibleSource, visibleTranslated);
        }

        return BuildMutationText(mutation, ExtractMutationName(visibleTranslated));
    }

    private static string ExtractMutationName(string visibleText)
    {
        var separatorIndex = visibleText.LastIndexOf(" (", StringComparison.Ordinal);
        return separatorIndex > 0
            ? visibleText.Substring(0, separatorIndex)
            : visibleText;
    }

    private static string BuildMutationText(object mutation, string displayName)
    {
        if (!InvokeBoolMethod(mutation, "ShouldShowLevel"))
        {
            return "{{y|" + displayName + "}}";
        }

        var currentLevel = GetIntMemberValue(mutation, "Level");
        var baseLevel = GetIntMemberValue(mutation, "BaseLevel");
        var cap = InvokeIntMethod(mutation, "GetMutationCap");
        var levelText = InvokeMethod(mutation, "GetUIDisplayLevel");

        var color = "C";
        if (currentLevel > baseLevel)
        {
            color = currentLevel <= cap ? "G" : "M";
        }
        else if (currentLevel < baseLevel)
        {
            color = "R";
        }

        return $"{{{{y|{displayName} ({{{{{color}|{levelText}}}}})}}}}";
    }

    private static void SetContextData(object instance, object data)
    {
        var context = GetMemberValue(instance, "context");
        if (context is not null)
        {
            SetMemberValue(context, "data", data);
        }
    }

    private static object? GetMemberValue(object instance, string memberName)
    {
        var type = instance.GetType();
        var property = AccessTools.Property(type, memberName);
        if (property is not null && property.CanRead)
        {
            return property.GetValue(instance);
        }

        var field = AccessTools.Field(type, memberName);
        return field?.GetValue(instance);
    }

    private static void SetMemberValue(object instance, string memberName, object? value)
    {
        var type = instance.GetType();
        var property = AccessTools.Property(type, memberName);
        if (property is not null && property.CanWrite)
        {
            property.SetValue(instance, value);
            return;
        }

        var field = AccessTools.Field(type, memberName);
        field?.SetValue(instance, value);
    }

    private static string? InvokeStringMethod(object instance, string methodName)
    {
        return InvokeMethod(instance, methodName) as string;
    }

    private static bool InvokeBoolMethod(object instance, string methodName)
    {
        return InvokeMethod(instance, methodName) as bool? ?? false;
    }

    private static int InvokeIntMethod(object instance, string methodName)
    {
        var value = InvokeMethod(instance, methodName);
        return value is null ? 0 : Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static int GetIntMemberValue(object instance, string memberName)
    {
        var value = GetMemberValue(instance, memberName);
        return value is null ? 0 : Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static object? InvokeMethod(object instance, string methodName)
    {
        return AccessTools.Method(instance.GetType(), methodName)?.Invoke(instance, null);
    }
}
