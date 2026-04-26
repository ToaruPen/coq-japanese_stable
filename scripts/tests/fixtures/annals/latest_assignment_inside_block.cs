using System;

namespace XRL.Annals;

[Serializable]
public class LatestAssignmentInsideBlockFixture : HistoricEvent
{
    public override void Generate()
    {
        string value;
        if (Random(0, 1) == 0)
        {
            value = "first";
            value = "second";
        }
        else
        {
            value = "alt";
        }
        SetEventProperty("gospel", value);
    }
}
