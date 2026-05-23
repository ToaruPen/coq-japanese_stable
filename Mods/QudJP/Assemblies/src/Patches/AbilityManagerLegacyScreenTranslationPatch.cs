using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

public static class AbilityManagerLegacyScreenTranslationPatch
{
    private const string Context = nameof(AbilityManagerLegacyScreenTranslationPatch);

    private static readonly Regex CooldownTagPattern = new(
        "\\[\\{\\{C\\|(?<rounds>.+?)\\}\\} turn cooldown(?<astral>, astrally tethered)?\\]",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AbilityRowNamePattern = new(
        "^(?<prefix>(?:\\{\\{K\\|)?\\s*(?:\\{\\{K\\|[^}]+\\}\\}|.)\\)\\s+)(?<name>.+?)(?<suffix>\\s+(?:\\[|\\{\\{Y\\|<).*)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex CategoryMarkupPattern = new(
        "^\\{\\{W\\|(?<category>.+?)\\}\\}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex CooldownDetailPattern = new(
        "^Cooldown: (?<value>.+?) rounds?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly MethodInfo TranslateLiteralMethod =
        AccessTools.Method(typeof(AbilityManagerLegacyScreenTranslationPatch), nameof(TranslateLiteral))
        ?? throw new InvalidOperationException("TranslateLiteral method not found.");

    private static readonly MethodInfo TranslateRenderedMethod =
        AccessTools.Method(typeof(AbilityManagerLegacyScreenTranslationPatch), nameof(TranslateRendered))
        ?? throw new InvalidOperationException("TranslateRendered method not found.");

    private static readonly Dictionary<string, string> AbilityFallbackTranslations = new(StringComparer.Ordinal)
    {
        ["Maneuvers"] = "戦技",
        ["Sprint"] = "スプリント",
        ["Teleport"] = "テレポート",
    };

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        try
        {
            var rewritten = new List<CodeInstruction>();
            foreach (var instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Ldstr && instruction.operand is string)
                {
                    rewritten.Add(instruction);
                    rewritten.Add(new CodeInstruction(OpCodes.Call, TranslateLiteralMethod));
                    continue;
                }

                if (IsTranslatableStringSink(instruction))
                {
                    var translateInstruction = new CodeInstruction(OpCodes.Call, TranslateRenderedMethod)
                    {
                        labels = instruction.labels,
                        blocks = instruction.blocks,
                    };

                    instruction.labels = new List<Label>();
                    instruction.blocks = new List<ExceptionBlock>();

                    rewritten.Add(translateInstruction);
                    rewritten.Add(instruction);
                    continue;
                }

                rewritten.Add(instruction);
            }

            return rewritten;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Transpiler failed: {1}", Context, ex);
            return instructions;
        }
    }

    public static string TranslateLiteral(string source)
    {
        try
        {
            return TranslateRenderedCore(source);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.TranslateLiteral failed: {1}", Context, ex);
            return source;
        }
    }

    public static string TranslateRendered(string source)
    {
        try
        {
            return TranslateRenderedCore(source);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.TranslateRendered failed: {1}", Context, ex);
            return source;
        }
    }

