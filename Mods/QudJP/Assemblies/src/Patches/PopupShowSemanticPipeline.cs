namespace QudJP.Patches;

internal static class PopupShowSemanticPipeline
{
    internal static string TranslateMessage(string source, string route)
    {
        if (string.IsNullOrEmpty(source))
        {
            if (source is null)
            {
                return string.Empty;
            }

            return source;
        }

        if (TradeUiPopupTranslationPatch.TryTranslatePerformOfferTradeWaterMessage(source, out var tradeWaterTranslated))
        {
            return tradeWaterTranslated;
        }

        if (TradeUiPopupTranslationPatch.TryTranslateHasNothingToTradeMessage(source, out var hasNothingTranslated))
        {
            return hasNothingTranslated;
        }

        if (GameObjectStatPopupTranslationPatch.TryTranslatePopupMessage(source, route, "Popup.Show", out var statTranslated))
        {
            return statTranslated;
        }

        return PopupTranslationPatch.TranslatePopupTextForProducerRoute(source, route);
    }
}
