using System;

namespace XRL.Annals;

// Minimal reproducer for the ChallengeSultan collision (issue-430 follow-up): a 3-arm chain
// drives a branched local for a setter that has NO `ResolveIfBranchSuffix` suffix of its own,
// while a sibling 2-arm `if/else` carries setters that DO get `#if:then` / `#if:else`. Pre-fix
// both paths emit `then` / `else` and collide on `gospel#if:then`. Post-fix the 3-arm chain
// emits `#bl:case0` / `#bl:case1` / `#bl:case2` (separate branched-local namespace),
// eliminating the collision.
[Serializable]
public class ElseifChainCollisionWithSiblingIfFixture : HistoricEvent
{
    public override void Generate()
    {
        string value;
        if (Random(0, 2) == 0)
        {
            value = "alpha";
        }
        else if (Random(0, 1) == 0)
        {
            value = "beta";
        }
        else
        {
            value = "gamma";
        }
        if (Random(0, 1) == 0)
        {
            SetEventProperty("gospel", value);
        }
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
