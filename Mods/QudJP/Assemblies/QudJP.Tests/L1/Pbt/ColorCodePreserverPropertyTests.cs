using FsCheck;
using FsCheck.Fluent;
using FsCheck.NUnit;

using FsCheckProperty = FsCheck.Property;

namespace QudJP.Tests.L1.Pbt;

[TestFixture]
[Category("L1")]
public sealed class ColorCodePreserverPropertyTests
{
    private const string ReplaySeed = "123456789,97531";

    [FsCheck.NUnit.Property(Arbitrary = new[] { typeof(ColorCodePreserverArbitraries) }, MaxTest = 200, Replay = ReplaySeed)]
    public FsCheckProperty StripThenRestore_PreservesSupportedMarkup(ColorizedCase sample)
    {
        var (stripped, spans) = ColorCodePreserver.Strip(sample.Source);
        var restored = ColorCodePreserver.Restore(sample.TranslatedVisibleText, spans);

        Assert.Multiple(() =>
        {
            Assert.That(stripped, Is.EqualTo(sample.VisibleText));
            Assert.That(restored, Is.EqualTo(sample.ExpectedRestored));
        });

        return true.ToProperty();
    }

    [FsCheck.NUnit.Property(Arbitrary = new[] { typeof(ColorCodePreserverArbitraries) }, MaxTest = 200, Replay = ReplaySeed)]
    public FsCheckProperty StripThenRestore_DoesNotChangeVisibleText(ColorizedCase sample)
    {
        var (stripped, spans) = ColorCodePreserver.Strip(sample.Source);
        var restored = ColorCodePreserver.Restore(stripped, spans);

        Assert.That(restored, Is.EqualTo(sample.Source));

        return true.ToProperty();
    }

    [FsCheck.NUnit.Property(Arbitrary = new[] { typeof(ColorCodePreserverArbitraries) }, MaxTest = 200, Replay = ReplaySeed)]
    public FsCheckProperty StripThenRestore_PreservesForegroundCodes(ForegroundColorCodeCase sample)
    {
        var (stripped, spans) = ColorCodePreserver.Strip(sample.Source);
        var restored = ColorCodePreserver.Restore(sample.TranslatedVisibleText, spans);

        Assert.Multiple(() =>
        {
            Assert.That(stripped, Is.EqualTo(sample.VisibleText));
            Assert.That(restored, Is.EqualTo(sample.ExpectedRestored));
        });

        return true.ToProperty();
    }

    [FsCheck.NUnit.Property(Arbitrary = new[] { typeof(ColorCodePreserverArbitraries) }, MaxTest = 200, Replay = ReplaySeed)]
    public FsCheckProperty StripThenRestore_PreservesBackgroundCodes(BackgroundColorCodeCase sample)
    {
        var (stripped, spans) = ColorCodePreserver.Strip(sample.Source);
        var restored = ColorCodePreserver.Restore(sample.TranslatedVisibleText, spans);

        Assert.Multiple(() =>
        {
            Assert.That(stripped, Is.EqualTo(sample.VisibleText));
            Assert.That(restored, Is.EqualTo(sample.ExpectedRestored));
        });

        return true.ToProperty();
    }

    [FsCheck.NUnit.Property(Arbitrary = new[] { typeof(ColorCodePreserverArbitraries) }, MaxTest = 200, Replay = ReplaySeed)]
    public FsCheckProperty StripThenRestore_PreservesTmpColorTags(TmpColorTagCase sample)
    {
        var (stripped, spans) = ColorCodePreserver.Strip(sample.Source);
        var restored = ColorCodePreserver.Restore(sample.TranslatedVisibleText, spans);

        Assert.Multiple(() =>
        {
            Assert.That(stripped, Is.EqualTo(sample.VisibleText));
            Assert.That(restored, Is.EqualTo(sample.ExpectedRestored));
        });

        return true.ToProperty();
    }

    [FsCheck.NUnit.Property(Arbitrary = new[] { typeof(ColorCodePreserverArbitraries) }, MaxTest = 200, Replay = ReplaySeed)]
    public FsCheckProperty StripThenRestore_DoesNotChangeVisibleText_ForTmpColorTags(TmpColorTagCase sample)
    {
        var (stripped, spans) = ColorCodePreserver.Strip(sample.Source);
        var restored = ColorCodePreserver.Restore(stripped, spans);

        Assert.That(restored, Is.EqualTo(sample.Source));

        return true.ToProperty();
    }
}
