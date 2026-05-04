using System;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class GameManagerUpdateSelectedAbilityPatch
{
    private const string Context = nameof(GameManagerUpdateSelectedAbilityPatch);

    private static readonly Regex NonePattern =
        new Regex(
            "^(?<prefix><color=#666666>.*?</color>) (?<none><none>)$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AbilityPattern =
        new Regex(
            "^(?<prefix><color=#666666>.*?</color> )(?<stateOpen><color=#(?:999999|FFFFFF)><color=#FFFF00>.*?</color> )(?<name>.+?)(?<cooldown> \\[(?<turns>\\d+) turns\\])?(?<stateClose></color>)$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("GameManager", "GameManager");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: GameManagerUpdateSelectedAbilityPatch target type not found.");
            return null;
        }

        var method = AccessTools.Method(targetType, "UpdateSelectedAbility", Type.EmptyTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: GameManagerUpdateSelectedAbilityPatch.UpdateSelectedAbility not found.");
        }

        return method;
    }

    public static void Postfix(object? ___selectedAbilityText)
    {
        try
        {
            TranslateSelectedAbilityText(___selectedAbilityText);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: GameManagerUpdateSelectedAbilityPatch.Postfix failed: {0}", ex);
        }
    }

    private static void TranslateSelectedAbilityText(object? textComponent)
    {
        var current = GetCurrentText(textComponent);
        if (current is null)
        {
            if (textComponent is not null)
            {
                Trace.TraceError(
                    "QudJP: {0}.TranslateSelectedAbilityText failed to extract text from component={1}.",
                    Context,
                    GetComponentIdentifier(textComponent));
            }

            return;
        }

        if (current.Length == 0)
        {
            return;
        }

        var route = ObservabilityHelpers.ComposeContext(Context, "field=selectedAbilityText");
        if (!TryTranslateSelectedAbilityText(current!, out var translated)
            || string.Equals(current, translated, StringComparison.Ordinal))
        {
            return;
        }

        if (!SetCurrentText(textComponent, translated))
        {
            Trace.TraceError(
                "QudJP: {0}.TranslateSelectedAbilityText failed to write translated text. component={1}, translated={2}",
                Context,
                GetComponentIdentifier(textComponent),
                translated);
            return;
        }

        DynamicTextObservability.RecordTransform(route, "GameManager.SelectedAbility", current, translated);
    }

    private static bool TryTranslateSelectedAbilityText(string source, out string translated)
    {
        var noneMatch = NonePattern.Match(source);
        if (noneMatch.Success)
        {
            var translatedNone = StringHelpers.TranslateExactOrLowerAscii(noneMatch.Groups["none"].Value);
            if (translatedNone is null)
            {
                translated = source;
                return false;
            }

            translated = noneMatch.Groups["prefix"].Value + " " + translatedNone;
            if (string.Equals(translated, source, StringComparison.Ordinal))
            {
                return false;
            }

            return true;
        }

        var abilityMatch = AbilityPattern.Match(source);
        if (!abilityMatch.Success)
        {
            translated = source;
            return false;
        }

        var nameRoute = ObservabilityHelpers.ComposeContext(Context, "field=selectedAbilityText", "segment=name");
        var sourceName = abilityMatch.Groups["name"].Value;
        var translatedName = ActivatedAbilityNameTranslator.TranslatePreservingColors(
            sourceName,
            nameRoute,
            "GameManager.SelectedAbilityName");
        if (string.Equals(translatedName, sourceName, StringComparison.Ordinal))
        {
            translatedName = GetDisplayNameRouteTranslator.TranslatePreservingColors(sourceName, nameRoute);
        }

        var cooldownGroup = abilityMatch.Groups["cooldown"];
        var translatedCooldown = cooldownGroup.Value;
        if (cooldownGroup.Success)
        {
            translatedCooldown = " " + TranslateCooldownSuffix(abilityMatch.Groups["turns"].Value);
        }

        translated =
            abilityMatch.Groups["prefix"].Value
            + abilityMatch.Groups["stateOpen"].Value
            + translatedName
            + translatedCooldown
            + abilityMatch.Groups["stateClose"].Value;
        if (string.Equals(translated, source, StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    private static string TranslateCooldownSuffix(string turns)
    {
        var template = Translator.Translate("[{0} turns]");
        if (string.Equals(template, "[{0} turns]", StringComparison.Ordinal))
        {
            return "[" + turns + " turns]";
        }

        return template.Replace("{0}", turns);
    }

    private static string? GetCurrentText(object? textComponent)
    {
        if (textComponent is null)
        {
            return null;
        }

        var textProperty = AccessTools.Property(textComponent.GetType(), "text");
        if (textProperty is not null && textProperty.CanRead && textProperty.PropertyType == typeof(string))
        {
            return textProperty.GetValue(textComponent, null) as string;
        }

        var textField = AccessTools.Field(textComponent.GetType(), "text");
        if (textField?.FieldType == typeof(string))
        {
            return textField.GetValue(textComponent) as string;
        }

        var legacyProperty = AccessTools.Property(textComponent.GetType(), "Text");
        if (legacyProperty is not null && legacyProperty.CanRead && legacyProperty.PropertyType == typeof(string))
        {
            return legacyProperty.GetValue(textComponent, null) as string;
        }

        var legacyField = AccessTools.Field(textComponent.GetType(), "Text");
        return legacyField?.FieldType == typeof(string)
            ? legacyField.GetValue(textComponent) as string
            : null;
    }

    private static bool SetCurrentText(object? textComponent, string translated)
    {
        if (textComponent is null)
        {
            return false;
        }

        var textProperty = AccessTools.Property(textComponent.GetType(), "text");
        if (textProperty is not null && textProperty.CanWrite && textProperty.PropertyType == typeof(string))
        {
            textProperty.SetValue(textComponent, translated, null);
            return true;
        }

        var textField = AccessTools.Field(textComponent.GetType(), "text");
        if (textField?.FieldType == typeof(string))
        {
            textField.SetValue(textComponent, translated);
            return true;
        }

        var legacyProperty = AccessTools.Property(textComponent.GetType(), "Text");
        if (legacyProperty is not null && legacyProperty.CanWrite && legacyProperty.PropertyType == typeof(string))
        {
            legacyProperty.SetValue(textComponent, translated, null);
            return true;
        }

        var legacyField = AccessTools.Field(textComponent.GetType(), "Text");
        if (legacyField?.FieldType == typeof(string))
        {
            legacyField.SetValue(textComponent, translated);
            return true;
        }

        return false;
    }

    private static string GetComponentIdentifier(object? textComponent)
    {
        return textComponent?.GetType().FullName ?? "<null>";
    }
}
