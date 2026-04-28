using System;

namespace XRL.Annals;

// Regression test for the nested-if name-leak bug (CR R11): branched-local selection
// must NOT pick up names whose only writes are inside a nested `if`. Pre-fix, the
// outer name-discovery walked `root.DescendantNodes()`, found `value = "x"` inside
// the inner `if` of the outer `then` arm, and built a 2-arm fanout {"x", "y"} that
// silently dropped the runtime path where outer=true but inner=false (value would
// retain its initializer "default"). Post-fix, the `nestedIfNames` filter excludes
// `value` from branched-local seeding, so the extractor falls back to last-write
// resolution (selecting the source-latest `value = "y"`) and emits a single
// `status: pending` candidate rather than an under-counted fanout. Fully correct
// nested-if fanout is out of scope for PR1; locking in the last-write fallback
// here is the stop-gap that prevents the original silent-drop regression.
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
