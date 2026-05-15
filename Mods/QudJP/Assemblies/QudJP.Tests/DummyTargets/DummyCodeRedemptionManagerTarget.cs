namespace QudJP.Tests.DummyTargets;

internal sealed class DummyCodeRedemptionManagerTarget
{
    public string PopupMessageToShow { get; set; } = string.Empty;

    public void redeemNoProgress()
    {
        DummyPopupShow.ShowAsync(
            PopupMessageToShow,
            CopyScrap: true,
            Capitalize: true,
            DimBackground: true,
            LogMessage: true,
            PushView: true).GetAwaiter().GetResult();
    }

    public void redeemProgressDelegate()
    {
        DummyPopupShow.ShowAsync(
            PopupMessageToShow,
            CopyScrap: true,
            Capitalize: true,
            DimBackground: true,
            LogMessage: true,
            PushView: true).GetAwaiter().GetResult();
    }
}
