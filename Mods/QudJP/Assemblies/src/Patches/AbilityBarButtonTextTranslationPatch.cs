using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class AbilityBarButtonTextTranslationPatch
{
    private const string Context = nameof(AbilityBarButtonTextTranslationPatch);
    private static readonly object CacheLock = new object();
    private static readonly Dictionary<Type, FieldInfo?> AbilityButtonsFields = new Dictionary<Type, FieldInfo?>();
    private static readonly Dictionary<Type, FieldInfo?> TextFields = new Dictionary<Type, FieldInfo?>();
    private static readonly Dictionary<Type, MethodInfo?> GetComponentByTypeMethods = new Dictionary<Type, MethodInfo?>();
    private static readonly Dictionary<Type, MethodInfo?> GetComponentGenericMethods = new Dictionary<Type, MethodInfo?>();
    private static readonly Regex DischargeChargePattern = new Regex(
        "^Discharge \\[(?<count>\\d+) charge\\]$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex LaseChargesPattern = new Regex(
        "^Lase \\((?<count>\\d+) charges\\)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex RecoilToZonePattern = new Regex(
        "^Recoil to (?<zone>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static bool abilityBarButtonComponentTypeResolved;
    private static Type? abilityBarButtonComponentType;

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("Qud.UI.AbilityBar", "AbilityBar");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            return null;
        }

        var method = AccessTools.Method(targetType, "Update", Type.EmptyTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.Update not found.", Context);
        }

        return method;
    }

    public static void Postfix(object __instance)
    {
        try
        {
            TranslateAbilityButtons(__instance);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }

    private static void TranslateAbilityButtons(object instance)
    {
        var buttonsField = GetCachedField(AbilityButtonsFields, instance.GetType(), "AbilityButtons");
        if (buttonsField?.GetValue(instance) is not IEnumerable buttons)
        {
            return;
        }

        foreach (var buttonObject in buttons.Cast<object?>())
        {
            var component = ResolveButtonComponent(buttonObject);
            if (component is null)
            {
                continue;
            }

            var textObject = GetCachedField(TextFields, component.GetType(), "Text")?.GetValue(component);
            var current = UITextSkinReflectionAccessor.GetCurrentText(textObject, Context);
            if (string.IsNullOrEmpty(current))
            {
                continue;
            }

            var route = ObservabilityHelpers.ComposeContext(Context, "field=AbilityButtons.Text");
            if (!TryTranslateAbilityButtonText(current!, route, out var translated)
                || string.Equals(current, translated, StringComparison.Ordinal))
            {
                continue;
            }

            if (UITextSkinReflectionAccessor.SetCurrentText(textObject, translated, Context))
            {
                DynamicTextObservability.RecordTransform(route, "AbilityBar.ButtonText", current!, translated);
            }
        }
    }

    private static object? ResolveButtonComponent(object? buttonObject)
    {
        if (buttonObject is null)
        {
            return null;
        }

        if (GetCachedField(TextFields, buttonObject.GetType(), "Text") is not null)
        {
            return buttonObject;
        }

        var componentType = GetAbilityBarButtonComponentType();
        if (componentType is null)
        {
            return null;
        }

        var buttonType = buttonObject.GetType();
        var getComponentByType = GetCachedMethod(GetComponentByTypeMethods, buttonType, static type =>
            AccessTools.Method(type, "GetComponent", new[] { typeof(Type) }));
        if (getComponentByType is not null)
        {
            return getComponentByType.Invoke(buttonObject, new object[] { componentType });
        }

        var getComponentGeneric = GetCachedMethod(GetComponentGenericMethods, buttonType, type =>
            type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(static method =>
                string.Equals(method.Name, "GetComponent", StringComparison.Ordinal)
                && method.IsGenericMethodDefinition
                && method.GetParameters().Length == 0));
        return getComponentGeneric?.MakeGenericMethod(componentType).Invoke(buttonObject, Array.Empty<object>());
    }

    private static FieldInfo? GetCachedField(Dictionary<Type, FieldInfo?> cache, Type type, string fieldName)
    {
        lock (CacheLock)
        {
            if (!cache.TryGetValue(type, out var field))
            {
                field = AccessTools.Field(type, fieldName);
                cache[type] = field;
            }

            return field;
        }
    }

    private static MethodInfo? GetCachedMethod(
        Dictionary<Type, MethodInfo?> cache,
        Type type,
        Func<Type, MethodInfo?> resolve)
    {
        lock (CacheLock)
        {
            if (!cache.TryGetValue(type, out var method))
            {
                method = resolve(type);
                cache[type] = method;
            }

            return method;
        }
    }

    private static Type? GetAbilityBarButtonComponentType()
    {
        if (abilityBarButtonComponentTypeResolved)
        {
            return abilityBarButtonComponentType;
        }

        lock (CacheLock)
        {
            if (!abilityBarButtonComponentTypeResolved)
            {
                abilityBarButtonComponentType = GameTypeResolver.FindType("Qud.UI.AbilityBarButton", "AbilityBarButton");
                abilityBarButtonComponentTypeResolved = true;
            }

            return abilityBarButtonComponentType;
        }
    }

    private static bool TryTranslateAbilityButtonText(string source, string route, out string translated)
    {
        var suffixIndex = source.IndexOf(" {{", StringComparison.Ordinal);
        var nameSegment = suffixIndex >= 0 ? source.Substring(0, suffixIndex) : source;
        var suffix = suffixIndex >= 0 ? source.Substring(suffixIndex) : string.Empty;

        var changed = false;
        var translatedName = TranslateNameSegment(nameSegment, route, out var nameChanged);
        changed |= nameChanged;

        var translatedSuffix = TranslateSuffix(suffix, out var suffixChanged);
        changed |= suffixChanged;

        translated = translatedName + translatedSuffix;
        return changed;
    }

    private static string TranslateNameSegment(string source, string route, out bool changed)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        string? translated;
        if (TryTranslateDynamicAbilityBarName(stripped, route, out var dynamicTranslated))
        {
            translated = dynamicTranslated;
        }
        else
        {
            translated = StringHelpers.TranslateExactOrLowerAscii(stripped);
            if (translated is null)
            {
                translated = TranslateDisplayNameRoutePreservingColors(
                    source,
                    ObservabilityHelpers.ComposeContext(route, "segment=name"));
                changed = !string.Equals(translated, source, StringComparison.Ordinal);
                return translated;
            }
        }

        var restored = spans.Count == 0 ? translated : ColorAwareTranslationComposer.Restore(translated, spans);
        changed = !string.Equals(restored, source, StringComparison.Ordinal);
        return restored;
    }

    private static bool TryTranslateDynamicAbilityBarName(string source, string route, out string translated)
    {
        var dischargeMatch = DischargeChargePattern.Match(source);
        if (dischargeMatch.Success && TryTranslateAbilityBarBaseLeaf("Discharge", out var discharge))
        {
            translated = discharge + " [" + dischargeMatch.Groups["count"].Value + "チャージ]";
            return true;
        }

        var laseMatch = LaseChargesPattern.Match(source);
        if (laseMatch.Success && TryTranslateAbilityBarBaseLeaf("Lase", out var lase))
        {
            translated = lase + " (" + laseMatch.Groups["count"].Value + "チャージ)";
            return true;
        }

        var recoilMatch = RecoilToZonePattern.Match(source);
        if (recoilMatch.Success && TryTranslateAbilityBarBaseLeaf("Recoil", out var recoil))
        {
            var zone = TranslateRecoilZone(recoilMatch.Groups["zone"].Value, route);
            translated = zone + "へ" + recoil;
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateAbilityBarBaseLeaf(string source, out string translated)
    {
        var maybeTranslated = StringHelpers.TranslateExactOrLowerAscii(source);
        if (maybeTranslated is null)
        {
            translated = source;
            return false;
        }

        translated = maybeTranslated;
        return true;
    }

    private static string TranslateRecoilZone(string zone, string route)
    {
        if (GetDisplayNameRouteTranslator.IsAlreadyLocalizedDisplayNameStateText(zone))
        {
            return zone;
        }

        var exact = StringHelpers.TranslateExactOrLowerAscii(zone);
        if (exact is not null)
        {
            return exact;
        }

        var translated = TranslateDisplayNameRoutePreservingColors(
            zone,
            ObservabilityHelpers.ComposeContext(route, "segment=recoil-zone"));
        return string.Equals(translated, zone, StringComparison.Ordinal)
            ? zone
            : translated;
    }

    private static string TranslateDisplayNameRoutePreservingColors(string source, string route)
    {
        return GetDisplayNameRouteTranslator.TranslatePreservingColors(source, route);
    }

    private static string TranslateSuffix(string source, out bool changed)
    {
        var translated = source;
        translated = ReplaceExactToken(translated, "[disabled]");
        translated = ReplaceExactToken(translated, "on");
        translated = ReplaceExactToken(translated, "off");
        changed = !string.Equals(translated, source, StringComparison.Ordinal);
        return translated;
    }

    private static string ReplaceExactToken(string source, string token)
    {
        var translated = StringHelpers.TranslateExactOrLowerAscii(token);
        if (translated is null)
        {
            return source;
        }

        var pattern = token.All(IsAsciiLetterOrDigit)
            ? $@"(?<![A-Za-z0-9]){Regex.Escape(token)}(?![A-Za-z0-9])"
            : Regex.Escape(token);
        return Regex.Replace(source, pattern, translated, RegexOptions.CultureInvariant);
    }

    private static bool IsAsciiLetterOrDigit(char character)
    {
        return (character >= 'A' && character <= 'Z')
            || (character >= 'a' && character <= 'z')
            || (character >= '0' && character <= '9');
    }
}
