using System;

namespace XRL.Annals;

// Branch fanout when only ONE branch reassigns the local. The unassigned else-branch keeps
// the local's declared initializer ("default"); the then-branch overrides it with "alt".
// Runtime can produce EITHER value, so the extractor emits TWO candidates with `#bl:then`
// / `#bl:else` ids — runtime-faithful per the BuildSetterScopedLocals design.
[Serializable]
public class BranchFanoutWithSetterOutsideIfFixture : HistoricEvent
{
    public override void Generate()
    {
        string text = "default";
        if (Random(0, 1) == 0)
        {
            text = "alt";
        }
        SetEventProperty("gospel", text);
    }
}
