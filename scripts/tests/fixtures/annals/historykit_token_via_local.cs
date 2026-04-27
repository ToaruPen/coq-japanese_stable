using System;

namespace XRL.Annals;

[Serializable]
public class HistorykitTokenViaLocalFixture : HistoricEvent
{
    public override void Generate()
    {
        string tok = "<entity.name>";
        SetEventProperty("gospel", ExpandString(tok));
    }
}
