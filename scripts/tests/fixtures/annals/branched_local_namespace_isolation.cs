using System;

namespace XRL.Annals;

// Regression test for the R3 namespace-mixing fix: a single generator that
// emits BOTH `#bl:` (branched-local fanout) and `#if:` (setter-chain) siblings
// against the same event property. Pre-R3, the bucket-key stripped both
// markers so divergent `#if:` siblings would land in the same bucket as
// identical `#bl:` siblings and block their collapse. Post-R3 the bucket key
// keeps each marker separate, so the `#bl:` family collapses on its own.
[Serializable]
public class BranchedLocalNamespaceIsolationFixture : HistoricEvent
{
    public override void Generate()
    {
        string text = "same";
        if (Random(0, 1) == 0)
        {
            text = "same";
        }
        SetEventProperty("gospel", text);
        if (Random(0, 1) == 0)
        {
            SetEventProperty("gospel", "x");
        }
        else
        {
            SetEventProperty("gospel", "y");
        }
    }
}
