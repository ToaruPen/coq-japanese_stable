using System;

namespace XRL.Annals;

// Regression test: when a branched-local fanout produces 2+ arms whose final
// resolved rhs is identical (the partial-branch reassigns to the same value
// the declared initializer holds), the post-extract collapse pass MUST merge
// them into one candidate. The collapse strips both `#if:` and `#bl:` so
// branched-local fanout siblings with identical templates also collapse.
[Serializable]
public class BranchedLocalCollapseIdenticalFixture : HistoricEvent
{
    public override void Generate()
    {
        string text = "same";
        if (Random(0, 1) == 0)
        {
            text = "same";
        }
        SetEventProperty("gospel", text);
    }
}
