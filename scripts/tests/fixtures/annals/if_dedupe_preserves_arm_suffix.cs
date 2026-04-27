using System;

namespace XRL.Annals;

[Serializable]
public class IfDedupePreservesArmSuffixFixture : HistoricEvent
{
    public override void Generate()
    {
        int num = Random(0, 1);
        string text = num switch
        {
            0 => "alpha",
            _ => "beta",
        };
        if (Random(0, 1) == 0)
        {
            SetEventProperty("gospel", "base " + text);
        }
        else
        {
            SetEventProperty("gospel", "base " + text);
        }
    }
}
