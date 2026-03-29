using System;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class SkillsAndPowersLineTranslationPatch
{
    private const string Context = nameof(SkillsAndPowersLineTranslationPatch);
    private static readonly Regex LearnedStatusPattern =
        new Regex("^(?<label>.+) \\[(?<owned>\\d+)\\/(?<limit>\\d+)\\]$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex StartingCostPattern =
        new Regex("^(?<label>.+) \\[(?<cost>\\d+) sp\\](?: \\[(?<owned>\\d+)\\/(?<limit>\\d+)\\])?$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("Qud.UI.SkillsAndPowersLine", "SkillsAndPowersLine");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: SkillsAndPowersLineTranslationPatch target type not found.");
            return null;
        }

        var frameworkDataElementType = GameTypeResolver.FindType("XRL.UI.Framework.FrameworkDataElement", "FrameworkDataElement");
        var method = frameworkDataElementType is null
            ? null
            : AccessTools.Method(targetType, "setData", new[] { frameworkDataElementType });
        if (method is null)
        {
            Trace.TraceError("QudJP: SkillsAndPowersLineTranslationPatch.setData(FrameworkDataElement) not found.");
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

            var entry = GetMemberValue(data, "entry");
            if (entry is null)
            {
                return true;
            }

            SetContextData(__instance, data);

            var go = GetMemberValue(data, "go");
            var screen = GetMemberValue(data, "screen");
            var skill = GetMemberValue(entry, "Skill");
            var power = GetMemberValue(entry, "Power");

            SetGameObjectActive(GetMemberValue(__instance, "skillType"), skill is not null);
            SetGameObjectActive(GetMemberValue(__instance, "powerType"), power is not null);

            if (skill is not null)
            {
                ApplySkillRow(__instance, entry, skill, go);
            }
            else
            {
                ApplyPowerRow(__instance, entry, screen, go);
            }

            return false;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: SkillsAndPowersLineTranslationPatch.Prefix failed: {0}", ex);
            return true;
        }
    }

    private static void ApplySkillRow(object instance, object entry, object skill, object? go)
    {
        var expand = GetBoolMemberValue(entry, "Expand");
        var expanderText = expand ? "[-]" : "[+]";
        _ = UITextSkinReflectionAccessor.SetCurrentText(
            GetMemberValue(instance, "skillExpander"),
            expanderText,
            Context);

        var learnedStatus = InvokeMethod(entry, "IsLearned", go)?.ToString() ?? string.Empty;
        var skillName = GetStringMemberValue(entry, "Name");
        if (skillName is null)
        {
            skillName = string.Empty;
        }
        var skillTextSource = BuildSkillTextSource(skillName, learnedStatus);
        var skillRoute = ObservabilityHelpers.ComposeContext(Context, "field=skillText");
        var translatedSkillText = TranslateExactLeaf(skillTextSource, skillRoute);
        OwnerTextSetter.SetTranslatedText(
            GetMemberValue(instance, "skillText"),
            skillTextSource,
            translatedSkillText,
            Context,
            typeof(SkillsAndPowersLineTranslationPatch));

        var cost = GetIntMemberValue(skill, "Cost");
        var availableSkillPoints = go is null ? 0 : GetGameStatValue(go, "SP");
        var totalPowers = CountEnumerable(GetMemberValue(entry, "powers"));
        var learnedPowers = CountLearnedPowers(GetMemberValue(entry, "powers"), go);
        var skillRightColor = availableSkillPoints >= cost ? "g" : "r";
        var skillRightTextSource = BuildSkillRightTextSource(learnedStatus, cost, skillRightColor, learnedPowers, totalPowers);
        var skillRightRoute = ObservabilityHelpers.ComposeContext(Context, "field=skillRightText");
        var translatedSkillRightText = TranslateSkillRightText(
            skillRightTextSource,
            skillRightRoute,
            learnedStatus,
            skillRightColor);
        OwnerTextSetter.SetTranslatedText(
            GetMemberValue(instance, "skillRightText"),
            skillRightTextSource,
            translatedSkillRightText,
            Context,
            typeof(SkillsAndPowersLineTranslationPatch));

        ApplySkillIcon(instance, entry, learnedStatus);
    }

    private static void ApplyPowerRow(object instance, object entry, object? screen, object? go)
    {
        object? screenGo = null;
        if (screen is not null)
        {
            screenGo = GetMemberValue(screen, "GO");
        }

        var modernUiSource = InvokeStringMethod(entry, "ModernUIText", screenGo ?? go);
        var source = modernUiSource ?? string.Empty;
        var route = ObservabilityHelpers.ComposeContext(Context, "field=powerText");
        var translated = TranslateStructuredText(source, route);
        OwnerTextSetter.SetTranslatedText(
            GetMemberValue(instance, "powerText"),
            source,
            translated,
            Context,
            typeof(SkillsAndPowersLineTranslationPatch));
    }

    private static void ApplySkillIcon(object instance, object entry, string learnedStatus)
    {
        var skillIcon = GetMemberValue(instance, "skillIcon");
        var uiIcon = GetMemberValue(entry, "UIIcon");
        if (skillIcon is null || uiIcon is null)
        {
            return;
        }

        var renderable = CloneRenderable(uiIcon);
        if (renderable is null)
        {
            renderable = uiIcon;
        }
        if (string.Equals(learnedStatus, "None", StringComparison.Ordinal))
        {
            SetMemberValue(renderable, "TileColor", "&K");
            SetMemberValue(renderable, "DetailColor", 'k');
        }

        AccessTools.Method(skillIcon.GetType(), "FromRenderable")?.Invoke(skillIcon, new[] { renderable });
    }

    private static string BuildSkillTextSource(string skillName, string learnedStatus)
    {
        return learnedStatus switch
        {
            "Partial" => " {{g|" + skillName + "}}",
            "Learned" => " {{G|" + skillName + "}}",
            _ => skillName,
        };
    }

    private static string BuildSkillRightTextSource(string learnedStatus, int cost, string affordabilityColor, int learnedPowers, int totalPowers)
    {
        if (string.Equals(learnedStatus, "Learned", StringComparison.Ordinal))
        {
            return $"{{{{g|Learned}}}} [{learnedPowers}/{totalPowers}]";
        }

        var source = $"Starting Cost {{{{{affordabilityColor}|[{cost} sp]}}}}";
        if (string.Equals(learnedStatus, "Partial", StringComparison.Ordinal))
        {
            source += $" [{learnedPowers}/{totalPowers}]";
        }

        return source;
    }

    private static string TranslateExactLeaf(string source, string route)
    {
        return SkillsAndPowersStatusScreenTranslationPatch
            .TryTranslateExactLeafPreservingColors(source, route, recordTransform: true)
            .translated;
    }

    private static string TranslateStructuredText(string source, string route)
    {
        var exact = SkillsAndPowersStatusScreenTranslationPatch.TryTranslateExactLeafPreservingColors(
            source,
            route,
            recordTransform: true);
        if (exact.changed)
        {
            return exact.translated;
        }

        return ColorAwareTranslationComposer.TranslatePreservingColors(
            source,
            visible => SkillsAndPowersStatusScreenTranslationPatch.TryTranslateText(visible, route, out var candidate)
                ? candidate
                : visible);
    }

    private static string TranslateSkillRightText(string source, string route, string learnedStatus, string affordabilityColor)
    {
        var stripped = ColorAwareTranslationComposer.Strip(source).stripped;
        if (!SkillsAndPowersStatusScreenTranslationPatch.TryTranslateText(stripped, route, out var translatedPlain))
        {
            return source;
        }

        if (string.Equals(learnedStatus, "Learned", StringComparison.Ordinal))
        {
            var learnedMatch = LearnedStatusPattern.Match(translatedPlain);
            if (!learnedMatch.Success)
            {
                return source;
            }

            return $"{{{{g|{learnedMatch.Groups["label"].Value}}}}} [{learnedMatch.Groups["owned"].Value}/{learnedMatch.Groups["limit"].Value}]";
        }

        var startingCostMatch = StartingCostPattern.Match(translatedPlain);
        if (!startingCostMatch.Success)
        {
            return source;
        }

        var translated = $"{startingCostMatch.Groups["label"].Value} {{{{{affordabilityColor}|[{startingCostMatch.Groups["cost"].Value} sp]}}}}";
        if (startingCostMatch.Groups["owned"].Success)
        {
            translated += $" [{startingCostMatch.Groups["owned"].Value}/{startingCostMatch.Groups["limit"].Value}]";
        }

        return translated;
    }

    private static object? CloneRenderable(object renderable)
    {
        var constructor = AccessTools.Constructor(renderable.GetType(), new[] { renderable.GetType() });
        return constructor?.Invoke(new[] { renderable });
    }

    private static int GetGameStatValue(object go, string statName)
    {
        var value = InvokeMethod(go, "GetStatValue", statName);
        if (value is null)
        {
            value = InvokeMethod(go, "Stat", statName);
        }
        return value is null ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private static int CountLearnedPowers(object? powers, object? go)
    {
        if (powers is not IEnumerable enumerable)
        {
            return 0;
        }

        return enumerable.Cast<object?>().Count(
            power => power is not null
                && string.Equals(InvokeMethod(power, "IsLearned", go)?.ToString(), "Learned", StringComparison.Ordinal));
    }

    private static int CountEnumerable(object? collection)
    {
        if (collection is not IEnumerable enumerable)
        {
            return 0;
        }

        var count = 0;
        foreach (var _ in enumerable)
        {
            count++;
        }

        return count;
    }

    private static void SetGameObjectActive(object? instance, bool active)
    {
        if (instance is null)
        {
            return;
        }

        AccessTools.Method(instance.GetType(), "SetActive", new[] { typeof(bool) })?.Invoke(instance, new object[] { active });
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

    private static string? GetStringMemberValue(object instance, string memberName)
    {
        return GetMemberValue(instance, memberName) as string;
    }

    private static bool GetBoolMemberValue(object instance, string memberName)
    {
        return GetMemberValue(instance, memberName) as bool? ?? false;
    }

    private static int GetIntMemberValue(object instance, string memberName)
    {
        var value = GetMemberValue(instance, memberName);
        return value is null ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private static string? InvokeStringMethod(object instance, string methodName, params object?[]? args)
    {
        return InvokeMethod(instance, methodName, args) as string;
    }

    private static object? InvokeMethod(object instance, string methodName, params object?[]? args)
    {
        return AccessTools.Method(instance.GetType(), methodName)?.Invoke(instance, args);
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