    private static string TranslateRenderedCore(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var stripped))
        {
            return stripped;
        }

        var translated = TranslateExactChrome(source);
        translated = TranslateAbilityRowName(translated);
        translated = TranslateCategoryMarkup(translated);
        if (!string.Equals(translated, source, StringComparison.Ordinal)
            || !LooksLikeDetailsLine(source))
        {
            return translated;
        }

        return TranslateStructuredDetailLine(source);
    }

    private static string TranslateExactChrome(string source)
    {
        var translated = source switch
        {
            "[ {{W|Manage Abilities}} ]" => "[ {{W|能力管理}} ]",
            "{{W|<More...>}}" => "{{W|<続き…>}}",
            "<More...>" => "<続き…>",
            _ => source,
        };

        translated = translated.Replace("-exit", "-終了");
        translated = translated.Replace("-Use Ability", "-能力を使用");
        translated = translated.Replace("-Map key", "-キー割り当て");
        translated = translated.Replace("-unbind", "-キー解除");
        translated = translated.Replace("-Change Order", "-順序変更");
        translated = translated.Replace("-custom", "-任意");
        translated = translated.Replace("-by class", "-クラス別");
        translated = translated.Replace("[{{W|attack}}]", "[{{W|攻撃}}]");
        translated = translated.Replace("[attack]", "[攻撃]");
        translated = translated.Replace("[disabled]", "[無効]");
        translated = translated.Replace("[astrally tethered]", "[アストラル束縛]");
        translated = translated.Replace("{{K|[{{g|Toggled on}}]}}", "{{K|[{{g|オン}}]}}");
        translated = translated.Replace("{{K|[{{y|Toggled off}}]}}", "{{K|[{{y|オフ}}]}}");

        return CooldownTagPattern.Replace(
            translated,
            static match => "[{{C|" + match.Groups["rounds"].Value + "}}ターンのクールダウン"
                            + (match.Groups["astral"].Success ? "、アストラル束縛" : string.Empty)
                            + "]");
    }

    private static string TranslateAbilityRowName(string source)
    {
        var match = AbilityRowNamePattern.Match(source);
        if (!match.Success)
        {
            return source;
        }

        var name = match.Groups["name"].Value;
        var translatedName = TranslateAbilityOrFragment(name);
        return string.Equals(translatedName, name, StringComparison.Ordinal)
            ? source
            : match.Groups["prefix"].Value + translatedName + match.Groups["suffix"].Value;
    }

    private static string TranslateCategoryMarkup(string source)
    {
        var match = CategoryMarkupPattern.Match(source);
        if (!match.Success)
        {
            return source;
        }

        var category = match.Groups["category"].Value;
        var translatedCategory = TranslateAbilityOrFragment(category);
        return string.Equals(translatedCategory, category, StringComparison.Ordinal)
            ? source
            : "{{W|" + translatedCategory + "}}";
    }

    private static string TranslateStructuredDetailLine(string source)
    {
        try
        {
            var cooldown = CooldownDetailPattern.Match(source);
            if (cooldown.Success)
            {
                return "クールダウン: " + cooldown.Groups["value"].Value + "ラウンド";
            }

            var detail = SkillsAndPowersStatusScreenTranslationPatch.TryTranslateDetailText(
                source,
                Context + ".LegacyDetails",
                recordTransform: false);
            if (detail.changed)
            {
                return detail.translated;
            }

            return StringHelpers.TryGetTranslationExactOrLowerAscii(source, out var exact)
                ? exact
                : source;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.TranslateStructuredDetailLine failed: {1}", Context, ex);
            return source;
        }
    }

    private static bool LooksLikeDetailsLine(string source)
    {
        return ContainsOrdinal(source, ":") || ContainsOrdinal(source, "\n");
    }

    private static bool ContainsOrdinal(string source, string value)
    {
        return source.Contains(value);
    }

    private static string TranslateAbilityOrFragment(string source)
    {
        if (AbilityFallbackTranslations.TryGetValue(source, out var fallback))
        {
            return fallback;
        }

        try
        {
            if (ActivatedAbilityNameTranslator.TryTranslateVisibleName(source, out var abilityName)
                && !string.Equals(abilityName, source, StringComparison.Ordinal))
            {
                return abilityName;
            }

            return StringHelpers.TryGetTranslationExactOrLowerAscii(source, out var exact)
                ? exact
                : source;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.TranslateAbilityOrFragment failed: {1}", Context, ex);
            return source;
        }
    }

    private static bool IsTranslatableStringSink(CodeInstruction instruction)
    {
        if ((instruction.opcode != OpCodes.Call && instruction.opcode != OpCodes.Callvirt)
            || instruction.operand is not MethodInfo method)
        {
            return false;
        }

        var parameters = method.GetParameters();
        return (method.ReturnType == typeof(void)
                && ((string.Equals(method.Name, "Write", StringComparison.Ordinal)
                     && parameters.Length == 1
                     && parameters[0].ParameterType == typeof(string))
                    || (string.Equals(method.Name, "WriteAt", StringComparison.Ordinal)
                        && parameters.Length == 3
                        && parameters[0].ParameterType == typeof(int)
                        && parameters[1].ParameterType == typeof(int)
                        && parameters[2].ParameterType == typeof(string))))
               || (method.ReturnType == typeof(string)
                   && string.Equals(method.Name, "StripFormatting", StringComparison.Ordinal)
                   && parameters.Length == 1
                   && parameters[0].ParameterType == typeof(string));
    }
}
