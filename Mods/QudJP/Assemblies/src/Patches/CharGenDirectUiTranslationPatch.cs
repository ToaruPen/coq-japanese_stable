using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;
using QudJP;

namespace QudJP.Patches;

[HarmonyPatch]
public static class CharGenDirectUiTranslationPatch
{
    private const string Context = nameof(CharGenDirectUiTranslationPatch);
    private const string AttributeBonusFamily = Context + ".AttributeBonusSource";
    private const string AttributePointCostFamily = Context + ".AttributePointCost";
    private const string SubtypeTitleFamily = Context + ".SubtypeTitle";

    private static readonly Regex AttributeBonusLinePattern = new(
        "^(?<bonus>[+-]\\d+) from (?<source>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var attributeControlType = AccessTools.TypeByName("XRL.CharacterBuilds.Qud.UI.AttributeSelectionControl");
        if (attributeControlType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found: XRL.CharacterBuilds.Qud.UI.AttributeSelectionControl.", Context);
        }
        else
        {
            var updated = AccessTools.Method(attributeControlType, "Updated", Type.EmptyTypes);
            if (updated is null)
            {
                Trace.TraceError("QudJP: {0} target method not found: AttributeSelectionControl.Updated().", Context);
            }
            else
            {
                yield return updated;
            }
        }

        var subtypeWindowType = AccessTools.TypeByName("XRL.CharacterBuilds.Qud.UI.QudSubtypeModuleWindow");
        var descriptorType = AccessTools.TypeByName("XRL.CharacterBuilds.EmbarkBuilderModuleWindowDescriptor");
        if (subtypeWindowType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found: XRL.CharacterBuilds.Qud.UI.QudSubtypeModuleWindow.", Context);
        }

        if (descriptorType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found: XRL.CharacterBuilds.EmbarkBuilderModuleWindowDescriptor.", Context);
        }

        if (subtypeWindowType is not null && descriptorType is not null)
        {
            var beforeShow = AccessTools.Method(subtypeWindowType, "BeforeShow", new[] { descriptorType });
            if (beforeShow is null)
            {
                Trace.TraceError("QudJP: {0} target method not found: QudSubtypeModuleWindow.BeforeShow(EmbarkBuilderModuleWindowDescriptor).", Context);
            }
            else
            {
                yield return beforeShow;
            }
        }
    }

