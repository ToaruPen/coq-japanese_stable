namespace QudJP.Tests.DummyTargets;

internal sealed class DummyTradeScreenTarget
{
    public DummyUITextSkin[] totalLabels = new DummyUITextSkin[]
    {
        new DummyUITextSkin(),
        new DummyUITextSkin(),
    };

    public DummyUITextSkin[] freeDramsLabels = new DummyUITextSkin[]
    {
        new DummyUITextSkin(),
        new DummyUITextSkin(),
    };

    public void UpdateTotals()
    {
        totalLabels[0].SetText("{{B|42 drams →}}");
        totalLabels[1].SetText("{{B|← 10 drams}}");
        freeDramsLabels[0].SetText("{{W|$100}}");
        freeDramsLabels[1].SetText("{{W|$50}} | {{K|123/200 lbs.}}");
    }
}
