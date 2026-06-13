namespace QudJP.Tests.DummyTargets;

internal sealed class DummySaveInfoData
{
    public DummySaveGameInfo SaveGame { get; set; } = new();
}

internal sealed class DummySaveGameInfo
{
    public string Name { get; set; } = "Warden";

    public string Description { get; set; } = "Level 29  [Roleplay]";

    public string Info { get; set; } = "Bethesda Susa, 7 turn 12345";

    public string SaveTime { get; set; } = "Wednesday, June 10, 2026 at 5:58:42 PM";

    public string Size { get; set; } = "Total size: 12mb";

    public string ID { get; set; } = "save-123";
}

internal sealed class DummySaveManagementRowTarget
{
    public DummyUITextSkin[] TextSkins { get; } =
    {
        new DummyUITextSkin(),
        new DummyUITextSkin(),
        new DummyUITextSkin(),
        new DummyUITextSkin(),
    };

    public void setData(object data)
    {
        if (data is not DummySaveInfoData saveInfoData)
        {
            return;
        }

        TextSkins[0].SetText("{{W|" + saveInfoData.SaveGame.Name + " :: " + saveInfoData.SaveGame.Description + " }}");
        TextSkins[1].SetText("{{C|Location:}} " + saveInfoData.SaveGame.Info);
        TextSkins[2].SetText("{{C|Last saved:}} " + saveInfoData.SaveGame.SaveTime);
        TextSkins[3].SetText("{{K|" + saveInfoData.SaveGame.Size + " {" + saveInfoData.SaveGame.ID + "} }}");
    }
}

internal static class DummySavesApiTarget
{
    public static DummySaveGameInfo ReadSaveJson(string dir, string file)
    {
        _ = dir;
        _ = file;
        return new DummySaveGameInfo();
    }
}
