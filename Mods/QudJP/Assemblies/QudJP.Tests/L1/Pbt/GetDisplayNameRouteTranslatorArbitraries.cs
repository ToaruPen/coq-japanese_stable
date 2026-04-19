using FsCheck;
using FsCheck.Fluent;

namespace QudJP.Tests.L1.Pbt;

public sealed record DisplayNameExactCase(string Source, string Expected);

public sealed record DisplayNameTrimmedExactCase(string Source, string Expected);

public sealed record DisplayNameScopedConflictCase(string Source, string Expected);

public sealed record DisplayNameBracketedStateCase(string Source, string Expected);

public sealed record DisplayNameQuantityCase(string Source, string Expected);

public sealed record DisplayNameParenthesizedStateCase(string Source, string Expected);

public sealed record DisplayNameLeadingMarkupCase(string Source, string Expected);

public sealed record DisplayNameMkTierCase(string Source, string Expected);

public sealed record DisplayNameAngleCodeCase(string Source, string Expected);

public static class GetDisplayNameRouteTranslatorArbitraries
{
    public static Arbitrary<DisplayNameExactCase> ExactCases()
    {
        return Gen.Elements(
                new DisplayNameExactCase("worn bronze sword", "使い込まれた青銅の剣"),
                new DisplayNameExactCase("Water Containers", "水容器"))
            .ToArbitrary();
    }

    public static Arbitrary<DisplayNameTrimmedExactCase> TrimmedExactCases()
    {
        return Gen.Elements(
                new DisplayNameTrimmedExactCase("  worn bronze sword  ", "  使い込まれた青銅の剣  "),
                new DisplayNameTrimmedExactCase("Water Containers  ", "水容器  "),
                new DisplayNameTrimmedExactCase("  Water Containers", "  水容器"))
            .ToArbitrary();
    }

    public static Arbitrary<DisplayNameScopedConflictCase> ScopedConflictCases()
    {
        return Gen.Elements(
                new DisplayNameScopedConflictCase("water", "{{B|水の}}"),
                new DisplayNameScopedConflictCase("bloody Naruur", "{{r|血まみれの}}Naruur"))
            .ToArbitrary();
    }

    public static Arbitrary<DisplayNameBracketedStateCase> BracketedStateCases()
    {
        return Gen.Elements(
                new DisplayNameBracketedStateCase("water flask [empty]", "水袋 [空]"),
                new DisplayNameBracketedStateCase("water flask [empty, sealed]", "水袋 [空／密封]"),
                new DisplayNameBracketedStateCase("water flask [auto-collecting]", "水袋 [自動採取中]"))
            .ToArbitrary();
    }

    public static Arbitrary<DisplayNameQuantityCase> QuantityCases()
    {
        return Gen.Elements(
                new DisplayNameQuantityCase("water flask x2", "水袋 x2"),
                new DisplayNameQuantityCase("water flask x12", "水袋 x12"),
                new DisplayNameQuantityCase("worn bronze sword x3", "使い込まれた青銅の剣 x3"))
            .ToArbitrary();
    }

    public static Arbitrary<DisplayNameParenthesizedStateCase> ParenthesizedStateCases()
    {
        return Gen.Elements(
                new DisplayNameParenthesizedStateCase("lead slug (Frozen)", "鉛の弾 (凍結)"),
                new DisplayNameParenthesizedStateCase("lead slug (FROZEN)", "鉛の弾 (凍結)"),
                new DisplayNameParenthesizedStateCase("lead slug (frozen)", "鉛の弾 (凍結)"))
            .ToArbitrary();
    }

    public static Arbitrary<DisplayNameLeadingMarkupCase> LeadingMarkupCases()
    {
        return Gen.Elements(
                new DisplayNameLeadingMarkupCase("{{r|bloody}} dromad merchant", "{{r|血まみれの}} ドロマド商人"),
                new DisplayNameLeadingMarkupCase("[{{r|bloody}}] dromad merchant", "[{{r|血まみれの}}] ドロマド商人"))
            .ToArbitrary();
    }

    public static Arbitrary<DisplayNameMkTierCase> MkTierCases()
    {
        return Gen.Elements(
                new DisplayNameMkTierCase("rusted grenade mk I <AA1>", "錆びたグレネード mk I <AA1>"),
                new DisplayNameMkTierCase("rusted grenade mk X", "錆びたグレネード mk X"))
            .ToArbitrary();
    }

    public static Arbitrary<DisplayNameAngleCodeCase> AngleCodeCases()
    {
        return Gen.Elements(
                new DisplayNameAngleCodeCase("worn bronze sword <BD1>", "使い込まれた青銅の剣 <BD1>"),
                new DisplayNameAngleCodeCase("worn bronze sword <Z9>", "使い込まれた青銅の剣 <Z9>"))
            .ToArbitrary();
    }
}
