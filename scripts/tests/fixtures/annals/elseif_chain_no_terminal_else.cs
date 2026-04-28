using System;

namespace XRL.Annals;

// Regression test for the implicit-no-op-arm bug (CR R10): when a chain has no
// terminal `else`, the runtime path where every condition is false must still
// produce a candidate (the local stays at its declared initializer). Pre-fix
// `if A else if B; setter(x)` produced two candidates `then`/`else` and dropped
// the `!a && !b` path; post-fix it produces three case-labelled candidates with
// the third being the implicit no-op arm using the initializer.
[Serializable]
public class ElseifChainNoTerminalElseFixture : HistoricEvent
{
    public override void Generate()
    {
        string value = "default";
        if (Random(0, 2) == 0)
        {
            value = "alpha";
        }
        else if (Random(0, 1) == 0)
        {
            value = "beta";
        }
        SetEventProperty("gospel", value);
    }
}
