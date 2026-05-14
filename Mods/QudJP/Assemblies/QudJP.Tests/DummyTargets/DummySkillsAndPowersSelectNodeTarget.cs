namespace QudJP.Tests.DummyTargets;

internal sealed class DummySkillsAndPowersSelectNodeTarget
{
    public string PopupMessageToShow { get; set; } = string.Empty;

    public string PopupSurface { get; set; } = nameof(DummyPopupShow.Show);

    public void SelectNode()
    {
        if (string.Equals(PopupSurface, nameof(DummyPopupShow.ShowYesNo), StringComparison.Ordinal))
        {
            DummyPopupShow.ShowYesNo(PopupMessageToShow);
            return;
        }

        if (string.Equals(PopupSurface, nameof(DummyPopupShow.ShowYesNoCancel), StringComparison.Ordinal))
        {
            DummyPopupShow.ShowYesNoCancel(PopupMessageToShow);
            return;
        }

        DummyPopupShow.Show(PopupMessageToShow);
    }
}
