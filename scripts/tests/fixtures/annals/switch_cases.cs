using System;

namespace XRL.Annals;

[Serializable]
public class FoundAsBabeFixture : HistoricEvent
{
    public override void Generate()
    {
        switch (Random(0, 2))
        {
            case 0:
                SetEventProperty("gospel", "case zero gospel.");
                break;
            case 1:
                SetEventProperty("gospel", "case one gospel.");
                break;
            default:
                SetEventProperty("gospel", "default gospel.");
                break;
        }
    }
}
