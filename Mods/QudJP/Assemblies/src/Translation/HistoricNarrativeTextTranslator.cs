namespace QudJP;

/// <summary>
/// Translates HistoricStringExpander narrative prose (sultan gospel/tomb inscription,
/// village proverb/Gospels list/sacredThings/profaneThings/dialog list properties).
/// Delegates to JournalPatternTranslator without applying a direct marker so that
/// VillageStoryReveal and other non-journal display paths render the translated
/// text as-is.
/// </summary>
internal static class HistoricNarrativeTextTranslator
{
    internal static string Translate(string? source, string? context = null)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source ?? string.Empty;
        }
        return JournalPatternTranslator.Translate(source, context);
    }
}
