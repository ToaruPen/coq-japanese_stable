using System.Globalization;
using System.Text.RegularExpressions;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class ColorRouteCatalogTests
{
    private static readonly Regex ColorPreservingEntryPointDeclarationPattern =
        new(
            "^[^\\S\\r\\n]*(?:internal|public)\\s+static\\s+[^=;{]*?\\b(?<name>(?:Try)?\\w*PreservingColors|Strip|Restore|RestoreRelative|RestoreCapture|MarkupAwareRestoreCapture|RestoreCaptureWholeBoundaryWrappersPreservingTranslatedOwnership|RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership|RestoreWholeSliceBoundaryWrappersPreservingTranslatedOwnership|RestoreSourceBoundaryWrappersByVisibleTextPreservingTranslatedOwnership|RestoreSlice|RestoreMatchBoundaries)\\s*\\(",
            RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly string[] TranslationLayerPatchRouteImplementationReferences =
    {
        "GetDisplayNameRouteTranslator.TranslatePreservingColors(",
        "DisplayNameCaptureTranslator.TranslatePreservingColors(",
        "GeneratedQuestTitleTranslator.TranslatePreservingColors(",
        "GeneratedQuestTitleTranslator.TryTranslatePreservingColors(",
        "PopupTranslationPatch.TranslatePopupTextForRoute(",
        "PopupTranslationPatch.TranslatePopupTextForProducerRoute(",
        "PopupTranslationPatch.TranslatePopupMenuItemTextForProducerRoute(",
        "UITextSkinTranslationPatch.TranslatePreservingColors(",
    };

    [Test]
    public void Catalog_CompletelyCoversColorSensitiveTranslationCallSites()
    {
        var root = TestProjectPaths.GetRepositoryRoot();
        var sourceRoot = Path.Combine(root, "Mods", "QudJP", "Assemblies", "src");
        var actual = ScanSymbolOccurrences(sourceRoot, ColorRouteCatalog.RouteSymbols);

        Assert.That(actual, Is.EquivalentTo(ColorRouteCatalog.ExpectedSymbolOccurrences));
    }

    [Test]
    public void Catalog_CompletelyCoversColorPreservingEntryPointDeclarations()
    {
        var root = TestProjectPaths.GetRepositoryRoot();
        var sourceRoot = Path.Combine(root, "Mods", "QudJP", "Assemblies", "src");
        var actual = ScanColorPreservingEntryPointDeclarations(sourceRoot);

        Assert.That(actual, Is.EquivalentTo(ColorRouteCatalog.ExpectedEntryPointDeclarations.Keys));
    }

    [Test]
    public void Catalog_DocumentsColorPreservingEntryPointOwnership()
    {
        foreach (var entry in ColorRouteCatalog.ExpectedEntryPointDeclarations)
        {
            Assert.That(entry.Value.Kind, Is.Not.EqualTo(ColorPreservingEntryPointKind.Unclassified), entry.Key);
            Assert.That(entry.Value.RouteContract, Is.Not.Empty, entry.Key);
            Assert.That(entry.Value.Reason, Is.Not.Empty, entry.Key);
        }
    }

    [Test]
    public void TranslationLayer_DoesNotCallPatchRouteAdaptersDirectly()
    {
        var root = TestProjectPaths.GetRepositoryRoot();
        var translationRoot = Path.Combine(root, "Mods", "QudJP", "Assemblies", "src", "Translation");
        var actual = ScanTranslationLayerPatchRouteImplementationReferences(translationRoot);

        Assert.That(
            actual,
            Is.Empty,
            "Translation-layer code must depend on route contracts in the QudJP namespace, not patch-layer route adapter implementation types.");
    }

    [Test]
    public void Catalog_OnlyReferencesExistingSourceFiles()
    {
        var root = TestProjectPaths.GetRepositoryRoot();
        foreach (var entry in ColorRouteCatalog.ExpectedSymbolOccurrences.Keys)
        {
            var relativePath = GetCatalogRelativePath(entry, '|');
            var fullPath = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.That(File.Exists(fullPath), Is.True, $"Catalog file not found: {relativePath}");
        }

        foreach (var entry in ColorRouteCatalog.ExpectedEntryPointDeclarations.Keys)
        {
            var relativePath = GetCatalogRelativePath(entry, ':');
            var fullPath = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.That(File.Exists(fullPath), Is.True, $"Entry point catalog file not found: {relativePath}");
        }
    }

    [Test]
    public void Catalog_DocumentsGenericPopupProducerRouteCallSites()
    {
        var root = TestProjectPaths.GetRepositoryRoot();
        var sourceRoot = Path.Combine(root, "Mods", "QudJP", "Assemblies", "src");
        var actual = ScanSymbolOccurrences(sourceRoot, ColorRouteCatalog.GenericPopupProducerRouteSymbols);
        var expected = new SortedDictionary<string, int>(StringComparer.Ordinal);

        foreach (var allowance in ColorRouteCatalog.GenericPopupProducerRouteAllowlist)
        {
            expected[allowance.Key] = allowance.Value.ExpectedCount;
            Assert.That(
                allowance.Value.Reason,
                Is.Not.Empty,
                "Generic popup producer route call sites must document why a narrower owner route is not used: "
                + allowance.Key);
        }

        Assert.That(
            actual,
            Is.EquivalentTo(expected),
            "Generic popup producer routes are intentionally narrow. Add or change an allowance only after proving "
            + "the call handles fixed popup text, menu items, or a shared popup helper after owner-specific routes run first.");
    }

    private static SortedDictionary<string, int> ScanSymbolOccurrences(
        string sourceRoot,
        IReadOnlyList<string> routeSymbols)
    {
        var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var files = Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories);
        Array.Sort(files, StringComparer.Ordinal);

        for (var fileIndex = 0; fileIndex < files.Length; fileIndex++)
        {
            var file = files[fileIndex];
            var relativePath = Path.GetRelativePath(TestProjectPaths.GetRepositoryRoot(), file)
                .Replace(Path.DirectorySeparatorChar, '/');
            var source = File.ReadAllText(file);

            for (var symbolIndex = 0; symbolIndex < routeSymbols.Count; symbolIndex++)
            {
                var symbol = routeSymbols[symbolIndex];
                var occurrenceCount = CountOccurrences(source, symbol);
                if (occurrenceCount == 0)
                {
                    continue;
                }

                var key = relativePath + "|" + symbol;
                counts[key] = counts.TryGetValue(key, out var count) ? count + occurrenceCount : occurrenceCount;
            }
        }

        return counts;
    }

    private static SortedSet<string> ScanColorPreservingEntryPointDeclarations(string sourceRoot)
    {
        var entries = new SortedSet<string>(StringComparer.Ordinal);
        var files = Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories);
        Array.Sort(files, StringComparer.Ordinal);

        foreach (var file in files)
        {
            var relativePath = Path.GetRelativePath(TestProjectPaths.GetRepositoryRoot(), file)
                .Replace(Path.DirectorySeparatorChar, '/');
            var source = File.ReadAllText(file);
            foreach (Match match in ColorPreservingEntryPointDeclarationPattern.Matches(source))
            {
                entries.Add(relativePath + ":" + GetLineNumber(source, match.Index).ToString(CultureInfo.InvariantCulture) + ":"
                    + match.Groups["name"].Value);
            }
        }

        return entries;
    }

    private static SortedSet<string> ScanTranslationLayerPatchRouteImplementationReferences(string translationRoot)
    {
        var entries = new SortedSet<string>(StringComparer.Ordinal);
        var files = Directory.GetFiles(translationRoot, "*.cs", SearchOption.AllDirectories);
        Array.Sort(files, StringComparer.Ordinal);

        foreach (var file in files)
        {
            var relativePath = Path.GetRelativePath(TestProjectPaths.GetRepositoryRoot(), file)
                .Replace(Path.DirectorySeparatorChar, '/');
            var source = File.ReadAllText(file);
            foreach (var reference in TranslationLayerPatchRouteImplementationReferences)
            {
                foreach (var lineNumber in FindSymbolOccurrenceLineNumbers(source, reference))
                {
                    entries.Add(relativePath + ":" + lineNumber.ToString(CultureInfo.InvariantCulture) + ":"
                        + reference);
                }
            }
        }

        return entries;
    }

    private static string GetCatalogRelativePath(string entry, char separator)
    {
        return entry[..entry.IndexOf(separator)];
    }

    private static int CountOccurrences(string source, string symbol)
    {
        var pattern = CreateWhitespaceTolerantSymbolPattern(symbol);
        return Regex.Count(source, pattern, RegexOptions.CultureInvariant | RegexOptions.Singleline);
    }

    private static IEnumerable<int> FindSymbolOccurrenceLineNumbers(string source, string symbol)
    {
        var pattern = CreateWhitespaceTolerantSymbolPattern(symbol);
        foreach (Match match in Regex.Matches(source, pattern, RegexOptions.CultureInvariant | RegexOptions.Singleline))
        {
            yield return GetLineNumber(source, match.Index);
        }
    }

    private static string CreateWhitespaceTolerantSymbolPattern(string symbol)
    {
        return Regex.Escape(symbol)
            .Replace("\\.", "\\s*\\.\\s*", StringComparison.Ordinal)
            .Replace("\\(", "\\s*\\(", StringComparison.Ordinal);
    }

    private static int GetLineNumber(string source, int index)
    {
        var line = 1;
        for (var position = 0; position < index; position++)
        {
            if (source[position] == '\n')
            {
                line++;
            }
        }

        return line;
    }

    [Test]
    public void CountOccurrences_AllowsWhitespaceAroundPopupRouteInvocation()
    {
        const string source = """
            PopupTranslationPatch
                .
                TranslatePopupTextForProducerRoute
                (
                    message,
                    Context);
            PopupTranslationPatch.TranslatePopupTextForProducerRoute(message, Context);
            """;

        Assert.That(
            CountOccurrences(source, "PopupTranslationPatch.TranslatePopupTextForProducerRoute("),
            Is.EqualTo(2));
    }

    [Test]
    public void FindSymbolOccurrenceLineNumbers_AllowsWhitespaceAroundForbiddenRouteInvocation()
    {
        const string source = """
            return GetDisplayNameRouteTranslator
                .
                TranslatePreservingColors
                (
                    source,
                    route);
            """;

        Assert.That(
            FindSymbolOccurrenceLineNumbers(source, "GetDisplayNameRouteTranslator.TranslatePreservingColors("),
            Is.EquivalentTo(new[] { 1 }));
    }

    [Test]
    public void DeclarationPattern_AllowsMultilineColorPreservingEntryPointDeclaration()
    {
        const string source = """
            internal static string
                TranslateCapturePreservingColors
                (
                    string source,
                    string context)
            {
                return source;
            }
            """;

        var match = ColorPreservingEntryPointDeclarationPattern.Match(source);

        Assert.That(match.Success, Is.True);
        Assert.That(match.Groups["name"].Value, Is.EqualTo("TranslateCapturePreservingColors"));
        Assert.That(GetLineNumber(source, match.Index), Is.EqualTo(1));
    }
}

