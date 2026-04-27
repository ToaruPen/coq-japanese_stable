using System;

namespace XRL.Annals;

[Serializable]
public class UnconditionalAppendNoFanoutFixture : HistoricEvent
{
    public override void Generate()
    {
        string text = "Hello";
        text += ", world.";
        SetEventProperty("gospel", text);
    }
}
