using System;

namespace XRL.Annals;

[Serializable]
public class ElseifChainThreeBranchesFixture : HistoricEvent
{
    public override void Generate()
    {
        string value;
        if (Random(0, 2) == 0)
        {
            value = "alpha";
        }
        else if (Random(0, 1) == 0)
        {
            value = "beta";
        }
        else
        {
            value = "gamma";
        }
        SetEventProperty("gospel", value);
    }
}
