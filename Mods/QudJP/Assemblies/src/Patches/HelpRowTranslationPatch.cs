using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class HelpRowTranslationPatch
{
    private const string Context = nameof(HelpRowTranslationPatch);
    private static readonly Regex TokenPattern =
        new Regex("~(?<command>[A-Za-z0-9_]+)", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("Qud.UI.HelpRow", "HelpRow");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: HelpRowTranslationPatch target type not found.");
            return null;
        }

        var frameworkDataElementType = GameTypeResolver.FindType("XRL.UI.Framework.FrameworkDataElement", "FrameworkDataElement");
        var method = frameworkDataElementType is null
            ? null
            : AccessTools.Method(targetType, "setData", new[] { frameworkDataElementType });
        if (method is null)
        {
            Trace.TraceError("QudJP: HelpRowTranslationPatch.setData(FrameworkDataElement) not found.");
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

            var helpText = GetStringMemberValue(data, "HelpText");
            if (helpText is null)
            {
                return true;
            }

            var categoryDescription = GetMemberValue(__instance, "categoryDescription");
            var description = GetMemberValue(__instance, "description");
            if (categoryDescription is null || description is null)
            {
                Trace.TraceError("QudJP: HelpRowTranslationPatch required UI members not found.");
                return true;
            }

            var rawDescription = GetStringMemberValue(data, "Description") ?? string.Empty;
            var categorySource = rawDescription.ToUpperInvariant();
            var categoryRoute = ObservabilityHelpers.ComposeContext(Context, "field=categoryDescription");
            var translatedCategory = TranslateVisibleText(categorySource, categoryRoute, "HelpRow.CategoryDescription");
            SetAppliedTranslatedText(
                categoryDescription,
                "{{C|" + categorySource + "}}",
                "{{C|" + translatedCategory + "}}",
                typeof(HelpRowTranslationPatch));

            var materializedHelpText = MaterializeHelpText(__instance, helpText);
            var helpRoute = ObservabilityHelpers.ComposeContext(Context, "field=description");
            var translatedHelpText = TranslateVisibleText(materializedHelpText, helpRoute, "HelpRow.HelpText");
            SetAppliedTranslatedText(
                description,
                materializedHelpText,
                translatedHelpText,
                typeof(HelpRowTranslationPatch));

            var collapsed = GetBoolMemberValue(data, "Collapsed");
            var descriptionElement = description;
            TrySetActive(GetMemberValue(descriptionElement, "gameObject"), !collapsed);
            var categoryExpander = GetMemberValue(__instance, "categoryExpander");
            if (categoryExpander is not null)
            {
                _ = UITextSkinReflectionAccessor.SetCurrentText(
                    categoryExpander,
                    collapsed ? "{{C|[+]}}" : "{{C|[-]}}",
                    Context);
                _ = AccessTools.Method(categoryExpander.GetType(), "Apply")?.Invoke(categoryExpander, null);
            }

            return false;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: HelpRowTranslationPatch.Prefix failed: {0}", ex);
            return true;
        }
    }

    private static string MaterializeHelpText(object instance, string source)
    {
        var materialized = source;
#pragma warning disable CA2249
        if (materialized.IndexOf("~", StringComparison.Ordinal) < 0)
#pragma warning restore CA2249
        {
            return materialized;
        }

#pragma warning disable CA2249
        if (!IsGamepadActive(instance) && materialized.IndexOf("~Highlight", StringComparison.Ordinal) >= 0)
#pragma warning restore CA2249
        {
            materialized = materialized.Replace("~Highlight", "{{W|Alt}}");
        }

        foreach (var key in ResolveKeysByLength(instance))
        {
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            var token = "~" + key;
#pragma warning disable CA2249
            if (materialized.IndexOf(token, StringComparison.Ordinal) < 0)
#pragma warning restore CA2249
            {
                continue;
            }

            materialized = materialized.Replace(token, FormatCommandInput(instance, key));
#pragma warning disable CA2249
            if (materialized.IndexOf("~", StringComparison.Ordinal) < 0)
#pragma warning restore CA2249
            {
                break;
            }
        }

#pragma warning disable CA2249
        if (materialized.IndexOf("~", StringComparison.Ordinal) < 0)
#pragma warning restore CA2249
        {
            return materialized;
        }

        return TokenPattern.Replace(materialized, match => FormatCommandInput(instance, match.Groups["command"].Value));
    }

    private static IReadOnlyList<string> ResolveKeysByLength(object instance)
    {
        if (GetMemberValue(instance, "keysByLength") is IEnumerable existing)
        {
            var keys = new List<string>();
            foreach (var item in existing)
            {
                if (item is string key)
                {
                    keys.Add(key);
                }
            }

            if (keys.Count > 0)
            {
                return keys;
            }
        }

        var bindingsType = AccessTools.TypeByName("XRL.UI.CommandBindingManager");
        if (bindingsType is null) { bindingsType = AccessTools.TypeByName("CommandBindingManager"); }
        var commandBindings = bindingsType is null ? null : GetStaticMemberValue(bindingsType, "CommandBindings") as IDictionary;
        var resolved = new List<string>();
        if (commandBindings is not null)
        {
            foreach (var key in commandBindings.Keys)
            {
                if (key is string command)
                {
                    resolved.Add(command);
                }
            }
        }

        resolved.Sort((left, right) => right.Length.CompareTo(left.Length));
        if (resolved.Count > 0)
        {
            SetMemberValue(instance, "keysByLength", resolved);
        }

        return resolved;
    }

    private static string FormatCommandInput(object instance, string command)
    {
        if (GetMemberValue(instance, "formattedBindings") is IDictionary formattedBindings
            && formattedBindings.Contains(command))
        {
            return formattedBindings[command] as string ?? command;
        }

        var controlManagerType = AccessTools.TypeByName("XRL.UI.ControlManager");
        if (controlManagerType is null) { controlManagerType = AccessTools.TypeByName("ControlManager"); }
        var formatter = controlManagerType is null
            ? null
            : AccessTools.Method(controlManagerType, "getCommandInputFormatted", new[] { typeof(string) });
        if (formatter is not null)
        {
            return formatter.Invoke(null, new object[] { command }) as string ?? command;
        }

        return "{{W|" + command + "}}";
    }

    private static bool IsGamepadActive(object instance)
    {
        var memberValue = GetMemberValue(instance, "GamepadActive");
        if (memberValue is bool gamepadActive)
        {
            return gamepadActive;
        }

        var controlManagerType = AccessTools.TypeByName("XRL.UI.ControlManager");
        if (controlManagerType is null) { controlManagerType = AccessTools.TypeByName("ControlManager"); }
        if (controlManagerType is null)
        {
            return false;
        }

        var activeControllerType = GetStaticMemberValue(controlManagerType, "activeControllerType");
        return string.Equals(activeControllerType?.ToString(), "Gamepad", StringComparison.Ordinal);
    }

    private static string TranslateVisibleText(string source, string route, string family)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source;
        }

        var translated = ColorAwareTranslationComposer.TranslatePreservingColors(
            source,
            static visible => StringHelpers.TryGetTranslationExactOrLowerAscii(visible, out var candidate)
                ? candidate
                : visible);
        if (!string.Equals(translated, source, StringComparison.Ordinal))
        {
            DynamicTextObservability.RecordTransform(route, family, source, translated);
        }

        return translated;
    }

    private static void SetAppliedTranslatedText(object? uiTextSkin, string source, string translated, Type patchType)
    {
        if (uiTextSkin is null)
        {
            return;
        }

        var value = translated;
        if (!string.Equals(source, translated, StringComparison.Ordinal)
            && uiTextSkin.GetType().Assembly != patchType.Assembly)
        {
            value = MessageFrameTranslator.MarkDirectTranslation(translated);
        }

        _ = UITextSkinReflectionAccessor.SetCurrentText(uiTextSkin, value, Context);
        _ = AccessTools.Method(uiTextSkin.GetType(), "Apply")?.Invoke(uiTextSkin, null);
    }

    private static void TrySetActive(object? target, bool active)
    {
        if (target is null)
        {
            return;
        }

        if (AccessTools.Method(target.GetType(), "SetActive", new[] { typeof(bool) }) is MethodInfo method)
        {
            _ = method.Invoke(target, new object[] { active });
            return;
        }

        SetMemberValue(target, "activeSelf", active);
        SetMemberValue(target, "Active", active);
    }

    private static object? GetStaticMemberValue(Type type, string memberName)
    {
        var property = AccessTools.Property(type, memberName);
        if (property is not null && property.CanRead)
        {
            return property.GetValue(null);
        }

        var field = AccessTools.Field(type, memberName);
        return field?.GetValue(null);
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

    private static string? GetStringMemberValue(object instance, string memberName)
    {
        return GetMemberValue(instance, memberName) as string;
    }

    private static bool GetBoolMemberValue(object instance, string memberName)
    {
        var value = GetMemberValue(instance, memberName);
        return value is not null && Convert.ToBoolean(value, CultureInfo.InvariantCulture);
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
}
