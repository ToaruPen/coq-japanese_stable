using System.Text.RegularExpressions;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class ColorRouteCatalogTests
{
    private static readonly Regex FileScopedNamespacePattern =
        new Regex("^namespace\\s+.+?;$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

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
            var lines = File.ReadAllLines(file);

            for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                var line = lines[lineIndex].Trim();
                if (line.Length == 0 || FileScopedNamespacePattern.IsMatch(line))
                {
                    continue;
                }

                for (var symbolIndex = 0; symbolIndex < routeSymbols.Count; symbolIndex++)
                {
                    var symbol = routeSymbols[symbolIndex];
                    if (!line.Contains(symbol, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var key = relativePath + "|" + symbol;
                    counts[key] = counts.TryGetValue(key, out var count) ? count + 1 : 1;
                }
            }
        }

        return counts;
    }
}

internal static class ColorRouteCatalog
{
    internal static readonly string[] RouteSymbols =
    {
        "MessagePatternTranslator.Translate(",
        "JournalPatternTranslator.Translate(",
        "ColorAwareTranslationComposer.TranslatePreservingColors(",
        "GetDisplayNameRouteTranslator.TranslatePreservingColors(",
        "UITextSkinTranslationPatch.TranslatePreservingColors(",
        "PopupTranslationPatch.TranslatePopupTextForRoute(",
        "PopupTranslationPatch.TranslatePopupMenuItemTextForRoute(",
        "PopupTranslationPatch.TranslatePopupTextForProducerRoute(",
        "PopupTranslationPatch.TranslatePopupMenuItemTextForProducerRoute(",
    };

    internal static readonly SortedDictionary<string, int> ExpectedSymbolOccurrences =
        new SortedDictionary<string, int>(StringComparer.Ordinal)
        {
            ["Mods/QudJP/Assemblies/src/Patches/AbilityBarUpdateAbilitiesTextPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/ActiveEffectTextTranslator.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 2,
            ["Mods/QudJP/Assemblies/src/Patches/CharGenLocalizationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/CharGenProducerTranslationHelpers.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/ConversationDisplayTextPatch.cs|UITextSkinTranslationPatch.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/DeathWrapperFamilyTranslator.cs|GetDisplayNameRouteTranslator.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/DescriptionLongDescriptionPatch.cs|UITextSkinTranslationPatch.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/DescriptionShortDescriptionPatch.cs|UITextSkinTranslationPatch.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/GetDisplayNamePatch.cs|GetDisplayNameRouteTranslator.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/GetDisplayNameProcessPatch.cs|GetDisplayNameRouteTranslator.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/AbilityBarAfterRenderTranslationPatch.cs|GetDisplayNameRouteTranslator.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/HistoricStringExpanderPatch.cs|UITextSkinTranslationPatch.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/InventoryAndEquipmentStatusScreenTranslationPatch.cs|UITextSkinTranslationPatch.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/InventoryLocalizationPatch.cs|GetDisplayNameRouteTranslator.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/JournalTextTranslator.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/JournalTextTranslator.cs|JournalPatternTranslator.Translate("] = 2,
            ["Mods/QudJP/Assemblies/src/Patches/LoadingStatusTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/LookTooltipContentPatch.cs|UITextSkinTranslationPatch.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/MainMenuLocalizationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/PlayerStatusBarProducerTranslationHelpers.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/MessageLogProducerTranslationHelpers.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 2,
            ["Mods/QudJP/Assemblies/src/Patches/MessageLogProducerTranslationHelpers.cs|MessagePatternTranslator.Translate("] = 2,
            ["Mods/QudJP/Assemblies/src/Patches/PopupMessageTranslationPatch.cs|PopupTranslationPatch.TranslatePopupTextForProducerRoute("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/QudMenuBottomContextTranslationPatch.cs|PopupTranslationPatch.TranslatePopupMenuItemTextForRoute("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/SkillsAndPowersStatusScreenTranslationPatch.cs|ColorAwareTranslationComposer.TranslatePreservingColors("] = 3,
            ["Mods/QudJP/Assemblies/src/Patches/DeathReasonTranslationPatch.cs|UITextSkinTranslationPatch.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs|PopupTranslationPatch.TranslatePopupTextForProducerRoute("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/SinkPrereqTextFieldTranslator.cs|UITextSkinTranslationPatch.TranslatePreservingColors("] = 1,
            ["Mods/QudJP/Assemblies/src/Patches/XDidYTranslationPatch.cs|GetDisplayNameRouteTranslator.TranslatePreservingColors("] = 1,
        };
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
