using System;

namespace XRL.Annals;

[Serializable]
public class BloodyBattleFixture : HistoricEvent
{
    public override void Generate()
    {
        SetEventProperty(
            "tombInscription",
            string.Format("In the {0}, {1} vanquished {2}.", "year", "Resheph", "an enemy"));
    }
}
