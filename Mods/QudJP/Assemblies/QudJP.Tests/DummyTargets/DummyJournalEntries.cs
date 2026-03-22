namespace QudJP.Tests.DummyTargets;

internal class DummyBaseJournalEntry
{
    public string Text { get; set; } = string.Empty;

    public virtual string GetDisplayText()
    {
        return Text;
    }
}

internal sealed class DummyJournalAccomplishment : DummyBaseJournalEntry
{
    public string Category { get; set; } = "general";
}

internal sealed class DummyJournalObservation : DummyBaseJournalEntry
{
    public string Category { get; set; } = "general";
}

internal sealed class DummyJournalGeneralNote : DummyBaseJournalEntry
{
    public string Section { get; set; } = "General";
}

internal sealed class DummyJournalMapNote : DummyBaseJournalEntry
{
    public string Category { get; set; } = "Merchants";

    public override string GetDisplayText()
    {
        return Text;
    }
}
