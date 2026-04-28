using System;

namespace XRL.Annals;

// Regression test for the nested-if name-leak bug (CR R11): branched-local selection
// must NOT pick up names whose only writes are inside a nested `if`. Pre-fix, the
// outer name-discovery walked `root.DescendantNodes()`, found `value = "x"` inside
// the inner `if` of the outer `then` arm, and built a 2-arm fanout {"x", "y"} that
// silently dropped the runtime path where outer=true but inner=false (value would
// retain its initializer "default"). Post-fix, names assigned only under nested
// conditions are excluded from branched-local seeding, so the extractor falls
// through to its needs_manual reason rather than emitting an under-counted fanout.
[Serializable]
public class BranchedLocalNestedIfExclusionFixture : HistoricEvent
{
    public override void Generate()
    {
        string value = "default";
        if (Random(0, 1) == 0)
        {
            if (Random(0, 1) == 0)
            {
                value = "x";
            }
        }
        else
        {
            value = "y";
        }
        SetEventProperty("gospel", value);
    }
}
