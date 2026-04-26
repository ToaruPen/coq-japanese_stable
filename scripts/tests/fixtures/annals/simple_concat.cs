using System;

namespace XRL.Annals;

[Serializable]
public class ReshephIsBornFixture : HistoricEvent
{
    public override void Generate()
    {
        string text = "<spice.commonPhrases.oneStarryNight.!random.capitalize>";
        SetEventProperty("gospel", text + ", a sultan was born in the salt marsh.");
    }
}