internal enum ColorPreservingEntryPointKind
{
    Unclassified,
    LowLevelHelper,
    RouteContract,
    RouteAdapter,
    PatchImplementation,
}

internal sealed class ColorPreservingEntryPointOwnership
{
    internal ColorPreservingEntryPointOwnership(
        ColorPreservingEntryPointKind kind,
        string routeContract,
        string reason)
    {
        Kind = kind;
        RouteContract = routeContract;
        Reason = reason;
    }

    internal ColorPreservingEntryPointKind Kind { get; }

    internal string RouteContract { get; }

    internal string Reason { get; }
}

internal static class ColorRouteCatalog
{
    internal static readonly string[] RouteSymbols =
    {
        "MessagePatternTranslator.Translate(",
        "MessagePatternTranslator.TranslateIfPatternMatches(",
        "JournalPatternTranslator.Translate(",
        "ColorAwareTranslationComposer.TranslatePreservingColors(",
        "DescriptionTextTranslator.TranslateLongDescription(",
        "DisplayNameRouteTranslation.TranslateCapturePreservingColors(",
        "DisplayNameRouteTranslation.TranslatePreservingColors(",
        "GetDisplayNameRouteTranslator.TranslatePreservingColors(",
        "UITextSkinTranslationPatch.TranslatePreservingColors(",
        "PopupTranslationPatch.TranslatePopupTextForRoute(",
        "PopupTranslationPatch.TranslatePopupTextForProducerRoute(",
        "PopupTranslationPatch.TranslatePopupMenuItemTextForProducerRoute(",
    };

