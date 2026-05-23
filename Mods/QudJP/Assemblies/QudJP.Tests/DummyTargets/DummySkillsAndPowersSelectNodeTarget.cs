namespace QudJP.Tests.DummyTargets;

internal sealed class DummySkillsAndPowersSelectNodeTarget
{
    public string PopupMessageToShow { get; set; } = string.Empty;

    public string PopupSurface { get; set; } = nameof(DummyPopupShow.Show);

    public string Kind { get; set; } = "power";

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

    public void SelectNodeNotEnoughSkillPointsMessageLog()
    {
        DummyMessageQueue.AddPlayerMessage("You don't have enough skill points to buy that " + Kind + "!");
    }

    public static void SelectNodeRequiredSkillPromptMessageLog()
    {
        DummyMessageQueue.AddPlayerMessage(
            "You do not have the skill associated with that power. Would you like to purchase the required skill?");
    }
}
