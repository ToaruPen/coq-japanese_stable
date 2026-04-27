using System;

namespace XRL.Annals;

[Serializable]
public class NeedsManualCollapsePreservesReasonFixture : HistoricEvent
{
    public override void Generate()
    {
        if (Random(0, 1) == 0)
        {
            // ConditionalExpression — unsupported AST kind for PR1 subset.
            SetEventProperty("gospel", true ? "x" : "y");
        }
        else
        {
            // ElementAccessExpression — unsupported AST kind for PR1 subset.
            SetEventProperty("gospel", new[] { "x" }[0]);
        }
    }
}
