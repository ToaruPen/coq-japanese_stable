using System;

namespace XRL.Annals;

[Serializable]
public class InitcapCapitalizesFirstLiteralFixture : HistoricEvent
{
    public override void Generate()
    {
        SetEventProperty("gospel", Grammar.InitCap(ExpandString("deep in the wilds, " + "<entity.name> wandered.")));
    }
}
