namespace QudJP.Tests.DummyTargets;

internal enum DummyMuralCategory
{
    Generic,
    FindsObject,
}

internal enum DummyMuralWeight
{
    Nil,
    Medium,
}

internal sealed class DummyJournalApiAccomplishment
{
    public string Text { get; set; } = string.Empty;

    public string MuralText { get; set; } = string.Empty;

    public string GospelText { get; set; } = string.Empty;

    public string Category { get; set; } = "general";

    public string? ID { get; set; }
}

internal sealed class DummyJournalApiMapNote
{
    public string ZoneID { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public string Category { get; set; } = "general";

    public string? ID { get; set; }
}

internal sealed class DummyJournalApiObservation
{
    public string Text { get; set; } = string.Empty;

    public string RevealText { get; set; } = string.Empty;

    public string Category { get; set; } = "general";

    public string ID { get; set; } = string.Empty;
}

internal static class DummyJournalApi
{
    public static List<DummyJournalApiAccomplishment> Accomplishments { get; } = new();

    public static List<DummyJournalApiMapNote> MapNotes { get; } = new();

    public static List<DummyJournalApiObservation> Observations { get; } = new();

    public static void Reset()
    {
        Accomplishments.Clear();
        MapNotes.Clear();
        Observations.Clear();
    }

    public static void AddAccomplishment(
        string text,
        string? muralText = null,
        string? gospelText = null,
        string? aggregateWith = null,
        string category = "general",
        DummyMuralCategory muralCategory = DummyMuralCategory.Generic,
        DummyMuralWeight muralWeight = DummyMuralWeight.Medium,
        string? secretId = null,
        long time = -1L,
        bool revealed = true)
    {
        _ = aggregateWith;
        _ = muralCategory;
        _ = muralWeight;
        _ = time;
        _ = revealed;
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        Accomplishments.Add(new DummyJournalApiAccomplishment
        {
            Text = text,
            MuralText = muralText ?? string.Empty,
            GospelText = gospelText ?? string.Empty,
            Category = category,
            ID = secretId,
        });
    }

    public static void AddMapNote(
        string ZoneID,
        string text,
        string category = "general",
        string[]? attributes = null,
        string? secretId = null,
        bool revealed = false,
        bool sold = false,
        long time = -1L,
        bool silent = false)
    {
        _ = attributes;
        _ = revealed;
        _ = sold;
        _ = time;
        _ = silent;
        MapNotes.Add(new DummyJournalApiMapNote
        {
            ZoneID = ZoneID,
            Text = text,
            Category = category,
            ID = secretId,
        });
    }

    public static void AddObservation(
        string text,
        string id,
        string category = "general",
        string? secretId = null,
        string[]? attributes = null,
        bool revealed = false,
        long time = -1L,
        string? additionalRevealText = null,
        bool initCapAsFragment = false,
        bool Tradable = true)
    {
        _ = secretId;
        _ = attributes;
        _ = revealed;
        _ = time;
        _ = initCapAsFragment;
        _ = Tradable;
        Observations.Add(new DummyJournalApiObservation
        {
            Text = text,
            RevealText = additionalRevealText ?? string.Empty,
            Category = category,
            ID = id,
        });
    }
}
