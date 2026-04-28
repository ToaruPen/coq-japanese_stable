using System;

namespace XRL.Annals;

[Serializable]
public class ElseifChainFourBranchesFixture : HistoricEvent
{
    public override void Generate()
    {
        string value;
        if (Random(0, 3) == 0)
        {
            value = "alpha";
        }
        else if (Random(0, 2) == 0)
        {
            value = "beta";
        }
        else if (Random(0, 1) == 0)
        {
            value = "gamma";
        }
        else
        {
            value = "delta";
        }
        SetEventProperty("gospel", value);
    }
}
