using System;

namespace XRL.Annals;

[Serializable]
public class HistoryKitTokensFixture : HistoricEvent
{
    public override void Generate()
    {
        SetEventProperty("gospel", "<$chosenSpice=spice.elements.entity$elements[random]><entity.name> was adopted by <$chosenSpice.professions.!random> who love <$chosenSpice.practices.!random>.");
    }
}
