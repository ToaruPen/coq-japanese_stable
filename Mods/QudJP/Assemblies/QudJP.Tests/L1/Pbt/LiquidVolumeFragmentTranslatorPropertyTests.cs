using FsCheck.Fluent;

using FsCheckProperty = FsCheck.Property;

using QudJP.Patches;

namespace QudJP.Tests.L1.Pbt;

[TestFixture]
[Category("L1")]
public sealed class LiquidVolumeFragmentTranslatorPropertyTests
{
    private const string ReplaySeed = "753124689,97531";

    [FsCheck.NUnit.Property(Arbitrary = new[] { typeof(LiquidVolumeFragmentTranslatorArbitraries) }, MaxTest = 100, Replay = ReplaySeed)]
    public FsCheckProperty TryTranslatePopupMessage_PreservesStatusWrappers(LiquidStatusCase sample)
    {
        var translated = LiquidVolumeFragmentTranslator.TryTranslatePopupMessage(
            sample.Source,
            nameof(LiquidVolumeFragmentTranslatorPropertyTests),
            "LiquidVolume",
            out var actual);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(actual, Is.EqualTo(sample.ExpectedTranslated));
        });

        return true.ToProperty();
    }

    [FsCheck.NUnit.Property(Arbitrary = new[] { typeof(LiquidVolumeFragmentTranslatorArbitraries) }, MaxTest = 100, Replay = ReplaySeed)]
    public FsCheckProperty TryTranslatePopupMessage_PreservesOwnershipTargetWrappers(LiquidOwnershipCase sample)
    {
        var translated = LiquidVolumeFragmentTranslator.TryTranslatePopupMessage(
            sample.Source,
            nameof(LiquidVolumeFragmentTranslatorPropertyTests),
            "LiquidVolume",
            out var actual);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(actual, Is.EqualTo(sample.ExpectedTranslated));
        });

        return true.ToProperty();
    }

    [FsCheck.NUnit.Property(Arbitrary = new[] { typeof(LiquidVolumeFragmentTranslatorArbitraries) }, MaxTest = 100, Replay = ReplaySeed)]
    public FsCheckProperty TryTranslatePopupMessage_TranslatesTargetRules(LiquidTargetRuleCase sample)
    {
        var translated = LiquidVolumeFragmentTranslator.TryTranslatePopupMessage(
            sample.Source,
            nameof(LiquidVolumeFragmentTranslatorPropertyTests),
            "LiquidVolume",
            out var actual);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(actual, Is.EqualTo(sample.ExpectedTranslated));
        });

        return true.ToProperty();
    }

    [FsCheck.NUnit.Property(Arbitrary = new[] { typeof(LiquidVolumeFragmentTranslatorArbitraries) }, MaxTest = 100, Replay = ReplaySeed)]
    public FsCheckProperty TryTranslatePopupMessage_TranslatesPourIntoTargets(LiquidPourIntoCase sample)
    {
        var translated = LiquidVolumeFragmentTranslator.TryTranslatePopupMessage(
            sample.Source,
            nameof(LiquidVolumeFragmentTranslatorPropertyTests),
            "LiquidVolume",
            out var actual);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(actual, Is.EqualTo(sample.ExpectedTranslated));
        });

        return true.ToProperty();
    }

    [FsCheck.NUnit.Property(Arbitrary = new[] { typeof(LiquidVolumeFragmentTranslatorArbitraries) }, MaxTest = 100, Replay = ReplaySeed)]
    public FsCheckProperty TryTranslatePopupMessage_LeavesUnmatchedTextUntouched(LiquidPassthroughCase sample)
    {
        var translated = LiquidVolumeFragmentTranslator.TryTranslatePopupMessage(
            sample.Source,
            nameof(LiquidVolumeFragmentTranslatorPropertyTests),
            "LiquidVolume",
            out var actual);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(actual, Is.EqualTo(sample.Source));
        });

        return true.ToProperty();
    }
}
