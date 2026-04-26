using System;

namespace XRL.Annals;

[Serializable]
public class ConcatInitFixture : HistoricEvent
{
    public override void Generate()
    {
        string value = "In %" + year + "%, an event happened, and Resheph rejoiced.";
        SetEventProperty("gospel", value);
    }
}
