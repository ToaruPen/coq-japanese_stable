using System;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

internal static class CyberneticsTerminalTextTranslator
{
    private const string Context = nameof(CyberneticsTerminalTextTranslator);

    private static readonly string[] ExactReplacementKeys =
    {
        "Become a finer Aristocrat. Upgrade your license tier with cybernetics credits.\n\n{{C|1}} credit for license tiers 1-8\n{{C|2}} credits for license tiers 9-16\n{{C|3}} credits for license tiers 17-24\n{{C|4}} credits for license tiers 25+\n",
        "Your curiosity is admirable, aristocrat.\n\nCybernetics are bionic augmentations implanted in your body to assist in your self-actualization. You can have implants installed at becoming nooks such as this one. Either load them in the rack or carry them on your person.",
        "Insightful question, Aristocrat.\n\nEach implant has a point cost, and the total point cost of your installed implants can't exceed your license tier (displayed at the bottom of this screen). You can upgrade your license at a nook by spending cybernetic credits.",
        "Aristocrat, your question was most finely uttered.\n\nYou may freely install and uninstall implants at a nook, though some implants are destroyed when uninstalled or can't be uninstalled at all.",
        "You are becoming, aristocrat. Choose an implant to install.",
        "You are given to whimsy, Aristocrat. Choose an implant to uninstall.",
        "Select who is to Become, aristocrat.",
        "Please choose a target body part.",
        "Whimsy must yield to necessity, Aristocrat.",
        "You are no aristocrat. Goodbye.",
        "How many implants can I install?",
        "Can I uninstall implants?",
        "Learn About Cybernetics",
        "Install Cybernetics",
        "Uninstall Cybernetics",
        "Upgrade Your License",
        "Select Subject",
        "Return To Main Menu",
        "Return to main menu",
        "<cancel operation, return to main menu>",
        "<back>",
        "...",
        "\n\n{{R|<No implants available>}}",
        "\n\n{{R|<no implants installed>}}",
        "[already installed]",
        "[cannot be uninstalled]",
        "[destroyed on uninstall]",
        "insufficent credits",
        "{{R|Insufficient credits to upgrade}}",
        "Interfacing with nervous system...................\n",
        ".....Complete!\n\n",
        "Congratulations! Your cybernetic implant was successfully installed.\n",
        "Congratulations! Your cybernetic implant was successfully uninstalled.",
        "You are becoming.",
        "You are Becoming, Aristocrat.",
        "!Error: cannot unequip that limb",
    };

