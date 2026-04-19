using FsCheck;
using FsCheck.Fluent;

namespace QudJP.Tests.L1.Pbt;

public sealed record DoesVerbMarkedTailCase(string Fragment, string Verb, string Tail, string Expected);

public sealed record DoesVerbEarliestVerbCase(string Source, string Expected);

public sealed record DoesVerbCanonicalMarkerCase(string Fragment, string Verb, string Tail, string Expected);

public static class DoesVerbRouteTranslatorArbitraries
{
    public static Arbitrary<DoesVerbMarkedTailCase> MarkedTailCases()
    {
        return Gen.Elements(
                new DoesVerbMarkedTailCase("The 熊 are", "are", " stuck.", "熊は動けなくなった。"),
                new DoesVerbMarkedTailCase("The 熊 are", "are", " exhausted!", "熊は疲弊した！"))
            .ToArbitrary();
    }

    public static Arbitrary<DoesVerbEarliestVerbCase> EarliestVerbCases()
    {
        return Gen.Elements(
                new DoesVerbEarliestVerbCase(
                    "The 熊 asks about its location and is no longer lost.",
                    "熊は自分の居場所について尋ね、もう迷っていない"),
                new DoesVerbEarliestVerbCase(
                    "The 熊 asks about its location and is no longer stunned.",
                    "熊は自分の居場所について尋ね、もう気絶していない"))
            .ToArbitrary();
    }

    public static Arbitrary<DoesVerbCanonicalMarkerCase> CanonicalMarkerCases()
    {
        return Gen.Elements(
                new DoesVerbCanonicalMarkerCase("The 熊 is", "are", " stunned!", "熊は気絶した！"),
                new DoesVerbCanonicalMarkerCase("The 水筒 has", "have", " no room for more water.", "水筒はこれ以上の水を入れる余地がない。"),
                new DoesVerbCanonicalMarkerCase("The 熊 falls", "fall", " to the ground.", "熊は地面に倒れた。"))
            .ToArbitrary();
    }
}
