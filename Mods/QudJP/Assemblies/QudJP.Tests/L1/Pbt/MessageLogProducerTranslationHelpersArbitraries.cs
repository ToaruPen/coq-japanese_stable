using FsCheck;
using FsCheck.Fluent;

namespace QudJP.Tests.L1.Pbt;

public sealed record CombatPatternCase(string Source, string ExpectedTranslated);

public sealed record DirectMarkedCase(string Source);

public static class MessageLogProducerTranslationHelpersArbitraries
{
    private static Gen<string> SafeWeaponText()
    {
        var characters = Gen.Elements('刀', '剣', '槍', '光', '炎', '鋼', '青', '銅');
        return Gen.Choose(1, 6)
            .SelectMany(length => Gen.ArrayOf(characters, length))
            .Select(chars => new string(chars));
    }

    public static Arbitrary<CombatPatternCase> CombatPatternCases()
    {
        return (from weaponText in SafeWeaponText()
                from multiplier in Gen.Choose(1, 4)
                from damage in Gen.Choose(1, 12)
                from roll in Gen.Choose(1, 30)
                let multiplierText = $"x{multiplier}"
                let weapon = $"{{{{w|{weaponText}}}}}"
                let source = $"\u0002hit\u001F{damage}\u001F{roll}\u001F\u0003{{{{g|You hit {{{{&w|({multiplierText})}}}} for {damage} damage with your {weapon}! [{roll}]}}}}"
                let expected = $"\u0001{{{{g|{weapon}で{damage}ダメージを与えた。({{{{&w|{multiplierText}}}}}) [{roll}]}}}}"
                select new CombatPatternCase(source, expected))
            .ToArbitrary();
    }

    public static Arbitrary<DirectMarkedCase> DirectMarkedCases()
    {
        return (from visible in SafeWeaponText()
                from wrapper in Gen.OneOf(Gen.Constant("{{g|"), Gen.Constant("{{r|"))
                let source = "\u0001" + wrapper + visible + "}}"
                select new DirectMarkedCase(source))
            .ToArbitrary();
    }
}
