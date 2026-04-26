using System;

namespace XRL.Annals;

[Serializable]
public class DuplicateEventPropertyDedupeFixture : HistoricEvent
{
    public override void Generate()
    {
        if (Random(0, 1) == 0)
        {
            SetEventProperty("gospel", "Resheph triumphed.");
        }
        else
        {
            SetEventProperty("gospel", "Resheph triumphed.");
        }
    }
}
