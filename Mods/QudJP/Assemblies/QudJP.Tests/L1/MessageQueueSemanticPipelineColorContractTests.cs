using System.Text.RegularExpressions;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class MessageQueueSemanticPipelineColorContractTests
{
    private static readonly string[] ReviewedNonColorAwareTranslators =
    {
        "AbilityManagerShowTranslationPatch",
        "AdrenalControlTranslationPatch",
        "AmnesiaTranslationPatch",
        "BlinkingTicTranslationPatch",
        "BrainThinkTranslationPatch",
        "BrittleBonesTranslationPatch",
        "ClonelingVehicleTranslationPatch",
        "EelSpawnTranslationPatch",
        "EffectStaticMessageTranslationPatch",
        "ElectromagneticImpulseTranslationPatch",
        "EnclosingTranslationPatch",
        "ErosTeleportationTranslationPatch",
        "FearAuraTranslationPatch",
        "GameObjectDieTranslationPatch",
        "GiantClamTeleportTranslationPatch",
        "GlotrotOnsetTranslationPatch",
        "GritGateTerminalScreenMessageTranslationPatch",
        "HealingTranslationPatch",
        "IllRemoveTranslationPatch",
        "IronshankOnsetTranslationPatch",
        "IronshankTranslationPatch",
        "JoppaZealotTranslationPatch",
        "LiquidVolumeTranslationPatch",
        "MeditatingTranslationPatch",
        "MonochromeOnsetTranslationPatch",
        "MutationAbsorptionHealingTranslationPatch",
        "OnEatRewardMessageTranslationPatch",
        "PersuasionRebukeRobotTranslationPatch",
        "PreacherHomilyTranslationPatch",
        "PrefixedOwnerQueueTranslationPatch",
        "RegenerationTranslationPatch",
        "ShortBladesHobbleTranslationPatch",
        "SingleCallsiteOwnerQueueTranslationPatch",
        "SixDayZealotTranslationPatch",
        "StasisTranslationPatch",
        "StressedTranslationPatch",
        "SvardymSystemTranslationPatch",
        "SystemStaticMessageTranslationPatch",
        "TerrainTravelTranslationPatch",
        "TombAnchorSystemTranslationPatch",
        "WishCommandQueueTranslationPatch",
        "XrlCoreHotloadConfigurationTranslationPatch",
        "XrlGameTranslationPatch",
    };

    [Test]
    public void MessageQueueTranslatorsWithoutColorAwareHelpers_AreExplicitlyReviewed()
    {
        var patchesRoot = Path.Combine(
            FindRepositoryRoot().FullName,
            "Mods/QudJP/Assemblies/src/Patches");
        var pipelineSource = File.ReadAllText(Path.Combine(patchesRoot, "MessageQueueSemanticPipeline.cs"));
        var translatorNames = Regex.Matches(
                pipelineSource,
                @"^\s*(?<name>[A-Za-z0-9_]+)\.TryTranslateQueuedMessage,",
                RegexOptions.Multiline | RegexOptions.CultureInvariant)
            .Select(match => match.Groups["name"].Value)
            .ToArray();

        var nonColorAware = new List<string>();
        foreach (var translatorName in translatorNames)
        {
            var sourcePath = Path.Combine(patchesRoot, $"{translatorName}.cs");
            Assert.That(File.Exists(sourcePath), Is.True, $"Missing translator source: {sourcePath}");

            var source = File.ReadAllText(sourcePath);
            if (!HasKnownColorPreservationRoute(source))
            {
                nonColorAware.Add(translatorName);
            }
        }

        Assert.That(nonColorAware, Is.EquivalentTo(ReviewedNonColorAwareTranslators));
    }

    [Test]
    public void RepresentativeQueuedPatternTranslation_PreservesColorTagsInJapaneseOutput()
    {
        var dictionaryPath = Path.Combine(
            FindRepositoryRoot().FullName,
            "Mods/QudJP/Localization/Dictionaries/messages.ja.json");
        MessagePatternTranslator.SetPatternFileForTests(dictionaryPath);

        try
        {
            var translated = MessagePatternTranslator.Translate("You gain {{C|75}} XP!");

            Assert.That(translated, Is.EqualTo("あなたは経験値を{{C|75}}獲得した"));
        }
        finally
        {
            MessagePatternTranslator.ResetForTests();
        }
    }

    private static bool HasKnownColorPreservationRoute(string source)
    {
        return source.Contains("ColorAwareTranslationComposer", StringComparison.Ordinal)
            || source.Contains("TranslatePreservingColors", StringComparison.Ordinal)
            || source.Contains("MessageLogProducerTranslationHelpers.TryPreparePatternMessage", StringComparison.Ordinal)
            || source.Contains("MessagePatternTranslator.Translate", StringComparison.Ordinal)
            || source.Contains("MessagePatternTranslator.TranslateIfPatternMatches", StringComparison.Ordinal);
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(
                    directory.FullName,
                    "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs")))
            {
                return directory;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from test base directory.");
    }
}
