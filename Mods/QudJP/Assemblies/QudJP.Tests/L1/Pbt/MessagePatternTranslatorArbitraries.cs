using FsCheck;
using FsCheck.Fluent;

namespace QudJP.Tests.L1.Pbt;

public sealed record HitWithRollPatternCase(string Source, string ExpectedTranslated);

public sealed record WeaponMissPatternCase(string Source, string ExpectedTranslated);

public static class MessagePatternTranslatorArbitraries
{
    private static Gen<string> SafeWeaponText()
    {
        var characters = Gen.Elements('刀', '剣', '槍', '光', '炎', '鋼', '青', '銅');
        return Gen.Choose(1, 6)
            .SelectMany(length => Gen.ArrayOf(characters, length))
            .Select(chars => new string(chars));
    }

    public static Arbitrary<HitWithRollPatternCase> HitWithRollPatternCases()
    {
        return (from weaponText in SafeWeaponText()
                from multiplier in Gen.Choose(1, 4)
                from damage in Gen.Choose(1, 12)
                from roll in Gen.Choose(1, 30)
                let multiplierText = $"x{multiplier}"
                let weapon = $"{{{{w|{weaponText}}}}}"
                let source = $"{{{{g|You hit {{{{&w|({multiplierText})}}}} for {damage} damage with your {weapon}! [{roll}]}}}}"
                let expected = $"{{{{g|{weapon}で{damage}ダメージを与えた。({{{{&w|{multiplierText}}}}}) [{roll}]}}}}"
                select new HitWithRollPatternCase(source, expected))
            .ToArbitrary();
    }

    public static Arbitrary<WeaponMissPatternCase> WeaponMissPatternCases()
    {
        return (from weaponText in SafeWeaponText()
                from attacker in Gen.Choose(0, 20)
                from defender in Gen.Choose(0, 20)
                let weapon = $"{{{{w|{weaponText}}}}}"
                let source = $"{{{{r|You miss with your {weapon}! [{attacker} vs {defender}]}}}}"
                let expected = $"{{{{r|{weapon}での攻撃は外れた。[{attacker} vs {defender}]}}}}"
                select new WeaponMissPatternCase(source, expected))
            .ToArbitrary();
    }
}
