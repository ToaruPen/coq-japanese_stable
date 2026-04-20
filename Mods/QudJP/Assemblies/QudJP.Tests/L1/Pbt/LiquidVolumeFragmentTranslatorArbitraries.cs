using FsCheck;
using FsCheck.Fluent;

namespace QudJP.Tests.L1.Pbt;

public sealed record LiquidFragment(string Source, string Expected);

public sealed record LiquidStatusCase(string Source, string ExpectedTranslated);

public sealed record LiquidOwnershipCase(string Source, string ExpectedTranslated);

public sealed record LiquidTargetRuleCase(string Source, string ExpectedTranslated);

public sealed record LiquidPourIntoCase(string Source, string ExpectedTranslated);

public sealed record LiquidPassthroughCase(string Source);

public static class LiquidVolumeFragmentTranslatorArbitraries
{
    private static Gen<string> TargetBaseText()
    {
        return Gen.Elements("canteen", "vial", "tank", "熊油瓶");
    }

    private static Gen<string> StatusBaseText()
    {
        return Gen.Elements("hydrated", "slimy", "glowing", "発光中");
    }

    private static Gen<LiquidFragment> PlainOrWrappedTargets()
    {
        var wrappers = Gen.Elements("{{Y|", "{{C|", "{{B|", "{{r|");

        var plain = TargetBaseText().Select(value => new LiquidFragment(value, value));
        var wrapped =
            from value in TargetBaseText()
            from open in wrappers
            select new LiquidFragment(open + value + "}}", open + value + "}}");

        return Gen.OneOf(plain, wrapped);
    }

    private static Gen<LiquidFragment> NormalizedContainerTargets()
    {
        var plain = PlainOrWrappedTargets();
        var articleTargets = Gen.Elements(
            new LiquidFragment("the canteen", "canteen"),
            new LiquidFragment("The vial", "vial"));

        return Gen.OneOf(plain, articleTargets);
    }

    private static Gen<LiquidFragment> PourTargets()
    {
        return Gen.Elements(
            new LiquidFragment("yourself", "自分"),
            new LiquidFragment("itself", "それ自身"),
            new LiquidFragment("{{Y|yourself}}", "{{Y|自分}}"),
            new LiquidFragment("{{Y|itself}}", "{{Y|それ自身}}"));
    }

    private static Gen<LiquidFragment> StatusFragments()
    {
        var wrappers = Gen.Elements("{{B|", "{{C|", "{{Y|");

        var plain = StatusBaseText().Select(value => new LiquidFragment(value, value));
        var wrapped =
            from value in StatusBaseText()
            from open in wrappers
            select new LiquidFragment(open + value + "}}", open + value + "}}");

        return Gen.OneOf(plain, wrapped);
    }

    public static Arbitrary<LiquidStatusCase> StatusCases()
    {
        return StatusFragments()
            .Select(status => new LiquidStatusCase(
                $"You are now {status.Source}.",
                $"あなたは今、{status.Expected}。"))
            .ToArbitrary();
    }

    public static Arbitrary<LiquidOwnershipCase> OwnershipCases()
    {
        var templates = Gen.Elements(
            (Source: "drink from it", Suffix: "そこから飲みますか？"),
            (Source: "drain it", Suffix: "排出しますか？"),
            (Source: "fill it", Suffix: "満たしますか？"));

        return (from target in PlainOrWrappedTargets()
                from template in templates
                select new LiquidOwnershipCase(
                    $"{target.Source} is not owned by you. Are you sure you want to {template.Source}?",
                    $"{target.Expected}はあなたの所有物ではない。本当に{template.Suffix}"))
            .ToArbitrary();
    }

    public static Arbitrary<LiquidTargetRuleCase> TargetRuleCases()
    {
        var templates = Gen.Elements(
            (SourcePrefix: "You cannot seem to interact with ", SourceSuffix: " in any way.", ExpectedSuffix: "にはどうやっても干渉できないようだ。"),
            (SourcePrefix: string.Empty, SourceSuffix: " has no drain.", ExpectedSuffix: "には排出口がない。"),
            (SourcePrefix: string.Empty, SourceSuffix: " is sealed.", ExpectedSuffix: "は密閉されている。"),
            (SourcePrefix: "Do you want to empty ", SourceSuffix: " first?", ExpectedSuffix: "を先に空にしますか？"));

        return (from target in NormalizedContainerTargets()
                from template in templates
                select new LiquidTargetRuleCase(
                    template.SourcePrefix + target.Source + template.SourceSuffix,
                    target.Expected + template.ExpectedSuffix))
            .ToArbitrary();
    }

    public static Arbitrary<LiquidPourIntoCase> PourIntoCases()
    {
        return PourTargets()
            .Select(target => new LiquidPourIntoCase(
                $"You can't pour from a container into {target.Source}.",
                $"{target.Expected}に容器から注ぐことはできない。"))
            .ToArbitrary();
    }

    public static Arbitrary<LiquidPassthroughCase> PassthroughCases()
    {
        return Gen.Elements(
                new LiquidPassthroughCase(string.Empty),
                new LiquidPassthroughCase("This is a random liquid message."),
                new LiquidPassthroughCase("Are you sure you want to drink from canteen?"),
                new LiquidPassthroughCase("\u0001You cannot seem to interact with canteen in any way."))
            .ToArbitrary();
    }
}
