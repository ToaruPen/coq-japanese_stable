namespace QudJP.Tests.DummyTargets;

internal sealed class DummyAbilityBarAfterRenderTarget
{
    private string effectText = string.Empty;

    private bool effectTextDirty;

    private string targetText = string.Empty;

    private string targetHealthText = string.Empty;

    public DummyUITextSkin EffectText { get; } = new DummyUITextSkin();

    public string NextEffectText { get; set; } = string.Empty;

    public string NextTargetText { get; set; } = string.Empty;

    public string NextTargetHealthText { get; set; } = string.Empty;

    public void AfterRender(object? core, object? sb)
    {
        _ = core;
        _ = sb;
        effectText = NextEffectText;
        effectTextDirty = true;
        targetText = NextTargetText;
        targetHealthText = NextTargetHealthText;
    }

    public void Update()
    {
        if (effectTextDirty)
        {
            EffectText.SetText(effectText);
            effectTextDirty = false;
        }
    }

    public string GetEffectText()
    {
        return effectText;
    }

    public string GetTargetText()
    {
        return targetText;
    }

    public string GetTargetHealthText()
    {
        return targetHealthText;
    }
}

internal sealed class DummyAbilityBarButtonTextTarget
{
    public List<object> AbilityButtons = new List<object>();

    public void Update()
    {
    }
}

internal sealed class DummyAbilityBarButton
{
    public DummyUITextSkin Text = new DummyUITextSkin();

    public DummyAbilityBarButton(string text)
    {
        Text.SetText(text);
    }
}
