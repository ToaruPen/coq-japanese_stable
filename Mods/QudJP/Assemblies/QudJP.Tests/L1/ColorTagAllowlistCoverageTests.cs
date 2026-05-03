using System.Text.Json;
using System.Text.RegularExpressions;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class ColorTagAllowlistCoverageTests
{
    private static readonly Regex FileScopedNamespacePattern =
        new("^namespace\\s+.+?;$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex MethodSignaturePattern =
        new(
            "^\\s*(?:private|internal|public|protected|static|sealed|override|async|unsafe|extern|readonly|virtual|new|partial|\\s)+[^=;]*?\\b(?<name>[A-Za-z_]\\w*)\\s*\\(",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex StripDeconstructionPattern =
        new(
            "\\bvar\\s*\\(\\s*(?:[A-Za-z_]\\w*|_)\\s*,\\s*(?<spans>[A-Za-z_]\\w*|_)\\s*\\)\\s*=\\s*(?:ColorAwareTranslationComposer|ColorCodePreserver)\\.Strip\\(",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly string[] RestoreRoundTripSymbols =
    {
        "ColorAwareTranslationComposer.Restore(",
        "ColorAwareTranslationComposer.RestoreRelative(",
        "ColorAwareTranslationComposer.RestoreCapture(",
        "ColorAwareTranslationComposer.MarkupAwareRestoreCapture(",
        "ColorAwareTranslationComposer.RestoreSlice(",
        "ColorAwareTranslationComposer.RestoreMatchBoundaries(",
        "ColorAwareTranslationComposer.RestoreSourceBoundaryWrappersByVisibleTextPreservingTranslatedOwnership(",
        "ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(",
        "ColorAwareTranslationComposer.TranslatePreservingColors(",
        "GetDisplayNameRouteTranslator.TranslatePreservingColors(",
        "UITextSkinTranslationPatch.TranslatePreservingColors(",
        "RestoreBalancedCapture(",
        "RestoreCaptureAtOffset(",
    };

    private static readonly string[] StripRoundTripSymbols =
    {
        "ColorAwareTranslationComposer.Strip(",
        "ColorCodePreserver.Strip(",
    };

    private static readonly string[] NameLikeCaptureGroups =
    {
        "killer",
        "item",
        "name",
        "owner",
        "subject",
        "target",
    };

    private static readonly string NameLikeCaptureAlternation = string.Join("|", NameLikeCaptureGroups);

    private static readonly Regex DirectNameLikeRestoreCapturePattern =
        new(
            "ColorAwareTranslationComposer\\.RestoreCapture\\((?<value>.*?),\\s*[^,]+,\\s*(?<group>[^;]*?\\.Groups\\[\"(?<name>"
            + NameLikeCaptureAlternation
            + ")\"\\])",
            RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex AliasAssignmentPattern =
        new(
            "\\b(?:var|Group)\\s+(?<alias>[A-Za-z_]\\w*)\\s*=\\s*[^;]*?\\.Groups\\[\"(?<name>"
            + NameLikeCaptureAlternation
            + ")\"\\]",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AliasedNameLikeRestoreCapturePattern =
        new(
            "ColorAwareTranslationComposer\\.RestoreCapture\\((?<value>.*?),\\s*[^,]+,\\s*(?<alias>[A-Za-z_]\\w*)\\s*\\)",
            RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex SourceHelperNameLikeRestoreCapturePattern =
        new(
            "\\bRestoreCapture\\(\\s*match\\s*,\\s*spans\\s*,\\s*\"(?<name>"
            + NameLikeCaptureAlternation
            + ")\"\\s*\\)",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SourceCaptureValuePattern =
        new(
            "^(?:[^;]*?\\.Groups\\[\"(?<name>"
            + NameLikeCaptureAlternation
            + ")\"\\]\\.Value|(?<alias>[A-Za-z_]\\w*))$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly string[] NameLikeRestoreCaptureGuardSymbols =
    {
        "HasColorMarkup(",
        "IsAlreadyLocalized",
        "MarkupAwareRestoreCapture(",
    };

    private static readonly SortedDictionary<string, string> StripWithoutLocalRestoreAllowlist =
        new(StringComparer.Ordinal)
        {
            ["Mods/QudJP/Assemblies/src/Observability/FinalOutputObservability.cs:116:RecordDirectMarker"] = "Observation-only final sink marker check.",
            ["Mods/QudJP/Assemblies/src/Patches/AbilityBarAfterRenderTranslationPatch.cs:238:HasColorMarkup"] = "Predicate only; it compares stripped and original text.",
            ["Mods/QudJP/Assemblies/src/Patches/ActiveEffectTextTranslator.cs:69:TryTranslateTemplate"] = "Delegates restoration to the template helper selected by the matched rule.",
            ["Mods/QudJP/Assemblies/src/Patches/BedChairFragmentTranslator.cs:120:TryTranslate"] = "Delegates capture restoration to rule builders through RestoreVisible.",
            ["Mods/QudJP/Assemblies/src/Patches/ClonelingVehicleFragmentTranslator.cs:56:TryTranslate"] = "Delegates capture restoration to the build function.",
            ["Mods/QudJP/Assemblies/src/Patches/CombatGetDefenderHitDiceTranslationPatch.cs:82:TryTranslateQueuedMessage"] = "Uses stripped text only for owner-route detection; queued message text is replaced wholesale.",
            ["Mods/QudJP/Assemblies/src/Patches/CombatMeleeAttackTranslationPatch.cs:100:TryTranslateQueuedMessage"] = "Uses stripped text only for owner-route detection; queued message text is replaced wholesale.",
            ["Mods/QudJP/Assemblies/src/Patches/CookingEffectFragmentTranslator.cs:92:TryTranslate"] = "Delegates capture restoration to RestoreVisible.",
            ["Mods/QudJP/Assemblies/src/Patches/DescriptionTextTranslator.cs:152:TryFindDanglingBoundaryOpening"] = "Uses stripped spans only to detect carried color boundary tokens for synthetic line reconstruction.",
            ["Mods/QudJP/Assemblies/src/Patches/DescriptionTextTranslator.cs:208:HasColorBoundaryOpening"] = "Predicate only; it uses stripped spans to detect color boundary tokens.",
            ["Mods/QudJP/Assemblies/src/Patches/DescriptionTextTranslator.cs:243:HasColorBoundaryClosing"] = "Predicate only; it uses stripped spans to detect color boundary closure.",
            ["Mods/QudJP/Assemblies/src/Patches/DescriptionTextTranslator.cs:314:TryTranslateSultanShrineWrapperPreservingColors"] = "Passes stripped text and spans to SultanShrineWrapperTranslator.",
            ["Mods/QudJP/Assemblies/src/Patches/EnclosingFragmentTranslator.cs:22:TryTranslatePopupMessage"] = "Delegates capture restoration to RestoreVisible.",
            ["Mods/QudJP/Assemblies/src/Patches/FactionsLineTranslationPatch.cs:84:TranslateTextField"] = "Strips only to detect whether the already-localized field contains visible text.",
            ["Mods/QudJP/Assemblies/src/Patches/FactionsStatusScreenTranslationPatch.cs:895:AddLocalizedSearchFragment"] = "Strips only to add searchable plain text beside the colored display text.",
            ["Mods/QudJP/Assemblies/src/Patches/GetDisplayNameRouteTranslator.cs:880:TranslateDisplayNameModifier"] = "Strips only to classify display-name modifier text before composing a translated modifier.",
            ["Mods/QudJP/Assemblies/src/Patches/JournalNotificationTranslator.cs:19:TryTranslate"] = "Strips source colors so journal notification patterns can replace the full system sentence.",
            ["Mods/QudJP/Assemblies/src/Patches/LiquidVolumeFragmentTranslator.cs:118:TryTranslate"] = "Delegates capture restoration to helper calls in the matched branch.",
            ["Mods/QudJP/Assemblies/src/Patches/MainMenuLocalizationPatch.cs:174:TranslateProducerText"] = "Strips only for already-localized/direct-route checks before TranslatePreservingColors owns restoration.",
            ["Mods/QudJP/Assemblies/src/Patches/MessageLogPatch.cs:77:Prefix"] = "Observation-only direct marker check before MessagePatternTranslator owns restoration.",
            ["Mods/QudJP/Assemblies/src/Patches/MutationsApiTranslationPatch.cs:85:TryTranslatePopupMessage"] = "Delegates capture restoration to term-specific branch helpers.",
            ["Mods/QudJP/Assemblies/src/Patches/OptionsLocalizationPatch.cs:106:TranslateProducerText"] = "Strips only for already-localized/direct-route checks before TranslatePreservingColors owns restoration.",
            ["Mods/QudJP/Assemblies/src/Patches/PickGameObjectScreenTranslationPatch.cs:115:TranslateProducerText"] = "Strips only for already-localized/direct-route checks before TranslatePreservingColors owns restoration.",
            ["Mods/QudJP/Assemblies/src/Patches/PlayerStatusBarProducerTranslationHelpers.cs:101:TryTranslateFoodWaterPart"] = "Strips only to choose an exact visible-text translation before TranslatePreservingColors owns restoration.",
            ["Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs:1126:IsAlreadyLocalizedPopupText"] = "Predicate only; it compares stripped and original text.",
            ["Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs:255:TranslatePopupTextForRoute"] = "Strips only for direct-marker and already-localized detection before producer routes own restoration.",
            ["Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs:301:TranslatePopupMenuItemTextForRoute"] = "Strips only for direct-marker and already-localized detection before producer routes own restoration.",
            ["Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs:352:TryTranslatePopupProducerText"] = "Delegates capture restoration to template/exact branch helpers.",
            ["Mods/QudJP/Assemblies/src/Patches/SkillsAndPowersLineTranslationPatch.cs:229:TranslateSkillRightText"] = "Strips only for skill right-text detection before a non-colored exact replacement.",
            ["Mods/QudJP/Assemblies/src/Patches/TradeUiPopupTranslationPatch.cs:261:TryTranslateTradeUiPopupText"] = "Delegates capture restoration to branch-specific RestoreCapture helpers.",
            ["Mods/QudJP/Assemblies/src/Patches/UITextSkinTranslationPatch.cs:100:TranslatePreservingColors"] = "Sink fallback uses stripped text only for already-localized/direct-route checks.",
            ["Mods/QudJP/Assemblies/src/Patches/WorldModsTextTranslator.cs:490:TryTranslateCoProcessorTemplate"] = "Delegates restoration to the template helper.",
            ["Mods/QudJP/Assemblies/src/Patches/WorldModsTextTranslator.cs:526:TryTranslateCounterweightedTemplate"] = "Delegates restoration to the template helper.",
            ["Mods/QudJP/Assemblies/src/Patches/WorldModsTextTranslator.cs:560:TryTranslateElementalDamageTemplate"] = "Delegates restoration to the template helper.",
            ["Mods/QudJP/Assemblies/src/Patches/WorldModsTextTranslator.cs:593:TryTranslateTemplate"] = "Delegates restoration to the template helper.",
            ["Mods/QudJP/Assemblies/src/Translation/ColorAwareTranslationComposer.cs:30:Strip"] = "Wrapper method exposes the Strip API; callers are checked where they consume the returned spans.",
            ["Mods/QudJP/Assemblies/src/Translation/JournalPatternTranslator.cs:70:Translate"] = "Passes stripped text and spans to TranslateStripped, where template application restores colors.",
            ["Mods/QudJP/Assemblies/src/Translation/MessagePatternTranslator.cs:79:Translate"] = "Passes stripped text and spans to TranslateStripped, where template application restores colors.",
            ["Mods/QudJP/Assemblies/src/UI/FontManager.cs:174:TryWarmPrimaryFontCharactersForUi"] = "UI glyph warmup intentionally strips markup only to add visible characters to the font atlas.",
        };

    private static readonly SortedDictionary<string, string> NameLikeRestoreCaptureWithoutGuardAllowlist =
        new(StringComparer.Ordinal)
        {
            ["Mods/QudJP/Assemblies/src/Patches/ChargenStructuredTextTranslator.cs:339:TryTranslateCyberneticsSlot:name"] =
                "Cybernetics slot names are exact static labels, not display-name owner captures.",
        };

    private static readonly string[] DisplayNameOwnerRouteFiles =
    {
        "Mods/QudJP/Assemblies/src/Patches/AbilityBarAfterRenderTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/AbilityBarButtonTextTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/DeathWrapperFamilyTranslator.cs",
        "Mods/QudJP/Assemblies/src/Patches/GameManagerUpdateSelectedAbilityPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/GetDisplayNamePatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/GetDisplayNameProcessPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/InventoryLocalizationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/XDidYTranslationPatch.cs",
    };

    private static readonly string[] MarkupAwareCaptureOwnerFiles =
    {
        "Mods/QudJP/Assemblies/src/Patches/DeathWrapperFamilyTranslator.cs",
        "Mods/QudJP/Assemblies/src/Translation/JournalPatternTranslator.cs",
        "Mods/QudJP/Assemblies/src/Translation/MessagePatternTranslator.cs",
    };

    [Test]
    public void EveryStripCallSite_HasMatchingRestoreCallSite()
    {
        var actual = FindStripCallSitesWithoutLocalRestore();

        Assert.That(
            actual.Keys,
            Is.EquivalentTo(StripWithoutLocalRestoreAllowlist.Keys),
            "Each Strip call site must restore colors in the same local forward block or be explicitly allowlisted.\n"
            + string.Join("\n", actual.Keys));
    }

    [Test]
    public void DisplayNameTranslate_OnlyCalled_FromOwnerRouteAllowlist()
    {
        var actual = FindFilesContaining("GetDisplayNameRouteTranslator.TranslatePreservingColors(");

        Assert.That(actual, Is.EquivalentTo(DisplayNameOwnerRouteFiles));
    }

    [Test]
    public void RestoreCapture_OnNameLikeCapture_HasMarkupGuard()
    {
        var unguarded = FindNameLikeRestoreCaptureCallSitesWithoutMarkupGuard();
        var markupAwareOwnerFiles = FindFilesContaining("ColorAwareTranslationComposer.MarkupAwareRestoreCapture(");

        Assert.Multiple(() =>
        {
            Assert.That(
                unguarded.Keys,
                Is.EquivalentTo(NameLikeRestoreCaptureWithoutGuardAllowlist.Keys),
                "RestoreCapture on name-like captures must use a markup-aware guard or be a documented non-display-name exception.\n"
                + string.Join("\n", unguarded.Keys));
            Assert.That(markupAwareOwnerFiles, Is.EquivalentTo(MarkupAwareCaptureOwnerFiles));
        });
    }

    [Test]
    public void DictionaryCorpus_HasBalancedMarkupTokens()
    {
        var failures = new List<string>();
        foreach (var value in EnumerateDictionaryTranslatedValues())
        {
            var (stripped, spans) = ColorAwareTranslationComposer.Strip(value.Value);
            var restored = ColorAwareTranslationComposer.Restore(stripped, spans);
            if (!string.Equals(value.Value, restored, StringComparison.Ordinal))
            {
                failures.Add($"{value.RelativePath}:{value.ArrayName}.{value.PropertyName}[{value.Index}]");
            }
        }

        Assert.That(failures, Is.Empty);
    }

    private static SortedDictionary<string, string> FindStripCallSitesWithoutLocalRestore()
    {
        var result = new SortedDictionary<string, string>(StringComparer.Ordinal);

        foreach (var file in GetSourceFiles())
        {
            var relativePath = ToRepositoryRelativePath(file);
            var lines = File.ReadAllLines(file);
            for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                if (!ContainsAny(lines[lineIndex], StripRoundTripSymbols))
                {
                    continue;
                }

                var methodStart = FindContainingMethodStart(lines, lineIndex);
                var methodName = methodStart < 0 ? "<unknown>" : ExtractMethodName(lines[methodStart]);
                var localText = methodStart < 0
                    ? lines[lineIndex]
                    : string.Join("\n", ExtractLocalForwardBlock(lines, methodStart, lineIndex));

                if (TryGetStripSpansVariable(lines[lineIndex], out var spansVariable)
                    && ContainsRestoreUsingSpansVariable(localText, spansVariable))
                {
                    continue;
                }

                var key = $"{relativePath}:{lineIndex + 1}:{methodName}";
                result[key] = lines[lineIndex].Trim();
            }
        }

        return result;
    }

    private static SortedDictionary<string, string> FindNameLikeRestoreCaptureCallSitesWithoutMarkupGuard()
    {
        var result = new SortedDictionary<string, string>(StringComparer.Ordinal);

        foreach (var file in GetSourceFiles())
        {
            var relativePath = ToRepositoryRelativePath(file);
            var lines = File.ReadAllLines(file);
            foreach (var invocation in EnumerateRestoreCaptureInvocations(lines))
            {
                var methodStart = FindContainingMethodStart(lines, invocation.StartLineIndex);
                var methodName = methodStart < 0 ? "<unknown>" : ExtractMethodName(lines[methodStart]);
                var methodLines = methodStart < 0
                    ? new[] { invocation.Text }
                    : ExtractMethodBlock(lines, methodStart);
                var aliases = FindNameLikeGroupAliases(methodLines);
                if (!TryGetUnsafeNameLikeRestoreCapture(invocation.Text, aliases, out var groupName))
                {
                    continue;
                }

                var guardText = methodStart < 0
                    ? invocation.Text
                    : string.Join("\n", ExtractLocalGuardBlock(lines, methodStart, invocation.StartLineIndex));
                if (ContainsAny(guardText, NameLikeRestoreCaptureGuardSymbols))
                {
                    continue;
                }

                var key = $"{relativePath}:{invocation.StartLineIndex + 1}:{methodName}:{groupName}";
                result[key] = invocation.Text.Trim();
            }
        }

        return result;
    }

    private static string[] FindFilesContaining(string symbol)
    {
        var matches = new List<string>();

        foreach (var file in GetSourceFiles())
        {
            var text = File.ReadAllText(file);
            if (text.Contains(symbol, StringComparison.Ordinal))
            {
                matches.Add(ToRepositoryRelativePath(file));
            }
        }

        return matches.ToArray();
    }

    private static IEnumerable<DictionaryValue> EnumerateDictionaryTranslatedValues()
    {
        var root = TestProjectPaths.GetRepositoryRoot();
        var dictionariesRoot = Path.Combine(root, "Mods", "QudJP", "Localization", "Dictionaries");

        foreach (var file in GetSortedFiles(dictionariesRoot, "*.json"))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            var rootElement = document.RootElement;
            foreach (var value in EnumerateTranslatedTextArrays(rootElement, file))
            {
                yield return value;
            }
        }
    }

    private static IEnumerable<DictionaryValue> EnumerateTranslatedTextArrays(JsonElement rootElement, string file)
    {
        foreach (var property in rootElement.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var value in EnumerateStringPropertyArray(rootElement, file, property.Name, "text"))
            {
                yield return value;
            }

            foreach (var value in EnumerateStringPropertyArray(rootElement, file, property.Name, "template"))
            {
                yield return value;
            }
        }
    }

    private static IEnumerable<DictionaryValue> EnumerateStringPropertyArray(
        JsonElement rootElement,
        string file,
        string arrayName,
        string propertyName)
    {
        if (!rootElement.TryGetProperty(arrayName, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        var index = 0;
        foreach (var element in array.EnumerateArray())
        {
            if (element.ValueKind == JsonValueKind.Object
                && element.TryGetProperty(propertyName, out var property)
                && property.ValueKind == JsonValueKind.String
                && property.GetString() is { } value)
            {
                yield return new DictionaryValue(ToRepositoryRelativePath(file), arrayName, propertyName, index, value);
            }

            index++;
        }
    }

    private static int FindContainingMethodStart(string[] lines, int callLineIndex)
    {
        for (var index = callLineIndex; index >= 0; index--)
        {
            var line = lines[index].Trim();
            if (line.Length == 0
                || line.StartsWith("[", StringComparison.Ordinal)
                || FileScopedNamespacePattern.IsMatch(line))
            {
                continue;
            }

            if (IsMethodSignature(line))
            {
                return index;
            }
        }

        return -1;
    }

    private static string ExtractMethodName(string line)
    {
        var match = MethodSignaturePattern.Match(line);
        return match.Success ? match.Groups["name"].Value : "<unknown>";
    }

    private static IReadOnlyList<string> ExtractMethodBlock(string[] lines, int methodStart)
    {
        var end = methodStart;
        var depth = 0;
        var sawOpeningBrace = false;
        for (var index = methodStart; index < lines.Length; index++)
        {
            var line = lines[index];
            for (var charIndex = 0; charIndex < line.Length; charIndex++)
            {
                if (line[charIndex] == '{')
                {
                    depth++;
                    sawOpeningBrace = true;
                }
                else if (line[charIndex] == '}')
                {
                    depth--;
                }
            }

            end = index;
            if (sawOpeningBrace && depth <= 0)
            {
                break;
            }
        }

        return lines[methodStart..(end + 1)];
    }

    private static IReadOnlyList<string> ExtractLocalForwardBlock(string[] lines, int methodStart, int callLineIndex)
    {
        var methodBlock = ExtractMethodBlock(lines, methodStart);
        var methodEnd = methodStart + methodBlock.Count - 1;
        var regionEnd = Math.Min(methodEnd, callLineIndex + 80);
        return lines[callLineIndex..(regionEnd + 1)];
    }

    private static IReadOnlyList<string> ExtractLocalGuardBlock(string[] lines, int methodStart, int callLineIndex)
    {
        var methodBlock = ExtractMethodBlock(lines, methodStart);
        var methodEnd = methodStart + methodBlock.Count - 1;
        var regionStart = Math.Max(methodStart, callLineIndex - 80);
        var regionEnd = Math.Min(methodEnd, callLineIndex + 80);
        return lines[regionStart..(regionEnd + 1)];
    }

    private static bool TryGetStripSpansVariable(string line, out string spansVariable)
    {
        var match = StripDeconstructionPattern.Match(line);
        if (match.Success && match.Groups["spans"].Value != "_")
        {
            spansVariable = match.Groups["spans"].Value;
            return true;
        }

        spansVariable = string.Empty;
        return false;
    }

    private static bool ContainsRestoreUsingSpansVariable(string source, string spansVariable)
    {
        for (var symbolIndex = 0; symbolIndex < RestoreRoundTripSymbols.Length; symbolIndex++)
        {
            var symbol = RestoreRoundTripSymbols[symbolIndex];
            var searchStart = 0;
            while (searchStart < source.Length)
            {
                var occurrence = source.IndexOf(symbol, searchStart, StringComparison.Ordinal);
                if (occurrence < 0)
                {
                    break;
                }

                var invocation = TryExtractInvocation(source, occurrence, out var extracted)
                    ? extracted
                    : source[occurrence..];
                if (ContainsIdentifier(invocation, spansVariable))
                {
                    return true;
                }

                searchStart = occurrence + symbol.Length;
            }
        }

        return false;
    }

    private static IEnumerable<InvocationText> EnumerateRestoreCaptureInvocations(string[] lines)
    {
        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var searchStart = 0;
            while (searchStart < lines[lineIndex].Length)
            {
                var occurrence = lines[lineIndex].IndexOf("RestoreCapture(", searchStart, StringComparison.Ordinal);
                if (occurrence < 0)
                {
                    break;
                }

                if (TryExtractInvocation(lines, lineIndex, occurrence, out var invocation))
                {
                    yield return invocation;
                }
                else
                {
                    yield return new InvocationText(lines[lineIndex][occurrence..], lineIndex);
                }

                searchStart = occurrence + "RestoreCapture(".Length;
            }
        }
    }

    private static bool TryExtractInvocation(string[] lines, int lineIndex, int invocationNameIndex, out InvocationText invocation)
    {
        var source = string.Join("\n", lines[lineIndex..]);
        var start = GetInvocationTextStart(lines[lineIndex], invocationNameIndex);
        if (!TryExtractInvocation(source, start, out var invocationText))
        {
            invocation = new InvocationText(string.Empty, lineIndex);
            return false;
        }

        invocation = new InvocationText(invocationText, lineIndex);
        return true;
    }

    private static bool TryExtractInvocation(string source, int invocationStart, out string invocationText)
    {
        var openParen = source.IndexOf('(', invocationStart);
        if (openParen < 0)
        {
            invocationText = string.Empty;
            return false;
        }

        var depth = 0;
        for (var index = openParen; index < source.Length; index++)
        {
            if (source[index] == '(')
            {
                depth++;
            }
            else if (source[index] == ')')
            {
                depth--;
                if (depth == 0)
                {
                    invocationText = source[invocationStart..(index + 1)];
                    return true;
                }
            }
        }

        invocationText = string.Empty;
        return false;
    }

    private static int GetInvocationTextStart(string line, int invocationNameIndex)
    {
        var index = invocationNameIndex;
        while (index > 0 && IsInvocationQualifierCharacter(line[index - 1]))
        {
            index--;
        }

        return index;
    }

    private static bool IsInvocationQualifierCharacter(char value)
    {
        return value == '.' || value == '_' || char.IsAsciiLetterOrDigit(value);
    }

    private static bool ContainsIdentifier(string source, string identifier)
    {
        var searchStart = 0;
        while (searchStart < source.Length)
        {
            var index = source.IndexOf(identifier, searchStart, StringComparison.Ordinal);
            if (index < 0)
            {
                return false;
            }

            var before = index == 0 ? '\0' : source[index - 1];
            var afterIndex = index + identifier.Length;
            var after = afterIndex >= source.Length ? '\0' : source[afterIndex];
            if (!IsIdentifierCharacter(before) && !IsIdentifierCharacter(after))
            {
                return true;
            }

            searchStart = index + identifier.Length;
        }

        return false;
    }

    private static bool IsIdentifierCharacter(char value)
    {
        return value == '_' || char.IsAsciiLetterOrDigit(value);
    }

    private static Dictionary<string, string> FindNameLikeGroupAliases(IReadOnlyList<string> methodLines)
    {
        var aliases = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < methodLines.Count; index++)
        {
            var match = AliasAssignmentPattern.Match(methodLines[index]);
            if (match.Success)
            {
                aliases[match.Groups["alias"].Value] = match.Groups["name"].Value;
            }
        }

        return aliases;
    }

    private static bool TryGetUnsafeNameLikeRestoreCapture(
        string line,
        IReadOnlyDictionary<string, string> aliases,
        out string groupName)
    {
        var directMatch = DirectNameLikeRestoreCapturePattern.Match(line);
        if (directMatch.Success)
        {
            groupName = directMatch.Groups["name"].Value;
            return !IsSourceCaptureRestore(directMatch.Groups["value"].Value, groupName, aliases);
        }

        var aliasMatch = AliasedNameLikeRestoreCapturePattern.Match(line);
        if (aliasMatch.Success && aliases.TryGetValue(aliasMatch.Groups["alias"].Value, out var aliasedGroupName))
        {
            groupName = aliasedGroupName;
            return !IsSourceCaptureRestore(aliasMatch.Groups["value"].Value, groupName, aliases);
        }

        var helperMatch = SourceHelperNameLikeRestoreCapturePattern.Match(line);
        if (helperMatch.Success)
        {
            groupName = helperMatch.Groups["name"].Value;
            return false;
        }

        groupName = string.Empty;
        return false;
    }

    private static bool IsSourceCaptureRestore(string valueExpression, string groupName, IReadOnlyDictionary<string, string> aliases)
    {
        var normalized = valueExpression.Trim();
        var sourceMatch = SourceCaptureValuePattern.Match(normalized);
        if (!sourceMatch.Success)
        {
            return false;
        }

        if (sourceMatch.Groups["name"].Success)
        {
            return string.Equals(sourceMatch.Groups["name"].Value, groupName, StringComparison.Ordinal);
        }

        return aliases.TryGetValue(sourceMatch.Groups["alias"].Value, out var aliasedGroup)
            && string.Equals(aliasedGroup, groupName, StringComparison.Ordinal);
    }

    private static bool IsMethodSignature(string line)
    {
        if (!MethodSignaturePattern.IsMatch(line))
        {
            return false;
        }

        return !line.StartsWith("if ", StringComparison.Ordinal)
            && !line.StartsWith("for ", StringComparison.Ordinal)
            && !line.StartsWith("foreach ", StringComparison.Ordinal)
            && !line.StartsWith("while ", StringComparison.Ordinal)
            && !line.StartsWith("switch ", StringComparison.Ordinal)
            && !line.StartsWith("catch ", StringComparison.Ordinal);
    }

    private static bool ContainsAny(string source, IReadOnlyList<string> symbols)
    {
        for (var index = 0; index < symbols.Count; index++)
        {
            if (source.Contains(symbols[index], StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string ToRepositoryRelativePath(string path)
    {
        return Path.GetRelativePath(TestProjectPaths.GetRepositoryRoot(), path)
            .Replace(Path.DirectorySeparatorChar, '/');
    }

    private static string[] GetSourceFiles()
    {
        var sourceRoot = Path.Combine(TestProjectPaths.GetRepositoryRoot(), "Mods", "QudJP", "Assemblies", "src");
        return GetSortedFiles(sourceRoot, "*.cs");
    }

    private static string[] GetSortedFiles(string root, string searchPattern)
    {
        var files = Directory.GetFiles(root, searchPattern, SearchOption.AllDirectories);
        Array.Sort(files, StringComparer.Ordinal);
        return files;
    }

    private sealed record DictionaryValue(string RelativePath, string ArrayName, string PropertyName, int Index, string Value);

    private sealed record InvocationText(string Text, int StartLineIndex);
}
