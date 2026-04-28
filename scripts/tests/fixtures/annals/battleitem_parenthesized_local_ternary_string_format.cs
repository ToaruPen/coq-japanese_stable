public class battleitem_parenthesized_local_ternary_string_format
{
    public void Generate()
    {
        string value2 = ((num2 != 0)
            ? string.Format("{0} the Battle of {1}, where -- {2} -- {3} wielded {5} {6} and struck down the {4} in the name of {7}. {0}, afterward, how {8} and {9} {10} in {11} for days and days.",
                ExpandString("<spice.commonPhrases.remember.!random.capitalize>"),
                newLocationInRegion,
                ExpandString("<spice.elements." + randomElement + ".mythicalBattleVista.!random>").Replace("*var*", text2),
                "<entity.name>",
                ExpandString("<spice.history.gospels.EnemyHostName." + QudHistoryHelpers.GetSultanateEra(snapshotAtYear) + ".!random>"),
                "<entity.possessivePronoun>",
                text,
                ExpandString("<spice.instancesOf.justice.!random>"),
                Faction.GetFormattedName(text4),
                ExpandString("<spice.instancesOf.dearOnes.!random>"),
                ExpandString("<spice.instancesOf.criedOut.!random>"),
                ExpandString("<spice.commonPhrases.woe.!random>"))
            : string.Format("{0} the Battle of {1}, where -- {2} -- {3} wielded {5} {6} and struck down the {4} in the name of {7}. {0}, afterward, how {8} and {9} {10} in {11} for days and days.",
                ExpandString("<spice.commonPhrases.remember.!random.capitalize>"),
                newLocationInRegion,
                ExpandString("<spice.elements." + randomElement + ".mythicalBattleVista.!random>").Replace("*var*", text2),
                "<entity.name>",
                ExpandString("<spice.history.gospels.EnemyHostName." + QudHistoryHelpers.GetSultanateEra(snapshotAtYear) + ".!random>"),
                "<entity.possessivePronoun>",
                text,
                ExpandString("<spice.instancesOf.justice.!random>"),
                Faction.GetFormattedName(text4),
                ExpandString("<spice.instancesOf.dearOnes.!random>"),
                ExpandString("<spice.instancesOf.criedOut.!random>"),
                ExpandString("<spice.commonPhrases.celebration.!random>")));
        SetEventProperty("tombInscription", value2);
    }
}
