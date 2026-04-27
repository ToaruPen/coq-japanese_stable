using System;

namespace XRL.Annals;

[Serializable]
public class EscapedBracesInFormatFixture : HistoricEvent
{
    public override void Generate()
    {
        string name = "Hero";
        SetEventProperty("gospel", string.Format("{{0}} struck {0}.", name));
    }
}
