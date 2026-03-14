namespace QudJP.Tests.DummyTargets;

internal sealed class DummyFactionsStatusScreen
{
    public List<DummyFactionsLineData> rawData =
    [
        new DummyFactionsLineData("The villagers of Abal"),
        new DummyFactionsLineData("Reputation:     0"),
        new DummyFactionsLineData("The villagers of Abal don't care about you, but aggressive ones will attack you."),
        new DummyFactionsLineData("The villagers of Abal are interested in hearing gossip that's about them."),
        new DummyFactionsLineData("You aren't welcome in their holy places."),
        new DummyFactionsLineData("You are welcome in their holy places."),
    ];

    public List<DummyFactionsLineData> sortedData =
    [
        new DummyFactionsLineData("The villagers of Biwar"),
        new DummyFactionsLineData("Reputation:  -475"),
        new DummyFactionsLineData("The villagers of Biwar don't care about you, but aggressive ones will attack you."),
        new DummyFactionsLineData("The villagers of Biwar are interested in hearing gossip that's about them."),
    ];

    public void UpdateViewFromData()
    {
    }
}

internal sealed class DummyFactionsLineData
{
    public DummyFactionsLineData(string label)
    {
        this.label = label;
        _searchText = label;
    }

    public string label;

    public string _searchText;
}
