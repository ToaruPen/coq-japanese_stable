using System;

namespace XRL.Annals;

[Serializable]
public class IfOptionalAppendFixture : HistoricEvent
{
    public override void Generate()
    {
        string text = "Resheph married Rebekah.";
        if (Random(0, 1) == 0)
        {
            text += " They received a gift.";
        }
        SetEventProperty("gospel", text);
    }
}