    internal static readonly SortedDictionary<string, ColorPreservingEntryPointOwnership> ExpectedEntryPointDeclarations =
        new(StringComparer.Ordinal)
        {
            ["Mods/QudJP/Assemblies/src/Translation/ColorAwareTranslationComposer.cs:32:Strip"] =
                new(ColorPreservingEntryPointKind.LowLevelHelper, "ColorAwareTranslationComposer", "Core strip helper for Qud/TMP color spans."),
            ["Mods/QudJP/Assemblies/src/Translation/ColorAwareTranslationComposer.cs:37:Restore"] =
                new(ColorPreservingEntryPointKind.LowLevelHelper, "ColorAwareTranslationComposer", "Core restore helper for stripped color spans."),
            ["Mods/QudJP/Assemblies/src/Translation/ColorAwareTranslationComposer.cs:56:RestoreRelative"] =
                new(ColorPreservingEntryPointKind.LowLevelHelper, "ColorAwareTranslationComposer", "Restores color spans relative to translated whole-source length."),
            ["Mods/QudJP/Assemblies/src/Translation/ColorAwareTranslationComposer.cs:72:TranslatePreservingColors"] =
                new(ColorPreservingEntryPointKind.LowLevelHelper, "ColorAwareTranslationComposer", "Generic color-preserving composer using the default translator."),
            ["Mods/QudJP/Assemblies/src/Translation/ColorAwareTranslationComposer.cs:77:TranslatePreservingColors"] =
                new(ColorPreservingEntryPointKind.LowLevelHelper, "ColorAwareTranslationComposer", "Generic color-preserving composer for visible-text translators."),
            ["Mods/QudJP/Assemblies/src/Translation/ColorAwareTranslationComposer.cs:101:TranslatePreservingColors"] =
                new(ColorPreservingEntryPointKind.LowLevelHelper, "ColorAwareTranslationComposer", "Generic color-preserving composer for translators that need source spans."),
            ["Mods/QudJP/Assemblies/src/Translation/ColorAwareTranslationComposer.cs:127:RestoreCapture"] =
                new(ColorPreservingEntryPointKind.LowLevelHelper, "ColorAwareTranslationComposer", "Capture-level color restoration helper."),
            ["Mods/QudJP/Assemblies/src/Translation/ColorAwareTranslationComposer.cs:139:MarkupAwareRestoreCapture"] =
                new(ColorPreservingEntryPointKind.LowLevelHelper, "ColorAwareTranslationComposer", "Capture restoration helper that preserves translated markup ownership."),
            ["Mods/QudJP/Assemblies/src/Translation/ColorAwareTranslationComposer.cs:179:RestoreCaptureWholeBoundaryWrappersPreservingTranslatedOwnership"] =
                new(ColorPreservingEntryPointKind.LowLevelHelper, "ColorAwareTranslationComposer", "Capture boundary wrapper helper for translated markup ownership."),
            ["Mods/QudJP/Assemblies/src/Translation/ColorAwareTranslationComposer.cs:193:RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership"] =
                new(ColorPreservingEntryPointKind.LowLevelHelper, "ColorAwareTranslationComposer", "Whole-source boundary wrapper helper for translated markup ownership."),
            ["Mods/QudJP/Assemblies/src/Translation/ColorAwareTranslationComposer.cs:207:RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership"] =
                new(ColorPreservingEntryPointKind.LowLevelHelper, "ColorAwareTranslationComposer", "Whole-source boundary wrapper helper with explicit translated source length."),
            ["Mods/QudJP/Assemblies/src/Translation/ColorAwareTranslationComposer.cs:250:RestoreWholeSliceBoundaryWrappersPreservingTranslatedOwnership"] =
                new(ColorPreservingEntryPointKind.LowLevelHelper, "ColorAwareTranslationComposer", "Slice boundary wrapper helper for translated markup ownership."),
            ["Mods/QudJP/Assemblies/src/Translation/ColorAwareTranslationComposer.cs:265:RestoreSourceBoundaryWrappersByVisibleTextPreservingTranslatedOwnership"] =
                new(ColorPreservingEntryPointKind.LowLevelHelper, "ColorAwareTranslationComposer", "Whole-source wrapper helper keyed by visible text."),
            ["Mods/QudJP/Assemblies/src/Translation/ColorAwareTranslationComposer.cs:1218:RestoreSlice"] =
                new(ColorPreservingEntryPointKind.LowLevelHelper, "ColorAwareTranslationComposer", "Visible slice color restoration helper."),
            ["Mods/QudJP/Assemblies/src/Translation/ColorAwareTranslationComposer.cs:1282:RestoreMatchBoundaries"] =
                new(ColorPreservingEntryPointKind.LowLevelHelper, "ColorAwareTranslationComposer", "Regex match boundary restoration helper."),
            ["Mods/QudJP/Assemblies/src/Translation/ColorCodePreserver.cs:13:Strip"] =
                new(ColorPreservingEntryPointKind.LowLevelHelper, "ColorCodePreserver", "Tokenizer-level color strip primitive."),
            ["Mods/QudJP/Assemblies/src/Translation/ColorCodePreserver.cs:26:Restore"] =
                new(ColorPreservingEntryPointKind.LowLevelHelper, "ColorCodePreserver", "Tokenizer-level color restore primitive."),
            ["Mods/QudJP/Assemblies/src/Translation/DisplayNameRouteTranslation.cs:62:TranslatePreservingColors"] =
                new(ColorPreservingEntryPointKind.RouteContract, "DisplayNameRouteTranslation", "Translation-layer display-name route contract."),
            ["Mods/QudJP/Assemblies/src/Translation/DisplayNameRouteTranslation.cs:73:TranslateCapturePreservingColors"] =
                new(ColorPreservingEntryPointKind.RouteContract, "DisplayNameRouteTranslation", "Translation-layer display-name capture route contract."),
            ["Mods/QudJP/Assemblies/src/Translation/DisplayNameRouteTranslation.cs:80:StripLeadingEnglishArticlePreservingColors"] =
                new(ColorPreservingEntryPointKind.RouteContract, "DisplayNameRouteTranslation", "Translation-layer display-name capture preprocessing contract."),
            ["Mods/QudJP/Assemblies/src/Patches/GetDisplayNameRouteTranslator.cs:259:TranslatePreservingColors"] =
                new(ColorPreservingEntryPointKind.RouteAdapter, "DisplayNameRouteTranslation", "Patch-layer implementation of the display-name route contract."),
            ["Mods/QudJP/Assemblies/src/Patches/GetDisplayNameRouteTranslator.cs:1984:TranslateScopedExactPreservingColors"] =
                new(ColorPreservingEntryPointKind.RouteAdapter, "DisplayNameRouteTranslation", "Display-name scoped exact helper keeps display route ownership."),
            ["Mods/QudJP/Assemblies/src/Patches/DisplayNameCaptureTranslator.cs:5:TranslatePreservingColors"] =
                new(ColorPreservingEntryPointKind.RouteAdapter, "DisplayNameRouteTranslation", "Display-name capture adapter strips articles and direct markers before route translation."),
            ["Mods/QudJP/Assemblies/src/Patches/DisplayNameCaptureTranslator.cs:18:StripLeadingEnglishArticlePreservingColors"] =
                new(ColorPreservingEntryPointKind.RouteAdapter, "DisplayNameRouteTranslation", "Compatibility adapter for display-name capture article stripping."),
            ["Mods/QudJP/Assemblies/src/Patches/ActivatedAbilityNameTranslator.cs:59:TranslatePreservingColors"] =
                new(ColorPreservingEntryPointKind.RouteAdapter, "ActivatedAbilityNameTranslator", "Activated-ability-name color-preserving route adapter."),
            ["Mods/QudJP/Assemblies/src/Patches/GeneratedQuestTitleTranslator.cs:18:TranslatePreservingColors"] =
                new(ColorPreservingEntryPointKind.RouteAdapter, "GeneratedQuestTitleTranslator", "Generated quest title color-preserving route adapter."),
            ["Mods/QudJP/Assemblies/src/Patches/GeneratedQuestTitleTranslator.cs:39:TranslateEmbeddedPreservingColors"] =
                new(ColorPreservingEntryPointKind.RouteAdapter, "GeneratedQuestTitleTranslator", "Generated quest title embedded text color-preserving route adapter."),
            ["Mods/QudJP/Assemblies/src/Patches/GeneratedQuestTitleTranslator.cs:66:TryTranslatePreservingColors"] =
                new(ColorPreservingEntryPointKind.RouteAdapter, "GeneratedQuestTitleTranslator", "Generated quest title try-translate route adapter."),
            ["Mods/QudJP/Assemblies/src/Patches/LiquidVolumeFragmentTranslator.cs:556:TranslateLiquidPhrasePreservingColors"] =
                new(ColorPreservingEntryPointKind.RouteAdapter, "LiquidVolumeFragmentTranslator", "Liquid phrase fragment route adapter preserves color ownership for generated liquid text."),
            ["Mods/QudJP/Assemblies/src/Patches/SkillsAndPowersStatusScreenTranslationPatch.cs:167:TryTranslateExactLeafPreservingColors"] =
                new(ColorPreservingEntryPointKind.RouteAdapter, "SkillsAndPowersStatusScreenTranslationPatch", "Skills and powers exact leaf route adapter preserves color ownership."),
            ["Mods/QudJP/Assemblies/src/Patches/SkillsAndPowersStatusScreenTranslationPatch.cs:524:TryTranslateStructuredLinePreservingColors"] =
                new(ColorPreservingEntryPointKind.RouteAdapter, "SkillsAndPowersStatusScreenTranslationPatch", "Skills and powers structured line route adapter preserves color ownership."),
            ["Mods/QudJP/Assemblies/src/Patches/UITextSkinTranslationPatch.cs:233:TranslatePreservingColors"] =
                new(ColorPreservingEntryPointKind.RouteAdapter, "UITextSkinTranslationPatch", "UITextSkin-specific color-preserving sink route."),
            ["Mods/QudJP/Assemblies/src/Patches/UITextSkinTranslationPatch.cs:238:TranslatePreservingColors"] =
                new(ColorPreservingEntryPointKind.RouteAdapter, "UITextSkinTranslationPatch", "UITextSkin-specific color-preserving sink route with context details."),
        };

