using System;

namespace XRL.Annals;

[Serializable]
public class LatestAssignmentWinsFixture : HistoricEvent
{
    public override void Generate()
    {
        string value;
        value = "first";
        value = "second";
        SetEventProperty("gospel", value);
    }
}
