using System.Globalization;
using System.Text;

using FsCheck.Fluent;

using FsCheckProperty = FsCheck.Property;

using QudJP.Patches;

namespace QudJP.Tests.L1.Pbt;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class GetDisplayNameRouteTranslatorPropertyTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private const string ReplaySeed = "159357246,97531";

    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-displayname-route-pbt", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        Translator.ResetForTests();
        ScopedDictionaryLookup.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);

        WriteCommonDictionaries();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        ScopedDictionaryLookup.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [FsCheck.NUnit.Property(Arbitrary = new[] { typeof(GetDisplayNameRouteTranslatorArbitraries) }, MaxTest = 100, Replay = ReplaySeed)]
    public FsCheckProperty TranslatePreservingColors_UsesExactWholeNameLookup(DisplayNameExactCase sample)
    {
        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            sample.Source,
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo(sample.Expected));

        return true.ToProperty();
    }

    [FsCheck.NUnit.Property(Arbitrary = new[] { typeof(GetDisplayNameRouteTranslatorArbitraries) }, MaxTest = 100, Replay = ReplaySeed)]
    public FsCheckProperty TranslatePreservingColors_UsesTrimmedExactWholeNameLookup(DisplayNameTrimmedExactCase sample)
    {
        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            sample.Source,
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo(sample.Expected));

        return true.ToProperty();
    }

    [FsCheck.NUnit.Property(Arbitrary = new[] { typeof(GetDisplayNameRouteTranslatorArbitraries) }, MaxTest = 100, Replay = ReplaySeed)]
    public FsCheckProperty TranslatePreservingColors_PrefersDisplayNameScopedConflicts(DisplayNameScopedConflictCase sample)
    {
        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            sample.Source,
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo(sample.Expected));

        return true.ToProperty();
    }

    [FsCheck.NUnit.Property(Arbitrary = new[] { typeof(GetDisplayNameRouteTranslatorArbitraries) }, MaxTest = 100, Replay = ReplaySeed)]
    public FsCheckProperty TranslatePreservingColors_UsesDisplayNameScopedBracketedStates(DisplayNameBracketedStateCase sample)
    {
        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            sample.Source,
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo(sample.Expected));

        return true.ToProperty();
    }

    [FsCheck.NUnit.Property(Arbitrary = new[] { typeof(GetDisplayNameRouteTranslatorArbitraries) }, MaxTest = 100, Replay = ReplaySeed)]
    public FsCheckProperty TranslatePreservingColors_UsesQuantitySuffixLookup(DisplayNameQuantityCase sample)
    {
        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            sample.Source,
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo(sample.Expected));

        return true.ToProperty();
    }

    [FsCheck.NUnit.Property(Arbitrary = new[] { typeof(GetDisplayNameRouteTranslatorArbitraries) }, MaxTest = 100, Replay = ReplaySeed)]
    public FsCheckProperty TranslatePreservingColors_UsesParenthesizedStateFallback(DisplayNameParenthesizedStateCase sample)
    {
        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            sample.Source,
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo(sample.Expected));

        return true.ToProperty();
    }

    [FsCheck.NUnit.Property(Arbitrary = new[] { typeof(GetDisplayNameRouteTranslatorArbitraries) }, MaxTest = 100, Replay = ReplaySeed)]
    public FsCheckProperty TranslatePreservingColors_PreservesLeadingMarkupModifier(DisplayNameLeadingMarkupCase sample)
    {
        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            sample.Source,
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo(sample.Expected));

        return true.ToProperty();
    }

    [FsCheck.NUnit.Property(Arbitrary = new[] { typeof(GetDisplayNameRouteTranslatorArbitraries) }, MaxTest = 100, Replay = ReplaySeed)]
    public FsCheckProperty TranslatePreservingColors_UsesMkTierSuffixLookup(DisplayNameMkTierCase sample)
    {
        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            sample.Source,
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo(sample.Expected));

        return true.ToProperty();
    }

    [FsCheck.NUnit.Property(Arbitrary = new[] { typeof(GetDisplayNameRouteTranslatorArbitraries) }, MaxTest = 100, Replay = ReplaySeed)]
    public FsCheckProperty TranslatePreservingColors_UsesAngleCodeSuffixLookup(DisplayNameAngleCodeCase sample)
    {
        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            sample.Source,
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo(sample.Expected));

        return true.ToProperty();
    }

    [FsCheck.NUnit.Property(Arbitrary = new[] { typeof(GetDisplayNameRouteTranslatorArbitraries) }, MaxTest = 100, Replay = ReplaySeed)]
    public FsCheckProperty TranslatePreservingColors_LeavesEmptyStringUntouched(DisplayNameEmptyStringCase sample)
    {
        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            sample.Source,
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo(sample.Expected));

        return true.ToProperty();
    }

    [FsCheck.NUnit.Property(Arbitrary = new[] { typeof(GetDisplayNameRouteTranslatorArbitraries) }, MaxTest = 100, Replay = ReplaySeed)]
    public FsCheckProperty TranslatePreservingColors_UsesExactLookupForControlCharacterInput(DisplayNameControlCharacterCase sample)
    {
        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            sample.Source,
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo(sample.Expected));

        return true.ToProperty();
    }

    private void WriteCommonDictionaries()
    {
        WriteDictionaryFile(
            "ui-displayname-route.ja.json",
            ("worn bronze sword", "使い込まれた青銅の剣"),
            ("Water Containers", "水容器"),
            ("water flask", "水袋"),
            ("lead slug", "鉛の弾"),
            ("frozen", "凍結"),
            ("dromad merchant", "ドロマド商人"),
            ("rusted grenade", "錆びたグレネード"),
            ("item \u0001 name", "制御マーカー付き品名"));

        WriteDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("water", "{{B|水の}}"),
            ("bloody", "{{r|血まみれの}}"),
            ("[empty]", "[空]"),
            ("[empty, sealed]", "[空／密封]"),
            ("[auto-collecting]", "[自動採取中]"));

        WriteDictionaryFile(
            "ui-liquids.ja.json",
            ("water", "水"));

        WriteDictionaryFile(
            "ui-liquid-adjectives.ja.json",
            ("bloody", "血混じりの"));
    }

    private void WriteDictionaryFile(string fileName, params (string key, string text)[] entries)
    {
        var builder = new StringBuilder();
        builder.Append('{');
        builder.Append("\"entries\":[");

        for (var index = 0; index < entries.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append("{\"key\":\"");
            builder.Append(EscapeJson(entries[index].key));
            builder.Append("\",\"text\":\"");
            builder.Append(EscapeJson(entries[index].text));
            builder.Append("\"}");
        }

        builder.Append("]}");
        builder.AppendLine();

        File.WriteAllText(
            Path.Combine(tempDirectory, fileName),
            builder.ToString(),
            Utf8WithoutBom);
    }

    private static string EscapeJson(string value)
    {
        var builder = new StringBuilder(value.Length + 8);
        foreach (var ch in value)
        {
            switch (ch)
            {
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\b':
                    builder.Append("\\b");
                    break;
                case '\f':
                    builder.Append("\\f");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    if (ch < 0x20)
                    {
                        builder.Append("\\u");
                        builder.Append(((int)ch).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(ch);
                    }

                    break;
            }
        }

        return builder.ToString();
    }
}
