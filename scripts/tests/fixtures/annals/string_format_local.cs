using System;

namespace XRL.Annals;

[Serializable]
public class StringFormatLocalFixture : HistoricEvent
{
    public override void Generate()
    {
        string region = "the salt marsh";
        string location = "Joppa";
        string value = string.Format("In {0}, a healthy child was born at {1}.", region, location);
        SetEventProperty("gospel", value);
    }
}
