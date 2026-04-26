using System;

namespace XRL.Annals;

/// <summary>
/// Fixture: local variable whose initializer is a binary-concat that partially fails.
/// "lit" succeeds (left), then SomeClass.UnsupportedMethod() fails (right).
/// FlattenConcat must roll back the stale "lit" piece and emit a single slot for `a`.
/// </summary>
[Serializable]
public class PartialRollbackFixture : HistoricEvent
{
    public override void Generate()
    {
        string a = "lit" + SomeClass.UnsupportedMethod() + "rest";
        SetEventProperty("gospel", a + " world");
    }
}