    internal static readonly string[] GenericPopupProducerRouteSymbols =
    {
        "PopupTranslationPatch.TranslatePopupTextForProducerRoute(",
        "PopupTranslationPatch.TranslatePopupMenuItemTextForProducerRoute(",
    };

    internal static readonly SortedDictionary<string, GenericPopupProducerRouteAllowance> GenericPopupProducerRouteAllowlist =
        new SortedDictionary<string, GenericPopupProducerRouteAllowance>(StringComparer.Ordinal)
        {
            ["Mods/QudJP/Assemblies/src/Patches/ActiveEffectMessageFrameOwnerTranslationPatch.cs|PopupTranslationPatch.TranslatePopupTextForProducerRoute("] =
                new(
                    1,
                    "ActiveEffectMessageFrame owner scope handles the fixed CardiacArrest restart popup only after the exact active-effect owner route is active."),
            ["Mods/QudJP/Assemblies/src/Patches/OldSaveContinueMenuPopupTranslationPatch.cs|PopupTranslationPatch.TranslatePopupTextForProducerRoute("] =
                new(
                    1,
                    "OldSaveContinueMenu is the owner-specific route for MainMenu/SaveManagement continue-menu old-save popups; it runs only inside that owner scope."),
            ["Mods/QudJP/Assemblies/src/Patches/OldSaveContinueMenuTranslationPatch.cs|PopupTranslationPatch.TranslatePopupTextForProducerRoute("] =
                new(
                    1,
                    "Old-save ContinueMenu owner scope runs before the generic Popup.Show fallback; this call only handles the fixed old-save popup template."),
            ["Mods/QudJP/Assemblies/src/Patches/PopupAskNumberTranslationPatch.cs|PopupTranslationPatch.TranslatePopupTextForProducerRoute("] =
                new(
                    1,
                    "AskNumber first checks the trade-screen owner template; this fallback is for fixed generic popup prompts."),
            ["Mods/QudJP/Assemblies/src/Patches/PopupAskStringTranslationPatch.cs|PopupTranslationPatch.TranslatePopupTextForProducerRoute("] =
                new(
                    1,
                    "AskString prompts are generic popup fixed text; route-specific dynamic prompts must add owner helpers before this fallback."),
            ["Mods/QudJP/Assemblies/src/Patches/PopupGetPopupOptionTranslationPatch.cs|PopupTranslationPatch.TranslatePopupMenuItemTextForProducerRoute("] =
                new(
                    1,
                    "Popup option text is a menu-item surface for fixed labels and shared popup menu translations."),
            ["Mods/QudJP/Assemblies/src/Patches/PopupMessageTranslationPatch.cs|PopupTranslationPatch.TranslatePopupTextForProducerRoute("] =
                new(
                    1,
                    "PopupMessage is a handoff surface for already-owner-translated text, fixed titles, and fixed button text."),
            ["Mods/QudJP/Assemblies/src/Patches/PopupPickOptionTranslationPatch.cs|PopupTranslationPatch.TranslatePopupMenuItemTextForProducerRoute("] =
                new(
                    2,
                    "PickOption menu-item objects and preserved option labels expose fixed option text through the shared popup menu route."),
            ["Mods/QudJP/Assemblies/src/Patches/SelectableTextMenuItemTranslationPatch.cs|PopupTranslationPatch.TranslatePopupMenuItemTextForProducerRoute("] =
                new(
                    1,
                    "SelectableTextMenuItem translates only display text after popup menu data is built, preserving command identity for tutorial guards."),
            ["Mods/QudJP/Assemblies/src/Patches/PopupPickOptionTranslationPatch.cs|PopupTranslationPatch.TranslatePopupTextForProducerRoute("] =
                new(
                    1,
                    "PickOption prompt/title/options are generic popup fixed text; dynamic option producers need owner helpers first."),
            ["Mods/QudJP/Assemblies/src/Patches/PopupShowColorPickerTranslationPatch.cs|PopupTranslationPatch.TranslatePopupTextForProducerRoute("] =
                new(
                    1,
                    "ColorPicker exposes fixed/shared popup color labels through the generic popup producer route after owner-specific routes have had a chance to run."),
            ["Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs|PopupTranslationPatch.TranslatePopupTextForProducerRoute("] =
                new(
                    1,
                    "Show first checks known owner handoffs; this fallback handles fixed popup text and the controlled PopupShow message-pattern route."),
            ["Mods/QudJP/Assemblies/src/Patches/TradeUiPopupTranslationPatch.cs|PopupTranslationPatch.TranslatePopupTextForProducerRoute("] =
                new(
                    1,
                    "Trade UI owner templates run first; this fallback is only for fixed/shared popup families after trade-specific ownership."),
            ["Mods/QudJP/Assemblies/src/QudTest/QudTestRouteExecutor.cs|PopupTranslationPatch.TranslatePopupMenuItemTextForProducerRoute("] =
                new(
                    1,
                    "QudTest route executor deliberately exposes shared popup menu-item routes for harness fixtures; it is not a production owner route."),
            ["Mods/QudJP/Assemblies/src/QudTest/QudTestRouteExecutor.cs|PopupTranslationPatch.TranslatePopupTextForProducerRoute("] =
                new(
                    2,
                    "QudTest route executor deliberately exposes shared popup text routes for harness fixtures; it is not a production owner route."),
        };

