using System;

namespace XRL.Annals;

[Serializable]
public class AppendInvalidatedByReassignmentFixture : HistoricEvent
{
    public override void Generate()
    {
        string text = "base";
        text += " middle";
        text = "override";
        SetEventProperty("gospel", text);
    }
}
