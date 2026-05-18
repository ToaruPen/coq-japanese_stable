namespace QudJP.Patches;

internal static class BroadcastPowerOcclusionReasonTranslator
{
    internal static bool TryTranslate(string? source, out string translated)
    {
        if (string.IsNullOrEmpty(source))
        {
            translated = source ?? string.Empty;
            return false;
        }

        var original = source!;
        if (MessageFrameTranslator.TryStripDirectTranslationMarker(original, out var markedText))
        {
            translated = markedText;
            return false;
        }

        translated = original switch
        {
            "orbital debris" => "軌道上の残骸",
            "a glass storm" => "ガラス嵐",
            "a flock of birds" => "鳥の群れ",
            "acid rain" => "酸性雨",
            "drift film" => "ドリフト膜",
            "an unidentified anomaly" => "未確認の異常",
            _ => original,
        };
        return !string.Equals(translated, original, System.StringComparison.Ordinal);
    }
}
