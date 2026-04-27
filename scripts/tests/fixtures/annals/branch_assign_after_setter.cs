using System;

namespace XRL.Annals;

[Serializable]
public class BranchAssignAfterSetterFixture : HistoricEvent
{
    public override void Generate()
    {
        string value = "base";
        if (Random(0, 1) == 0)
        {
            SetEventProperty("gospel", value);
            value = "after";
        }
    }
}
