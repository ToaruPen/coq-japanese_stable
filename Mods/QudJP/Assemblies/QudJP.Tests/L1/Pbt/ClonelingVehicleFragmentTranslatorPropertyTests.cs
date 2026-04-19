using FsCheck.Fluent;

using FsCheckProperty = FsCheck.Property;

using QudJP.Patches;

namespace QudJP.Tests.L1.Pbt;

[TestFixture]
[Category("L1")]
public sealed class ClonelingVehicleFragmentTranslatorPropertyTests
{
    private const string ReplaySeed = "642531987,97531";

    [FsCheck.NUnit.Property(Arbitrary = new[] { typeof(ClonelingVehicleFragmentTranslatorArbitraries) }, MaxTest = 100, Replay = ReplaySeed)]
    public FsCheckProperty TryTranslatePopupMessage_PreservesLiquidWrappers(ClonelingPopupCase sample)
    {
        var translated = ClonelingVehicleFragmentTranslator.TryTranslatePopupMessage(
            sample.Source,
            nameof(ClonelingVehicleFragmentTranslatorPropertyTests),
            "WorldParts.Popup",
            out var actual);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(actual, Is.EqualTo(sample.ExpectedTranslated));
        });

        return true.ToProperty();
    }

    [FsCheck.NUnit.Property(Arbitrary = new[] { typeof(ClonelingVehicleFragmentTranslatorArbitraries) }, MaxTest = 100, Replay = ReplaySeed)]
    public FsCheckProperty TryTranslateQueuedMessage_PreservesLiquidWrappers(ClonelingQueuedCase sample)
    {
        var translated = ClonelingVehicleFragmentTranslator.TryTranslateQueuedMessage(
            sample.Source,
            nameof(ClonelingVehicleFragmentTranslatorPropertyTests),
            "WorldParts.Queue",
            out var actual);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(actual, Is.EqualTo(sample.ExpectedTranslated));
        });

        return true.ToProperty();
    }

    [FsCheck.NUnit.Property(Arbitrary = new[] { typeof(ClonelingVehicleFragmentTranslatorArbitraries) }, MaxTest = 100, Replay = ReplaySeed)]
    public FsCheckProperty TryTranslatePopupMessage_LeavesDirectMarkedTextUntouched(ClonelingPopupPassthroughCase sample)
    {
        var translated = ClonelingVehicleFragmentTranslator.TryTranslatePopupMessage(
            sample.Source,
            nameof(ClonelingVehicleFragmentTranslatorPropertyTests),
            "WorldParts.Popup",
            out var actual);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(actual, Is.EqualTo(sample.Source));
        });

        return true.ToProperty();
    }

    [FsCheck.NUnit.Property(Arbitrary = new[] { typeof(ClonelingVehicleFragmentTranslatorArbitraries) }, MaxTest = 100, Replay = ReplaySeed)]
    public FsCheckProperty TryTranslateQueuedMessage_LeavesDirectMarkedTextUntouched(ClonelingQueuedPassthroughCase sample)
    {
        var translated = ClonelingVehicleFragmentTranslator.TryTranslateQueuedMessage(
            sample.Source,
            nameof(ClonelingVehicleFragmentTranslatorPropertyTests),
            "WorldParts.Queue",
            out var actual);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(actual, Is.EqualTo(sample.Source));
        });

        return true.ToProperty();
    }

    [Test]
    public void TryTranslatePopupMessage_LeavesEmptyInputUntouched()
    {
        var translated = ClonelingVehicleFragmentTranslator.TryTranslatePopupMessage(
            string.Empty,
            nameof(ClonelingVehicleFragmentTranslatorPropertyTests),
            "WorldParts.Popup",
            out var actual);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(actual, Is.EqualTo(string.Empty));
        });
    }

    [Test]
    public void TryTranslatePopupMessage_LeavesNonMatchingEnglishUntouched()
    {
        const string source = "You do not have 1 dram of nothing in particular?";
        var translated = ClonelingVehicleFragmentTranslator.TryTranslatePopupMessage(
            source,
            nameof(ClonelingVehicleFragmentTranslatorPropertyTests),
            "WorldParts.Popup",
            out var actual);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(actual, Is.EqualTo(source));
        });
    }

    [Test]
    public void TryTranslateQueuedMessage_LeavesEmptyInputUntouched()
    {
        var translated = ClonelingVehicleFragmentTranslator.TryTranslateQueuedMessage(
            string.Empty,
            nameof(ClonelingVehicleFragmentTranslatorPropertyTests),
            "WorldParts.Queue",
            out var actual);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(actual, Is.EqualTo(string.Empty));
        });
    }

    [Test]
    public void TryTranslateQueuedMessage_LeavesNonMatchingEnglishUntouched()
    {
        const string source = "Your onboard systems are out of nothing in particular?";
        var translated = ClonelingVehicleFragmentTranslator.TryTranslateQueuedMessage(
            source,
            nameof(ClonelingVehicleFragmentTranslatorPropertyTests),
            "WorldParts.Queue",
            out var actual);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(actual, Is.EqualTo(source));
        });
    }
}
