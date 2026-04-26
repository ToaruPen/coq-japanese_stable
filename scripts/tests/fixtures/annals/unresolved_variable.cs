using System;

namespace XRL.Annals;

[Serializable]
public class UnresolvedFixture : HistoricEvent
{
    public override void Generate()
    {
        string mystery = SomeHelper.Compute();
        SetEventProperty("gospel", "prefix " + mystery + " suffix.");
    }
}
