using System;

namespace XRL.Annals;

[Serializable]
public class HistorykitTokenRepeatedLocalFixture : HistoricEvent
{
    public override void Generate()
    {
        string prefix = "<entity.";
        string composed = prefix + "name>" + prefix + "type>";
        SetEventProperty("gospel", ExpandString(composed));
    }
}
