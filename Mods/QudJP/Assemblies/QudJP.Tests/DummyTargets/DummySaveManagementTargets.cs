namespace QudJP.Tests.DummyTargets;

internal sealed class DummySaveInfoData
{
    public DummySaveGameInfo SaveGame { get; set; } = new();
}

internal sealed class DummySaveGameInfo
{
    public string Name { get; set; } = "Warden";

    public string Description { get; set; } = "Mutated Human";

    public string Info { get; set; } = "Joppa";

    public string SaveTime { get; set; } = "1 hour ago";

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