    internal static readonly SortedDictionary<string, int> ExpectedSymbolOccurrences =
        new SortedDictionary<string, int>(StringComparer.Ordinal)
        {
            ["Mods/QudJP/Assemblies/src/Patches/AbilityBarUpdateAbilitiesTextPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/AbilityBarButtonTextTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/ActionManagerRunSegmentTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/ActivatedAbilityCooldownTranslator.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 4,
            ["Mods/QudJP/Assemblies/src/Patches/ActivatedAbilityNameTranslator.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 2,
            ["Mods/QudJP/Assemblies/src/Patches/ActivatedAbilityNameTranslator.cs|GetDisplayNameRouteTranslator.TranslatePreservingColors("] = 3,
            ["Mods/QudJP/Assemblies/src/Patches/ActiveEffectMessageFrameOwnerTranslationPatch.cs|MessagePatternTranslator.Translate("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/ActiveEffectMessageFrameOwnerTranslationPatch.cs|PopupTranslationPatch.TranslatePopupTextForProducerRoute("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/ActiveEffectPopupQueueTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/ActiveEffectTextTranslator.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 3,
            ["Mods/QudJP/Assemblies/src/Patches/AutomatedExternalDefibrillatorTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/BaseMutationSelectVariantPopupTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/BeginBeingUnequippedFailureMessageTranslationPatch.cs|GetDisplayNameRouteTranslator.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/CharGenLocalizationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/CharGenProducerTranslationHelpers.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,

            ["Mods/QudJP/Assemblies/src/Patches/CharacterEffectLineTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/CharacterStatusScreenBindingPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/CharacterStatusScreenMutationDetailsPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 2,
            ["Mods/QudJP/Assemblies/src/Patches/CombatSkillMessageTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/ConversationDisplayTextPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/ConversationRewardPopupTranslationPatch.cs|MessagePatternTranslator.Translate("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/ConversationTemplateTranslator.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/CampfireCookFromRecipeTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/CampfirePreserveTranslationPatch.cs|GetDisplayNameRouteTranslator.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/CookingIngredientFragmentTranslator.cs|GetDisplayNameRouteTranslator.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/CrayonsPopupTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/CudgelConkPopupTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 2,
            ["Mods/QudJP/Assemblies/src/Patches/CyberneticsButcherableCyberneticTranslationPatch.cs|MessagePatternTranslator.Translate("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/CyberneticsTerminalTextTranslator.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/DeathWrapperFamilyTranslator.cs|GetDisplayNameRouteTranslator.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/DescriptionAssignmentOwnerTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/DescriptionLongDescriptionPatch.cs|DescriptionTextTranslator.TranslateLongDescription("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/DescriptionDetailReturnTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/DescriptionDetailReturnTranslationPatch.cs|DescriptionTextTranslator.TranslateLongDescription("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/DescriptionInspectStatusPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/DescriptionTextTranslator.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/DescriptionTextTranslator.cs|GetDisplayNameRouteTranslator.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/DescriptionTextTranslator.cs|MessagePatternTranslator.Translate("] = 4,
            ["Mods/QudJP/Assemblies/src/Patches/DisassemblyStartTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 2,
            ["Mods/QudJP/Assemblies/src/Patches/DisassemblyStartTranslationPatch.cs|GetDisplayNameRouteTranslator.TranslatePreservingColors("] = 2,
            ["Mods/QudJP/Assemblies/src/Patches/DisplayNameCaptureTranslator.cs|DisplayNameRouteTranslation.TranslateCapturePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Translation/DisplayNameRouteTranslation.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Translation/DisplayNamePlaceholderTranslator.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Translation/MessageFrameTranslator.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 2,
            ["Mods/QudJP/Assemblies/src/Translation/MessageFrameTranslator.cs|DisplayNameRouteTranslation.TranslateCapturePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Translation/MessagePatternTranslator.cs|DisplayNameRouteTranslation.TranslateCapturePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/EnergyStorageChargeStatusTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/EquipmentLineTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/ExaminerTranslationPatch.cs|GetDisplayNameRouteTranslator.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/FabricateFromSelfAbilityDescriptionTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/FabricateFromSelfAbilityDescriptionTranslationPatch.cs|GetDisplayNameRouteTranslator.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/DisplayNameSemanticPipeline.cs|GetDisplayNameRouteTranslator.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/AbilityBarAfterRenderTranslationPatch.cs|GetDisplayNameRouteTranslator.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/AbilityBarButtonTextTranslationPatch.cs|GetDisplayNameRouteTranslator.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/GameObjectPopupTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/GameObjectMoveTranslationPatch.cs|MessagePatternTranslator.Translate("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/GameObjectPerformThrowTranslationPatch.cs|MessagePatternTranslator.Translate("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/GasGenerationDescriptionTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/MutationSelfTargetPopupTranslationPatch.cs|MessagePatternTranslator.Translate("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/GameSummaryTextTranslator.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 2,
            ["Mods/QudJP/Assemblies/src/Patches/GameSummaryTextTranslator.cs|JournalPatternTranslator.Translate("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/GameManagerUpdateSelectedAbilityPatch.cs|GetDisplayNameRouteTranslator.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/SifrahTokenDescriptionTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 2,
            ["Mods/QudJP/Assemblies/src/Patches/SifrahTokenDescriptionTranslationPatch.cs|GetDisplayNameRouteTranslator.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/GetDisplayNameRouteTranslator.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 7,
            ["Mods/QudJP/Assemblies/src/Patches/GeneratedDisplayNameOwnerTranslationPatch.cs|GetDisplayNameRouteTranslator.TranslatePreservingColors("] = 3,
            ["Mods/QudJP/Assemblies/src/Patches/GeneratedQuestTitleTranslator.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/HelpRowTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/HighScoresScreenTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/HistoricStringExpanderPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/HookedOwnerTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Translation/HistoricNarrativeTextTranslator.cs|JournalPatternTranslator.Translate("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/InventoryAndEquipmentStatusScreenTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/InventoryActionDisplayTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/InventoryFireEventTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/InventoryFireEventTranslationPatch.cs|GetDisplayNameRouteTranslator.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/InventoryLineTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/InventoryLineTranslationPatch.cs|GetDisplayNameRouteTranslator.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/JournalNotificationTranslator.cs|JournalPatternTranslator.Translate("] = 2,
            ["Mods/QudJP/Assemblies/src/Patches/JournalLineTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/JournalTextTranslator.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/JournalTextTranslator.cs|JournalPatternTranslator.Translate("] = 3,
            ["Mods/QudJP/Assemblies/src/Patches/KeybindBoxTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 2,
            ["Mods/QudJP/Assemblies/src/Patches/KeybindRowTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/KeybindsScreenTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/LegacyGamepadPromptTranslationHelpers.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 2,
            ["Mods/QudJP/Assemblies/src/Patches/LiquidLoaderTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/LiquidVolumeFragmentTranslator.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 4,
            ["Mods/QudJP/Assemblies/src/Patches/LiquidLeakMessageTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/LoadingStatusTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/LocationFinderPopupTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/LookTooltipContentPatch.cs|DescriptionTextTranslator.TranslateLongDescription("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/LookTooltipInformationWrapPatch.cs|GetDisplayNameRouteTranslator.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/MainMenuLocalizationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/MessageLogPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/TinkeringDetailsLineTranslationPatch.cs|DescriptionTextTranslator.TranslateLongDescription("] = 2,
            ["Mods/QudJP/Assemblies/src/Patches/TinkeringDetailsLineTranslationPatch.cs|GetDisplayNameRouteTranslator.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/PlayerStatusBarProducerTranslationHelpers.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 2,
            ["Mods/QudJP/Assemblies/src/Patches/PhysicAmputateLimbTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 2,
            ["Mods/QudJP/Assemblies/src/Patches/PickTargetWindowTextTranslator.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 9,
            ["Mods/QudJP/Assemblies/src/Patches/MessageLogProducerTranslationHelpers.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 5,
            ["Mods/QudJP/Assemblies/src/Patches/MessageLogProducerTranslationHelpers.cs|MessagePatternTranslator.Translate("] = 4,
            ["Mods/QudJP/Assemblies/src/Patches/MessageLogProducerTranslationHelpers.cs|MessagePatternTranslator.TranslateIfPatternMatches("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/MutationGeneratedTextTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/OptionsLocalizationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/OldSaveContinueMenuPopupTranslationPatch.cs|PopupTranslationPatch.TranslatePopupTextForProducerRoute("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/OldSaveContinueMenuTranslationPatch.cs|PopupTranslationPatch.TranslatePopupTextForProducerRoute("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/PopupAskNumberTranslationPatch.cs|PopupTranslationPatch.TranslatePopupTextForProducerRoute("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/PopupAskStringTranslationPatch.cs|PopupTranslationPatch.TranslatePopupTextForProducerRoute("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/PopupGetPopupOptionTranslationPatch.cs|PopupTranslationPatch.TranslatePopupMenuItemTextForProducerRoute("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 2,
            ["Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs|GetDisplayNameRouteTranslator.TranslatePreservingColors("] = 3,
            ["Mods/QudJP/Assemblies/src/Patches/PickGameObjectScreenTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/PhysicsProcessTakeDamageTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 2,
            ["Mods/QudJP/Assemblies/src/Patches/PhysicsProcessTakeDamageTranslationPatch.cs|MessagePatternTranslator.TranslateIfPatternMatches("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/PopupMessageTranslationPatch.cs|PopupTranslationPatch.TranslatePopupTextForProducerRoute("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/PopupPickOptionTranslationPatch.cs|GetDisplayNameRouteTranslator.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/PopupPickOptionTranslationPatch.cs|PopupTranslationPatch.TranslatePopupTextForProducerRoute("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/PopupPickOptionTranslationPatch.cs|PopupTranslationPatch.TranslatePopupMenuItemTextForProducerRoute("] = 2,
            ["Mods/QudJP/Assemblies/src/Patches/PopupShowColorPickerTranslationPatch.cs|PopupTranslationPatch.TranslatePopupTextForProducerRoute("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs|MessagePatternTranslator.TranslateIfPatternMatches("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/QuestLogTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/QuestLifecyclePopupTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/QuestsLineTranslationPatch.cs|GetDisplayNameRouteTranslator.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/QudMutationsModuleWindowHandleMenuOptionPopupTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/QudMutationsModuleWindowVariantPopupTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/RandomAltarBaetylTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/RandomAltarBaetylTranslationPatch.cs|GetDisplayNameRouteTranslator.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/RepairTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/QudTest/QudTestRouteExecutor.cs|PopupTranslationPatch.TranslatePopupMenuItemTextForProducerRoute("] = 1,
            ["Mods/QudJP/Assemblies/src/QudTest/QudTestRouteExecutor.cs|PopupTranslationPatch.TranslatePopupTextForProducerRoute("] = 2,
            ["Mods/QudJP/Assemblies/src/Patches/SelectableTextMenuItemTranslationPatch.cs|PopupTranslationPatch.TranslatePopupMenuItemTextForProducerRoute("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/SavesApiFatalSaveErrorTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 2,
            ["Mods/QudJP/Assemblies/src/Patches/TextFilterSpeechStatusTranslationPatches.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/TradeUiPopupTranslationPatch.cs|MessagePatternTranslator.TranslateIfPatternMatches("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/TradeUiPopupTranslationPatch.cs|PopupTranslationPatch.TranslatePopupTextForProducerRoute("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/TradeUiPopupTranslationPatch.cs|GetDisplayNameRouteTranslator.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/TradeLineTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/TradeLineTranslationPatch.cs|GetDisplayNameRouteTranslator.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/VillageSignatureItemTranslationPatch.cs|GetDisplayNameRouteTranslator.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/WaterRitualPopupTranslationPatch.cs|JournalPatternTranslator.Translate("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/SultanShrineWrapperTranslator.cs|JournalPatternTranslator.Translate("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/SkillsAndPowersStatusScreenTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 7,
            ["Mods/QudJP/Assemblies/src/Patches/DeathReasonTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs|PopupTranslationPatch.TranslatePopupTextForProducerRoute("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/SinkPrereqTextFieldTranslator.cs|UITextSkinTranslationPatch.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/SingleCallsiteOwnerPopupTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 2,
            ["Mods/QudJP/Assemblies/src/Patches/SingleCallsiteOwnerPopupTranslationPatch.cs|GetDisplayNameRouteTranslator.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/SifrahTokenItemPopupTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/StatusScreenPopupTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/SummaryBlockControlTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/TinkeringBuildPopupTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/TonicApplicatorTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/UiMenuOptionDescriptionTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/UiBindingTranslationHelpers.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/WorldModsTextTranslator.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/WorldPartFixedDisplayNameTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/WorldPartGeneratedDisplayNameTranslationPatches.cs|GetDisplayNameRouteTranslator.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/WorldCreationProgressTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/WorldGenerationScreenTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/XDidYTranslationPatch.cs|GetDisplayNameRouteTranslator.TranslatePreservingColors("] = 1,
        };

    internal sealed record GenericPopupProducerRouteAllowance(int ExpectedCount, string Reason);
}

internal static class TestProjectPaths
{
    internal static string GetRepositoryRoot()
    {
        var current = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "Mods", "QudJP", "Assemblies", "QudJP.csproj");
            if (File.Exists(candidate))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("QudJP repository root could not be located from the test directory.");
    }
}
