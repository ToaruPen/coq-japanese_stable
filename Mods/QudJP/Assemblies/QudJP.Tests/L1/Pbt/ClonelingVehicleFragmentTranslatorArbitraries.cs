using FsCheck;
using FsCheck.Fluent;

namespace QudJP.Tests.L1.Pbt;

public sealed record ClonelingVisibleFragment(string Source, string Expected);

public sealed record ClonelingPopupCase(string Source, string ExpectedTranslated);

public sealed record ClonelingQueuedCase(string Source, string ExpectedTranslated);

public sealed record ClonelingPopupPassthroughCase(string Source);

public sealed record ClonelingQueuedPassthroughCase(string Source);

public static class ClonelingVehicleFragmentTranslatorArbitraries
{
    private static Gen<string> BaseText()
    {
        return Gen.Elements("sunslag", "cloning draught", "warm static", "熊油", "発光液");
    }

    private static Gen<ClonelingVisibleFragment> VisibleFragments()
    {
        var wrappers = Gen.Elements("{{C|", "{{G|", "{{Y|", "{{r|");

        var plain = BaseText().Select(value => new ClonelingVisibleFragment(value, value));
        var wrapped =
            from value in BaseText()
            from open in wrappers
            select new ClonelingVisibleFragment(open + value + "}}", open + value + "}}");

        return Gen.OneOf(plain, wrapped);
    }

    public static Arbitrary<ClonelingPopupCase> PopupCases()
    {
        return VisibleFragments()
            .Select(liquid => new ClonelingPopupCase(
                $"You do not have 1 dram of {liquid.Source}.",
                $"{liquid.Expected}を1ドラム持っていない。"))
            .ToArbitrary();
    }

    public static Arbitrary<ClonelingQueuedCase> QueuedCases()
    {
        return VisibleFragments()
            .Select(liquid => new ClonelingQueuedCase(
                $"Your onboard systems are out of {liquid.Source}.",
                $"搭載システムの{liquid.Expected}が切れている。"))
            .ToArbitrary();
    }

    public static Arbitrary<ClonelingPopupPassthroughCase> PopupPassthroughCases()
    {
        return PopupCases().Generator
            .Select(sample => new ClonelingPopupPassthroughCase("\u0001" + sample.Source))
            .ToArbitrary();
    }

    public static Arbitrary<ClonelingQueuedPassthroughCase> QueuedPassthroughCases()
    {
        return QueuedCases().Generator
            .Select(sample => new ClonelingQueuedPassthroughCase("\u0001" + sample.Source))
            .ToArbitrary();
    }
}
