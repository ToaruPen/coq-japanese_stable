using System;

namespace XRL.Annals;

[Serializable]
public class ElseifChainTwoBranchesFixture : HistoricEvent
{
    public override void Generate()
    {
        if (Random(0, 1) == 0)
        {
            SetEventProperty("gospel", "alpha");
        }
        else if (Random(0, 1) == 0)
        {
            SetEventProperty("gospel", "beta");
        }
    }
}
