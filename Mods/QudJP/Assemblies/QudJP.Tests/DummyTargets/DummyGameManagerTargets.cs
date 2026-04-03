namespace QudJP.Tests.DummyTargets;

internal sealed class DummyGameManagerSelectedAbilityTarget
{
    public DummyGameManagerText selectedAbilityText = new DummyGameManagerText();

    public string NextSelectedAbilityText { get; set; } = string.Empty;

    public void UpdateSelectedAbility()
    {
        selectedAbilityText.SetText(NextSelectedAbilityText);
    }
}

internal sealed class DummyGameManagerText
{
    public string text { get; private set; } = string.Empty;

    public void SetText(string value)
    {
        text = value;
    }
}
