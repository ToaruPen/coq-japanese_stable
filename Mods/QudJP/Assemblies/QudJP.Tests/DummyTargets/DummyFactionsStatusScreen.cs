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
        new DummyFactionsLineData("The villagers of Biwar are preparing for war."),
    ];

    public void UpdateViewFromData()
    {
    }
}

internal sealed class DummyFactionsLineData
{
    public DummyFactionsLineData(string label, string? id = null)
    {
        set(id ?? label, label, icon: null, expanded: true);
    }

    public string id = string.Empty;

    public string label = string.Empty;

    public object? icon;

    public bool expanded = true;

    public string? _searchText;

    public string searchText
    {
        get
        {
            if (_searchText is null)
            {
#pragma warning disable CA1308
                _searchText = string.IsNullOrEmpty(id)
                    ? label.ToLowerInvariant()
                    : $"the villagers of {id}".ToLowerInvariant();
#pragma warning restore CA1308
            }

            return _searchText;
        }
#pragma warning disable CA1308
        set => _searchText = value?.ToLowerInvariant();
#pragma warning restore CA1308
    }

    public DummyFactionsLineData set(string id, string label, object? icon, bool expanded)
    {
        this.id = id;
        this.label = label;
        this.icon = icon;
        this.expanded = expanded;
        searchText = null!;
        return this;
    }
}

internal sealed class DummyTextSkin
{
    public string text = string.Empty;

    public bool enableWordWrapping { get; set; }

    public bool textWrapping { get; set; }

    public DummyTextWrappingMode textWrappingMode { get; set; } = DummyTextWrappingMode.NoWrap;

    public bool SetText(string value)
    {
        text = value;
        return true;
    }
}

internal enum DummyTextWrappingMode
{
    NoWrap,
    Normal,
}

internal sealed class DummyFactionsLine
{
    public DummyTextSkin barText = new DummyTextSkin();

    public DummyTextSkin barReputationText = new DummyTextSkin();

    public DummyTextSkin detailsText = new DummyTextSkin();

    public DummyTextSkin detailsText2 = new DummyTextSkin();

    public DummyTextSkin detailsText3 = new DummyTextSkin();

    public void setData(DummyFactionsLineData data)
    {
        barText.SetText(data.label);
        barReputationText.SetText("Reputation: 0");
        detailsText.SetText("{{C|The villagers of Abal}} don't care about you, but aggressive ones will attack you.");
        detailsText2.SetText("You aren't welcome in their holy places.");
        detailsText3.SetText("The Arbitrarilyborn Cult is interested in trading secrets about the sultan they worship. They're also interested in hearing gossip that's about them.");
    }
}
