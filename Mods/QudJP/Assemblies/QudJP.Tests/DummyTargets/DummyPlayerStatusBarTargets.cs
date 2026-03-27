using System.Collections.Generic;

namespace QudJP.Tests.DummyTargets;

internal sealed class DummyPlayerStatusBarTextSkin
{
    public string? text;

    public void SetText(string value)
    {
        text = value;
    }
}

internal sealed class DummyPlayerStatusBarProgressBar
{
    public DummyPlayerStatusBarTextSkin text = new DummyPlayerStatusBarTextSkin();
}

internal sealed class DummyPlayerStatusBarTarget
{
    private enum DummyStringDataType
    {
        FoodWater,
        Time,
        Temp,
        Weight,
        Zone,
        HPBar,
        PlayerName,
        ZoneOnly
    }

    private readonly Dictionary<DummyStringDataType, string> playerStringData = new Dictionary<DummyStringDataType, string>
    {
        { DummyStringDataType.FoodWater, string.Empty },
    };

    public DummyPlayerStatusBarProgressBar XPBar = new DummyPlayerStatusBarProgressBar();

    public string? NextFoodWater { get; set; }

    public string? NextTime { get; set; }

    public string? NextTemp { get; set; }

    public string? NextWeight { get; set; }

    public string? NextZone { get; set; }

    public string? NextZoneOnly { get; set; }

    public string? NextHpBar { get; set; }

    public string? NextPlayerName { get; set; }

    public int Level { get; set; } = 1;

    public int Experience { get; set; }

    public int NextLevelExperience { get; set; } = 220;

    public void BeginEndTurn(object? core)
    {
        _ = core;
        SetStringData(DummyStringDataType.FoodWater, NextFoodWater);
        SetStringData(DummyStringDataType.Time, NextTime);
        SetStringData(DummyStringDataType.Temp, NextTemp);
        SetStringData(DummyStringDataType.Weight, NextWeight);
        SetStringData(DummyStringDataType.Zone, NextZone);
        SetStringData(DummyStringDataType.ZoneOnly, NextZoneOnly);
        SetStringData(DummyStringDataType.HPBar, NextHpBar);
        SetStringData(DummyStringDataType.PlayerName, NextPlayerName);
    }

    public void Update()
    {
        XPBar.text.SetText($"LVL: {Level} Exp: {Experience} / {NextLevelExperience}");
    }

    public string? GetStringData(string name)
    {
        return Enum.TryParse<DummyStringDataType>(name, ignoreCase: false, out var key)
            && playerStringData.TryGetValue(key, out var value)
            ? value
            : null;
    }

    private void SetStringData(DummyStringDataType type, string? value)
    {
        if (value is not null)
        {
            playerStringData[type] = value;
        }
    }
}
