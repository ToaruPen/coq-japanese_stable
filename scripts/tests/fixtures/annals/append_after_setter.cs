using System;

namespace XRL.Annals;

[Serializable]
public class AppendAfterSetterFixture : HistoricEvent
{
    public override void Generate()
    {
        string text = "base";
        SetEventProperty("gospel", text);
        text += " after";
    }
}
