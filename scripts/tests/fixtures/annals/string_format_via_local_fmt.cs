using System;

namespace XRL.Annals;

[Serializable]
public class StringFormatViaLocalFmtFixture : HistoricEvent
{
    public override void Generate()
    {
        string fmt = "{0} struck {1}.";
        string a = "Alice";
        string b = "Bob";
        SetEventProperty("gospel", string.Format(fmt, a, b));
    }
}
