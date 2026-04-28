using System;

namespace XRL.Annals;

// Regression test for the inner-loop reverse fix (CR R2 #1) AND the branch-
// fanout extension (CR R3): given multiple SimpleAssignments to the same local
// within a single sibling stmt, the extractor picks the source-LATEST per
// branch, then emits ONE candidate per branch of the enclosing if/else.
//
// Runtime values: "second" (then-branch, source-latest of `value="first"; value="second"`)
// OR "alt" (else-branch). Both are reachable, so the extractor emits two
// candidates (`#bl:then` → `^second$`, `#bl:else` → `^alt$`). None of the 5
// PR2a target files exhibit this shape, but Resheph 16 byte-identicality is
// preserved because no Resheph file has setter-outside-if with branch-distinct
// SimpleAssignments either.
[Serializable]
public class LatestAssignmentInsideBlockFixture : HistoricEvent
{
    public override void Generate()
    {
        string value;
        if (Random(0, 1) == 0)
        {
            value = "first";
            value = "second";
        }
        else
        {
            value = "alt";
        }
        SetEventProperty("gospel", value);
    }
}
