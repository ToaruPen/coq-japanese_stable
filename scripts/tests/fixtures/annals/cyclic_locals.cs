using System;

namespace XRL.Annals;

[Serializable]
public class CyclicLocalsFixture : HistoricEvent
{
    public override void Generate()
    {
        string a = "prefix " + b + " mid";
        string b = "X" + a + "Y";
        SetEventProperty("gospel", a);
    }
}