    public static void Postfix(object? __instance, MethodBase __originalMethod)
    {
        try
        {
            var declaringTypeName = __originalMethod.DeclaringType?.FullName ?? string.Empty;
            if (string.Equals(declaringTypeName, "XRL.CharacterBuilds.Qud.UI.AttributeSelectionControl", StringComparison.Ordinal)
                && string.Equals(__originalMethod.Name, "Updated", StringComparison.Ordinal))
            {
                TranslateAttributeSelectionControl(__instance);
                return;
            }

            if (string.Equals(declaringTypeName, "XRL.CharacterBuilds.Qud.UI.QudSubtypeModuleWindow", StringComparison.Ordinal)
                && string.Equals(__originalMethod.Name, "BeforeShow", StringComparison.Ordinal))
            {
                TranslateQudSubtypeModuleWindow(__instance);
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }

    internal static void TranslateAttributeSelectionControlForTests(object? instance)
    {
        TranslateAttributeSelectionControl(instance);
    }

    internal static void TranslateQudSubtypeModuleWindowForTests(object? instance)
    {
        TranslateQudSubtypeModuleWindow(instance);
    }

    private static void TranslateAttributeSelectionControl(object? instance)
    {
        if (instance is null || !TryGetMemberValue(instance, "data", out var data) || data is null)
        {
            return;
        }

        TranslateAttributeBonusTooltip(instance, data);
        TranslateAttributePointCost(instance, data);
    }

    private static void TranslateAttributeBonusTooltip(object instance, object data)
    {
        if (!TryGetStringMemberValue(data, "BonusSource", out var source)
            || string.IsNullOrEmpty(source)
            || !TryGetMemberValue(instance, "tooltip", out var tooltip)
            || tooltip is null)
        {
            return;
        }

        var translated = TranslateAttributeBonusSource(source!);
        if (string.Equals(translated, source, StringComparison.Ordinal))
        {
            return;
        }

        DynamicTextObservability.RecordTransform(Context, AttributeBonusFamily, source, translated);
        InvokeSetText(tooltip, "BodyText", FormatToRtf(translated));
    }

    private static void TranslateAttributePointCost(object instance, object data)
    {
        if (!TryGetIntMemberValue(data, "APToRaise", out var cost))
        {
            return;
        }

        var source = "[" + cost + "pts]";
        var translated = CharGenProducerTranslationHelpers.TranslateText(source);
        if (string.Equals(translated, source, StringComparison.Ordinal))
        {
            return;
        }

        var titledIconButton = ResolveTitledIconButton(instance);
        if (titledIconButton is null)
        {
            return;
        }

        DynamicTextObservability.RecordTransform(Context, AttributePointCostFamily, source, translated);
        InvokeStringMethod(titledIconButton, "SetTitle", translated);
    }

    private static object? ResolveTitledIconButton(object instance)
    {
        if (TryGetMemberValue(instance, "TitleButton", out var testButton) && testButton is not null)
        {
            return testButton;
        }

        var titledIconButtonType = AccessTools.TypeByName("XRL.UI.Framework.TitledIconButton");
        if (titledIconButtonType is null)
        {
            return null;
        }

        var getComponent = AccessTools.Method(instance.GetType(), "GetComponent", Type.EmptyTypes);
        if (getComponent is null || !getComponent.IsGenericMethodDefinition)
        {
            return null;
        }

        var genericGetComponent = getComponent.MakeGenericMethod(titledIconButtonType);
        return genericGetComponent.Invoke(instance, null);
    }

    private static void TranslateQudSubtypeModuleWindow(object? instance)
    {
        if (instance is null
            || !TryGetMemberValue(instance, "prefabComponent", out var prefabComponent)
            || prefabComponent is null
            || !TryGetMemberValue(prefabComponent, "titleText", out var titleText)
            || titleText is null)
        {
            return;
        }

        var currentTitle = TryGetStringMemberValue(titleText, "text", out var current)
            ? current
            : null;
        if (string.IsNullOrEmpty(currentTitle))
        {
            currentTitle = TryInvokeStringMethod(instance, "getSubtypeTitle");
            if (!string.IsNullOrEmpty(currentTitle))
            {
                currentTitle = ":" + currentTitle + ":";
            }
        }

        if (string.IsNullOrEmpty(currentTitle))
        {
            return;
        }

        var translated = TranslateColonWrappedTitle(currentTitle!);
        if (string.Equals(translated, currentTitle, StringComparison.Ordinal))
        {
            return;
        }

        DynamicTextObservability.RecordTransform(Context, SubtypeTitleFamily, currentTitle, translated);
        InvokeSetText(titleText, translated);
    }

    private static string TranslateAttributeBonusSource(string source)
    {
        if (source.Length == 0 || source[0] == '\u0001')
        {
            return source;
        }

        var lines = source.Split('\n');
        var builder = new StringBuilder(source.Length);
        var changed = false;

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            if (line.Length == 0)
            {
                if (index < lines.Length - 1)
                {
                    builder.Append('\n');
                }

                continue;
            }

            var match = AttributeBonusLinePattern.Match(line);
            if (!match.Success)
            {
                builder.Append(line);
            }
            else
            {
                var bonus = match.Groups["bonus"].Value;
                var sourceName = match.Groups["source"].Value;
                var translatedSourceName = CharGenProducerTranslationHelpers.TranslateText(sourceName);
                if (string.Equals(translatedSourceName, sourceName, StringComparison.Ordinal))
                {
                    builder.Append(line);
                }
                else
                {
                    builder.Append(translatedSourceName);
                    builder.Append("による ");
                    builder.Append(bonus);
                    changed = true;
                }
            }

            if (index < lines.Length - 1)
            {
                builder.Append('\n');
            }
        }

        return changed ? builder.ToString() : source;
    }

    private static string TranslateColonWrappedTitle(string source)
    {
        if (source.Length >= 2
            && source[0] == ':'
            && source[source.Length - 1] == ':')
        {
            var inner = source.Substring(1, source.Length - 2);
            var translatedInner = CharGenProducerTranslationHelpers.TranslateText(inner);
            return string.Equals(translatedInner, inner, StringComparison.Ordinal)
                ? source
                : "：" + translatedInner + "：";
        }

        return CharGenProducerTranslationHelpers.TranslateText(source);
    }

    private static string FormatToRtf(string source)
    {
        var sidebarType = AccessTools.TypeByName("XRL.UI.Sidebar");
        var formatMethod = sidebarType is null
            ? null
            : AccessTools.Method(sidebarType, "FormatToRTF", new[] { typeof(string) });
        return formatMethod?.Invoke(null, new object[] { source }) as string ?? source;
    }

    private static void InvokeSetText(object target, string value)
    {
        InvokeStringMethod(target, "SetText", value);
    }

    private static void InvokeSetText(object target, string key, string value)
    {
        var method = AccessTools.Method(target.GetType(), "SetText", new[] { typeof(string), typeof(string) });
        if (method is null)
        {
            Trace.TraceWarning("QudJP: {0} could not find SetText(string, string) on {1}.", Context, target.GetType().FullName);
            return;
        }

        method.Invoke(target, new object[] { key, value });
    }

    private static void InvokeStringMethod(object target, string methodName, string value)
    {
        var method = AccessTools.Method(target.GetType(), methodName, new[] { typeof(string) });
        if (method is null)
        {
            Trace.TraceWarning("QudJP: {0} could not find {1}(string) on {2}.", Context, methodName, target.GetType().FullName);
            return;
        }

        method.Invoke(target, new object[] { value });
    }

    private static string? TryInvokeStringMethod(object target, string methodName)
    {
        var method = AccessTools.Method(target.GetType(), methodName, Type.EmptyTypes);
        return method?.Invoke(target, null) as string;
    }

    private static bool TryGetStringMemberValue(object target, string memberName, out string? value)
    {
        value = null;
        if (!TryGetMemberValue(target, memberName, out var raw))
        {
            return false;
        }

        value = raw as string;
        return true;
    }

    private static bool TryGetIntMemberValue(object target, string memberName, out int value)
    {
        value = 0;
        if (!TryGetMemberValue(target, memberName, out var raw) || raw is not int intValue)
        {
            return false;
        }

        value = intValue;
        return true;
    }

    private static bool TryGetMemberValue(object target, string memberName, out object? value)
    {
        var type = target.GetType();

        var field = AccessTools.Field(type, memberName);
        if (field is not null)
        {
            value = field.GetValue(target);
            return true;
        }

        var property = AccessTools.Property(type, memberName);
        if (property is not null && property.CanRead)
        {
            value = property.GetValue(target);
            return true;
        }

        value = null;
        return false;
    }
}
