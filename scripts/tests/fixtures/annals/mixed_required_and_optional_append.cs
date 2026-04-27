using System;

namespace XRL.Annals;

[Serializable]
public class MixedRequiredAndOptionalAppendFixture : HistoricEvent
{
    public override void Generate()
    {
        string text = "base";
        if (Random(0, 1) == 0)
        {
            text += " maybe";
        }
        text += " always";
        SetEventProperty("gospel", text);
    }
}
