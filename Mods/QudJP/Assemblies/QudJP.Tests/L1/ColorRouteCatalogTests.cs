using System.Text.RegularExpressions;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class ColorRouteCatalogTests
{
    [Test]
    public void Catalog_CompletelyCoversColorSensitiveTranslationCallSites()
    {
        var root = TestProjectPaths.GetRepositoryRoot();
        var sourceRoot = Path.Combine(root, "Mods", "QudJP", "Assemblies", "src");
        var actual = ScanSymbolOccurrences(sourceRoot, ColorRouteCatalog.RouteSymbols);

        Assert.That(actual, Is.EquivalentTo(ColorRouteCatalog.ExpectedSymbolOccurrences));
    }

    [Test]
    public void Catalog_OnlyReferencesExistingSourceFiles()
    {
        var root = TestProjectPaths.GetRepositoryRoot();
        foreach (var entry in ColorRouteCatalog.ExpectedSymbolOccurrences.Keys)
        {
            var relativePath = entry[..entry.IndexOf('|')];
            var fullPath = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.That(File.Exists(fullPath), Is.True, $"Catalog file not found: {relativePath}");
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

    private static int CountOccurrences(string source, string symbol)
    {
        var pattern = Regex.Escape(symbol)
            .Replace("\\.", "\\s*\\.\\s*", StringComparison.Ordinal)
            .Replace("\\(", "\\s*\\(", StringComparison.Ordinal);
        return Regex.Matches(source, pattern, RegexOptions.CultureInvariant | RegexOptions.Singleline).Count;
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
}

internal static class ColorRouteCatalog
{
    internal static readonly string[] RouteSymbols =
    {
        "MessagePatternTranslator.Translate(",
        "JournalPatternTranslator.Translate(",
        "ColorAwareTranslationComposer.TranslatePreservingColors(",
        "DescriptionTextTranslator.TranslateLongDescription(",
        "GetDisplayNameRouteTranslator.TranslatePreservingColors(",
        "UITextSkinTranslationPatch.TranslatePreservingColors(",
        "PopupTranslationPatch.TranslatePopupTextForRoute(",
        "PopupTranslationPatch.TranslatePopupMenuItemTextForRoute(",
        "PopupTranslationPatch.TranslatePopupTextForProducerRoute(",
        "PopupTranslationPatch.TranslatePopupMenuItemTextForProducerRoute(",
    };

    internal static readonly string[] GenericPopupProducerRouteSymbols =
    {
        "PopupTranslationPatch.TranslatePopupTextForProducerRoute(",
        "PopupTranslationPatch.TranslatePopupMenuItemTextForProducerRoute(",
    };

    internal static readonly SortedDictionary<string, GenericPopupProducerRouteAllowance> GenericPopupProducerRouteAllowlist =
        new SortedDictionary<string, GenericPopupProducerRouteAllowance>(StringComparer.Ordinal)
        {
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
                    1,
                    "PickOption menu-item objects expose fixed option labels through the shared popup menu route."),
            ["Mods/QudJP/Assemblies/src/Patches/PopupPickOptionTranslationPatch.cs|PopupTranslationPatch.TranslatePopupTextForProducerRoute("] =
                new(
                    2,
                    "PickOption prompt/title/options are generic popup fixed text; dynamic option producers need owner helpers first."),
            ["Mods/QudJP/Assemblies/src/Patches/PopupShowSpaceTranslationPatch.cs|PopupTranslationPatch.TranslatePopupTextForProducerRoute("] =
                new(
                    2,
                    "ShowSpace exposes generic popup body/title text without a narrower known producer owner."),
            ["Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs|PopupTranslationPatch.TranslatePopupTextForProducerRoute("] =
                new(
                    1,
                    "Show first checks known owner handoffs; this fallback handles fixed popup text and the controlled PopupShow message-pattern route."),
            ["Mods/QudJP/Assemblies/src/Patches/QudMenuBottomContextTranslationPatch.cs|PopupTranslationPatch.TranslatePopupMenuItemTextForProducerRoute("] =
                new(
                    1,
                    "Bottom context buttons are fixed menu labels routed through the shared popup menu-item translator."),
            ["Mods/QudJP/Assemblies/src/Patches/TradeUiPopupTranslationPatch.cs|PopupTranslationPatch.TranslatePopupTextForProducerRoute("] =
                new(
                    1,
                    "Trade UI owner templates run first; this fallback is only for fixed/shared popup families after trade-specific ownership."),
        };

    internal static readonly SortedDictionary<string, int> ExpectedSymbolOccurrences =
        new SortedDictionary<string, int>(StringComparer.Ordinal)
        {
            ["Mods/QudJP/Assemblies/src/Patches/AbilityBarUpdateAbilitiesTextPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/ActivatedAbilityNameTranslator.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/ActiveEffectTextTranslator.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 3,
            ["Mods/QudJP/Assemblies/src/Patches/CharGenLocalizationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/CharGenProducerTranslationHelpers.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,

            ["Mods/QudJP/Assemblies/src/Patches/CharacterEffectLineTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/CharacterStatusScreenBindingPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/CharacterStatusScreenMutationDetailsPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 2,
            ["Mods/QudJP/Assemblies/src/Patches/ConversationDisplayTextPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/ConversationTemplateTranslator.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/CyberneticsTerminalTextTranslator.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/DeathWrapperFamilyTranslator.cs|GetDisplayNameRouteTranslator.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/DescriptionLongDescriptionPatch.cs|DescriptionTextTranslator.TranslateLongDescription("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/DescriptionInspectStatusPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/DescriptionTextTranslator.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/DescriptionTextTranslator.cs|MessagePatternTranslator.Translate("] = 2,
            ["Mods/QudJP/Assemblies/src/Patches/EquipmentLineTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/GetDisplayNamePatch.cs|GetDisplayNameRouteTranslator.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/GetDisplayNameProcessPatch.cs|GetDisplayNameRouteTranslator.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/AbilityBarAfterRenderTranslationPatch.cs|GetDisplayNameRouteTranslator.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/AbilityBarButtonTextTranslationPatch.cs|GetDisplayNameRouteTranslator.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/GameSummaryTextTranslator.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 2,
            ["Mods/QudJP/Assemblies/src/Patches/GameSummaryTextTranslator.cs|JournalPatternTranslator.Translate("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/GameManagerUpdateSelectedAbilityPatch.cs|GetDisplayNameRouteTranslator.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/HelpRowTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/HighScoresScreenTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/HistoricStringExpanderPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Translation/HistoricNarrativeTextTranslator.cs|JournalPatternTranslator.Translate("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/InventoryAndEquipmentStatusScreenTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/InventoryLineTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/InventoryLocalizationPatch.cs|GetDisplayNameRouteTranslator.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/JournalNotificationTranslator.cs|JournalPatternTranslator.Translate("] = 2,
            ["Mods/QudJP/Assemblies/src/Patches/JournalTextTranslator.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/JournalTextTranslator.cs|JournalPatternTranslator.Translate("] = 2,
            ["Mods/QudJP/Assemblies/src/Patches/KeybindRowTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/KeybindsScreenTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/LoadingStatusTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/LookTooltipContentPatch.cs|DescriptionTextTranslator.TranslateLongDescription("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/MainMenuLocalizationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/PlayerStatusBarProducerTranslationHelpers.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 2,
            ["Mods/QudJP/Assemblies/src/Patches/MessageLogProducerTranslationHelpers.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 5,
            ["Mods/QudJP/Assemblies/src/Patches/MessageLogProducerTranslationHelpers.cs|MessagePatternTranslator.Translate("] = 4,
            ["Mods/QudJP/Assemblies/src/Patches/OptionsLocalizationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/PopupAskNumberTranslationPatch.cs|PopupTranslationPatch.TranslatePopupTextForProducerRoute("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/PopupAskStringTranslationPatch.cs|PopupTranslationPatch.TranslatePopupTextForProducerRoute("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/PopupGetPopupOptionTranslationPatch.cs|PopupTranslationPatch.TranslatePopupMenuItemTextForProducerRoute("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/PickGameObjectScreenTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/PopupMessageTranslationPatch.cs|PopupTranslationPatch.TranslatePopupTextForProducerRoute("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/PopupPickOptionTranslationPatch.cs|PopupTranslationPatch.TranslatePopupTextForProducerRoute("] = 2,
            ["Mods/QudJP/Assemblies/src/Patches/PopupPickOptionTranslationPatch.cs|PopupTranslationPatch.TranslatePopupMenuItemTextForProducerRoute("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs|MessagePatternTranslator.Translate("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/QudMenuBottomContextTranslationPatch.cs|PopupTranslationPatch.TranslatePopupMenuItemTextForProducerRoute("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/TradeUiPopupTranslationPatch.cs|MessagePatternTranslator.Translate("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/TradeUiPopupTranslationPatch.cs|PopupTranslationPatch.TranslatePopupTextForProducerRoute("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/TradeLineTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/TradeLineTranslationPatch.cs|GetDisplayNameRouteTranslator.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/SultanShrineWrapperTranslator.cs|JournalPatternTranslator.Translate("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/SkillsAndPowersLineTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/SkillsAndPowersStatusScreenTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 3,
            ["Mods/QudJP/Assemblies/src/Patches/DeathReasonTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs|PopupTranslationPatch.TranslatePopupTextForProducerRoute("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/PopupShowSpaceTranslationPatch.cs|PopupTranslationPatch.TranslatePopupTextForProducerRoute("] = 2,
            ["Mods/QudJP/Assemblies/src/Patches/SinkPrereqTextFieldTranslator.cs|UITextSkinTranslationPatch.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/SummaryBlockControlTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/UiBindingTranslationHelpers.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
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
