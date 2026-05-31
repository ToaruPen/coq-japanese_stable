namespace QudJP.Tests.DummyTargets;

internal sealed class DummyUITextSkin
{
    public string? Text { get; private set; }

    public string? LastSetTextArgument { get; private set; }

    public string? text;

    public int SetTextCallCount { get; private set; }

    public int ApplyCallCount { get; private set; }

    public DummyActiveObject gameObject = new DummyActiveObject();

    public void SetText(string text)
    {
        SetTextCallCount++;
        LastSetTextArgument = text;
        Text = text;
        this.text = text;
    }

    public void Apply()
    {
        ApplyCallCount++;
        Text = text;
    }
}
