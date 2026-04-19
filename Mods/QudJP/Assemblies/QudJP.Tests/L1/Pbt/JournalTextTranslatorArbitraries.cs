using FsCheck;
using FsCheck.Fluent;

namespace QudJP.Tests.L1.Pbt;

public sealed record JournalMarkerText(string Value);

public sealed record JournalJourneyCase(string PlaceSource, string PlaceTranslated)
{
    public string Source => $"You journeyed to {PlaceSource}.";

    public string ExpectedTranslated => MessageFrameTranslator.MarkDirectTranslation(PlaceTranslated + "に旅した。");
}

public sealed record JournalSkippedCategoryCase(string Category, string Source);

public static class JournalTextTranslatorArbitraries
{
    public static Arbitrary<JournalMarkerText> MarkerTexts()
    {
        var characters = Gen.Elements('a', 'b', 'c', 'x', 'y', 'z', '旅', '録', '地', '誌', '光');

        return Gen.Choose(1, 12)
            .SelectMany(length => Gen.ArrayOf(characters, length))
            .Select(chars => new JournalMarkerText(new string(chars)))
            .ToArbitrary();
    }

    public static Arbitrary<JournalJourneyCase> JourneyCases()
    {
        return Gen.Elements(
                new JournalJourneyCase("Kyakukya", "キャクキャ"),
                new JournalJourneyCase("Joppa", "ジョッパ"))
            .ToArbitrary();
    }

    public static Arbitrary<JournalSkippedCategoryCase> SkippedCategoryCases()
    {
        var categories = Gen.Elements("Miscellaneous", "miscellaneous", "Named Locations", "named locations");
        var texts = Gen.Elements("A \"SATED\" baetyl", "A strange ruin", "Untranslated note");

        return (from category in categories
                from text in texts
                select new JournalSkippedCategoryCase(category, text))
            .ToArbitrary();
    }
}
