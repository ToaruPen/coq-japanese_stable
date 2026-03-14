namespace QudJP.Tests.DummyTargets;

internal sealed class DummyFieldOnlyUITextSkin
{
    public string text = string.Empty;
}

internal sealed class DummyPropertyOnlyUITextSkin
{
    public string Text { get; set; } = string.Empty;
}

internal sealed class DummyLowercasePropertyUITextSkin
{
    public string text { get; set; } = string.Empty;
}
