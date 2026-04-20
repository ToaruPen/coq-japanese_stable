using FsCheck;
using FsCheck.Fluent;

namespace QudJP.Tests.L1.Pbt;

public sealed record VisibleFragment(string Source, string Expected);

public sealed record EnclosingExtricateCase(string Source, string ExpectedTranslated);

public sealed record EnclosingPassthroughCase(string Source);

public static class EnclosingFragmentTranslatorArbitraries
{
    private static Gen<string> BaseText()
    {
        return Gen.Elements("snapjaw", "golem", "stasis pod", "cryo chamber", "熊", "光る棺");
    }

    private static Gen<VisibleFragment> VisibleFragments()
    {
        var wrappers = Gen.Elements("{{r|", "{{C|", "{{G|", "{{Y|");

        var plain = BaseText().Select(value => new VisibleFragment(value, value));
        var wrapped =
            from value in BaseText()
            from open in wrappers
            select new VisibleFragment(open + value + "}}", open + value + "}}");

        return Gen.OneOf(plain, wrapped);
    }

    public static Arbitrary<EnclosingExtricateCase> ExtricateCases()
    {
        var yourselfCases =
            from container in VisibleFragments()
            select new EnclosingExtricateCase(
                $"You extricate yourself from {container.Source}.",
                $"{container.Expected}から抜け出した。");

        var itselfCases =
            from container in VisibleFragments()
            select new EnclosingExtricateCase(
                $"You extricate itself from {container.Source}.",
                $"{container.Expected}からそれ自身を引き出した。");

        var subjectCases =
            from subject in VisibleFragments()
            from container in VisibleFragments()
            select new EnclosingExtricateCase(
                $"You extricate {subject.Source} from {container.Source}.",
                $"{container.Expected}から{subject.Expected}を引き出した。");

        return Gen.OneOf(yourselfCases, itselfCases, subjectCases)
            .ToArbitrary();
    }

    public static Arbitrary<EnclosingPassthroughCase> PassthroughCases()
    {
        return ExtricateCases().Generator
            .Select(sample => new EnclosingPassthroughCase("\u0001" + sample.Source))
            .ToArbitrary();
    }
}
