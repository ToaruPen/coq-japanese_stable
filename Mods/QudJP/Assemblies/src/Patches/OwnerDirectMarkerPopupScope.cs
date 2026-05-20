namespace QudJP.Patches;

internal static class OwnerDirectMarkerPopupScope
{
    internal static void Enter(ref int activeDepth)
    {
        OwnerTranslationScope.Enter(ref activeDepth);
    }

    internal static void Exit(
        ref int activeDepth,
        ref string? passThroughText,
        string? previousPassThroughText)
    {
        try
        {
            OwnerTranslationScope.Exit(ref activeDepth);
        }
        finally
        {
            passThroughText = previousPassThroughText;
        }
    }

    internal static bool TryStripDirectMarkedProducerText(string source, out string translated)
    {
        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return true;
        }

        translated = source;
        return false;
    }
}
