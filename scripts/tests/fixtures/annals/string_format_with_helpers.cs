using System;

namespace XRL.Annals;

[Serializable]
public class StringFormatWithHelpersFixture : HistoricEvent
{
    public override void Generate()
    {
        string value = string.Format(
            "{0} cemented {1} friendship with {2} by marrying {3}.",
            "Resheph",
            Grammar.PossessivePronoun("he"),
            Faction.GetFormattedName("highly entropic beings"),
            NameMaker.MakeName(null, null, null, null, "Qudish"));
        SetEventProperty("gospel", Grammar.InitCap(value));
    }
}