    private static readonly Regex MainMenuTextPattern = new Regex(
        "^Welcome, Aristocrat, to a becoming nook\\. (?<subject>.+) one step closer to the Grand Unification\\. Please choose from the following options\\.$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex AlreadyPresentPattern = new Regex(
        "^Cybernetics already present:\\n  \\-(?<item>.+)\\n\\nPlease uninstall existing implant\\.$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex InsufficientLicensePattern = new Regex(
        "^Insufficent license points to install:\\n  \\-(?<item>.+)\\n\\nPlease uninstall an implant or upgrade your license\\.$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ConditionInadequatePattern = new Regex(
        "^Error: Condition inadequate for installation\\n  \\-(?<item>.+)\\n\\nPlease supply a replacement\\.$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ReminderPattern = new Regex(
        "Remember, Aristocrat, your base license tier is (?<tier>\\{\\{C\\|\\d+\\}\\})\\.",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex LicensePointsPattern = new Regex(
        "\\[(?<points>\\d+) license point(?:s)?\\]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex CreditBracketPattern = new Regex(
        "\\[(?<amount>\\{\\{[^|]+\\|\\d+\\}\\}) (?<unit>credit|credits)\\]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex WillReplacePattern = new Regex(
        " \\[will replace (?<item>[^\\]]+)\\]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex InstallingLinePattern = new Regex(
        "Installing (?<item>.+?)(?<suffix>\\.+\\n)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex UninstallingLinePattern = new Regex(
        "Uninstalling (?<item>.+?)(?<suffix>\\.+\\n)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly MethodInfo? LeetMethod = ResolveLeetMethod();

    public static void TranslateScreen(object? screen)
    {
        if (screen is null || !IsCyberneticsScreen(screen))
        {
            return;
        }

        try
        {
            TranslateMainText(screen);
            TranslateOptions(screen);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.TranslateScreen failed: {1}", Context, ex);
        }
    }

    private static void TranslateMainText(object screen)
    {
        var current = UiBindingTranslationHelpers.GetStringMemberValue(screen, "MainText");
        if (string.IsNullOrEmpty(current))
        {
            return;
        }

        var translated = TranslateText(current!);
        if (string.Equals(translated, current, StringComparison.Ordinal))
        {
            return;
        }

        UiBindingTranslationHelpers.SetMemberValue(screen, "MainText", translated);
        DynamicTextObservability.RecordTransform(
            ObservabilityHelpers.ComposeContext(Context, "field=MainText"),
            "CyberneticsTerminal.MainText",
            current!,
            translated);
    }

    private static void TranslateOptions(object screen)
    {
        if (UiBindingTranslationHelpers.GetMemberValue(screen, "Options") is not IList options)
        {
            return;
        }

        for (var index = 0; index < options.Count; index++)
        {
            if (options[index] is not string current || current.Length == 0)
            {
                continue;
            }

            var translated = TranslateText(current);
            if (string.Equals(translated, current, StringComparison.Ordinal))
            {
                continue;
            }

            options[index] = translated;
            DynamicTextObservability.RecordTransform(
                ObservabilityHelpers.ComposeContext(Context, "field=Options[" + index + "]"),
                "CyberneticsTerminal.OptionText",
                current,
                translated);
        }
    }

    private static string TranslateText(string source)
    {
        if (TryTranslateExact(source, out var exact))
        {
            return exact;
        }

        var knownLeet = TranslateKnownLeet(source);
        if (!string.Equals(knownLeet, source, StringComparison.Ordinal))
        {
            return knownLeet;
        }

        var translated = ApplyWholeMatchTemplates(source);
        translated = ApplyRegexTemplates(translated);
        translated = ApplyExactReplacements(translated);
        return translated;
    }

    private static string ApplyWholeMatchTemplates(string source)
    {
        if (TryApplyWholeMatchTemplate(
                source,
                MainMenuTextPattern,
                "Welcome, Aristocrat, to a becoming nook. {0} one step closer to the Grand Unification. Please choose from the following options.",
                static match => new object[] { match.Groups["subject"].Value },
                out var translated))
        {
            return translated;
        }

        if (TryApplyWholeMatchTemplate(
                source,
                AlreadyPresentPattern,
                "Cybernetics already present:\n  -{0}\n\nPlease uninstall existing implant.",
                static match => new object[] { match.Groups["item"].Value },
                out translated))
        {
            return translated;
        }

        if (TryApplyWholeMatchTemplate(
                source,
                InsufficientLicensePattern,
                "Insufficent license points to install:\n  -{0}\n\nPlease uninstall an implant or upgrade your license.",
                static match => new object[] { match.Groups["item"].Value },
                out translated))
        {
            return translated;
        }

        if (TryApplyWholeMatchTemplate(
                source,
                ConditionInadequatePattern,
                "Error: Condition inadequate for installation\n  -{0}\n\nPlease supply a replacement.",
                static match => new object[] { match.Groups["item"].Value },
                out translated))
        {
            return translated;
        }

        return source;
    }

    private static string ApplyRegexTemplates(string source)
    {
        source = ApplyRegexTemplate(
            source,
            ReminderPattern,
            "Remember, Aristocrat, your base license tier is {0}.",
            static match => new object[] { match.Groups["tier"].Value });

        source = ApplyRegexTemplate(
            source,
            LicensePointsPattern,
            "[{0} license points]",
            static match => new object[] { match.Groups["points"].Value });

        source = ApplyRegexTemplate(
            source,
            CreditBracketPattern,
            "[{0} credits]",
            static match => new object[] { match.Groups["amount"].Value });

        source = ApplyRegexTemplate(
            source,
            WillReplacePattern,
            " [will replace {0}]",
            static match => new object[] { match.Groups["item"].Value });

        source = ApplyRegexTemplate(
            source,
            InstallingLinePattern,
            "Installing {0}{1}",
            static match => new object[] { match.Groups["item"].Value, match.Groups["suffix"].Value });

        source = ApplyRegexTemplate(
            source,
            UninstallingLinePattern,
            "Uninstalling {0}{1}",
            static match => new object[] { match.Groups["item"].Value, match.Groups["suffix"].Value });

        return source;
    }

    private static string ApplyExactReplacements(string source)
    {
        for (var index = 0; index < ExactReplacementKeys.Length; index++)
        {
            var key = ExactReplacementKeys[index];
            if (source.IndexOf(key, StringComparison.Ordinal) < 0)
            {
                continue;
            }

            if (!TryTranslateExact(key, out var replacement))
            {
                continue;
            }

            source = source.Replace(key, replacement);
        }

        return source;
    }

    private static bool TryApplyWholeMatchTemplate(
        string source,
        Regex pattern,
        string templateKey,
        Func<Match, object[]> buildArgs,
        out string translated)
    {
        var match = pattern.Match(source);
        if (!match.Success || !TryGetTemplate(templateKey, out var template))
        {
            translated = source;
            return false;
        }

        translated = string.Format(CultureInfo.InvariantCulture, template, buildArgs(match));
        return true;
    }

    private static string ApplyRegexTemplate(
        string source,
        Regex pattern,
        string templateKey,
        Func<Match, object[]> buildArgs)
    {
        if (!TryGetTemplate(templateKey, out var template) || !pattern.IsMatch(source))
        {
            return source;
        }

        return pattern.Replace(
            source,
            match => string.Format(CultureInfo.InvariantCulture, template, buildArgs(match)));
    }

    private static bool TryTranslateExact(string source, out string translated)
    {
        if (Translator.TryGetTranslation(source, out translated)
            && !string.Equals(translated, source, StringComparison.Ordinal))
        {
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryGetTemplate(string templateKey, out string template)
    {
        if (Translator.TryGetTranslation(templateKey, out template)
            && !string.Equals(template, templateKey, StringComparison.Ordinal))
        {
            return true;
        }

        template = templateKey;
        return false;
    }

    private static string TranslateKnownLeet(string source)
    {
        if (LeetMethod is null)
        {
            return source;
        }

        return ColorAwareTranslationComposer.TranslatePreservingColors(source, TranslateKnownLeetVisible);
    }

    private static string TranslateKnownLeetVisible(string source)
    {
        if (TryTranslateKnownLeetVisible(source, "attempt hack", out var translated))
        {
            return translated;
        }

        if (TryTranslateKnownLeetVisible(source, "Attempt Hack to Select Subject", out translated))
        {
            return translated;
        }

        return source;
    }

    private static bool TryTranslateKnownLeetVisible(string source, string original, out string translated)
    {
        var leeted = GetLeetValue(original);
        if (!string.Equals(source, leeted, StringComparison.Ordinal) || !TryTranslateExact(original, out translated))
        {
            translated = source;
            return false;
        }

        return true;
    }

    private static string? GetLeetValue(string source)
    {
        if (LeetMethod is null)
        {
            return null;
        }

        return LeetMethod.Invoke(null, new object[] { source }) as string;
    }

    private static MethodInfo? ResolveLeetMethod()
    {
        var textFiltersType = GameTypeResolver.FindType("XRL.Language.TextFilters", "TextFilters");
        return textFiltersType is null
            ? null
            : AccessTools.Method(textFiltersType, "Leet", new[] { typeof(string) });
    }

    private static bool IsCyberneticsScreen(object screen)
    {
        for (var type = screen.GetType(); type != null; type = type.BaseType)
        {
            if (string.Equals(type.Name, "CyberneticsScreen", StringComparison.Ordinal)
                || string.Equals(type.FullName, "XRL.UI.CyberneticsScreen", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
