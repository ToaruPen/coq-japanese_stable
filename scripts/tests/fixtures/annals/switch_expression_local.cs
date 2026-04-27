using System;

namespace XRL.Annals;

[Serializable]
public class SwitchExpressionLocalFixture : HistoricEvent
{
    public override void Generate()
    {
        int num = Random(0, 2);
        string prefix = num switch
        {
            0 => "deep in the marsh, ",
            1 => "while wandering, ",
            _ => "deep in the wilds, ",
        };
        SetEventProperty("gospel", prefix + "Resheph discovered Joppa.");
    }
}
