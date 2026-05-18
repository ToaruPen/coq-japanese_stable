namespace QudJP.Patches;

internal static class BroadcastPowerOcclusionReasonTranslator
{
    internal static bool TryTranslate(string? source, out string translated)
    {
        if (string.IsNullOrEmpty(source))
        {
            if (source is null)
            {
                translated = string.Empty;
                return false;
            }

            translated = source;
            return false;
        }

        var original = source!;
        if (MessageFrameTranslator.TryStripDirectTranslationMarker(original, out var markedText))
        {
            translated = markedText;
            return false;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(original);
        var translatedCore = stripped switch
        {
            "orbital debris" => "軌道上の残骸",
            "a glass storm" => "ガラス嵐",
            "a flock of birds" => "鳥の群れ",
            "acid rain" => "酸性雨",
            "drift film" => "ドリフト膜",
            "an unidentified anomaly" => "未確認の異常",
            _ => stripped,
        };
        if (string.Equals(translatedCore, stripped, System.StringComparison.Ordinal))
        {
            translated = original;
            return false;
        }

        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translatedCore,
            spans,
            stripped.Length,
            original);
        return true;
    }
}
