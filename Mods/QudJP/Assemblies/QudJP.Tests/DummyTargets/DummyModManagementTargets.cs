using System.Collections.Generic;

namespace QudJP.Tests.DummyTargets;

internal sealed class DummyModMenuLineTarget
{
    public string Author = "Example Author";

    public DummyUITextSkin authorText = new();

    public List<string> tags = new();

    public void Update()
    {
        authorText.SetText("{{y|by " + Author + "}}");

        var index = 0;
        SetTag("{{green|ENABLED}}", ref index);
        SetTag("{{black|DISABLED}}", ref index);
        SetTag("{{red|FAILED}}", ref index);
        SetTag("{{W|# UPDATE AVAILABLE}}", ref index);
    }

    public void SetTag(string Text, ref int Index, bool State = true)
    {
        if (!State)
        {
            return;
        }

        tags.Add(Text);
        Index++;
    }
}

internal sealed class DummyModManagerUITarget
{
    public DummyUITextSkin SelectedModAuthor = new();

    public void OnSelect(string author)
    {
        SelectedModAuthor.SetText("{{C|by " + author + "}}");
    }
}

internal sealed class DummySteamWorkshopUploaderViewTarget
{
    public string LastPopup = string.Empty;

    public string LastProgressText = string.Empty;

    public float LastProgressValue;

    public void Popup(string text)
    {
        LastPopup = text;
    }

    public void ShowProgress(string Text)
    {
        LastProgressText = Text;
        LastProgressValue = 0f;
    }

    public void SetProgress(string Text, float Progress)
    {
        LastProgressText = Text;
        LastProgressValue = Progress;
    }
}

internal sealed class DummyModInfoTarget
{
    public string DisplayTitle = "Sample Mod";

    public string DisplayTitleStripped = "Sample Mod";

    public string RemoteVersion = "2.0.5";

    public string LastDependencyStatus = string.Empty;

    public string LastDependencyPopupTitle = string.Empty;

    public string LastUpdatePopupTitle = string.Empty;

    public string LastUpdatePopupMessage = string.Empty;

    public string LastLoadingText = string.Empty;

    public string AppendDependencyConfirmation(int mode)
    {
        _ = DisplayTitle;
        if (mode == 0)
        {
            LastDependencyStatus = "Invalid";
            return LastDependencyStatus;
        }

        if (mode == 1)
        {
            LastDependencyStatus = "OK";
            return LastDependencyStatus;
        }

        if (mode == 2)
        {
            LastDependencyStatus = "Version mismatch";
            return LastDependencyStatus;
        }

        LastDependencyStatus = "Missing";
        return LastDependencyStatus;
    }

    public void ConfirmDependencies()
    {
        LastDependencyPopupTitle = "{{W|Dependencies}}";
    }

    public void ConfirmUpdate()
    {
        LastUpdatePopupMessage = DisplayTitle + " has a new version available: " + RemoteVersion + ".";
        LastUpdatePopupMessage += "\n\nDo you want to download it?";
        LastUpdatePopupTitle = "{{W|Update Available}}";
    }

    public string DownloadUpdate()
    {
        LastLoadingText = "Updating " + DisplayTitleStripped + "...";
        return LastLoadingText;
    }
}

internal sealed class DummyModScrollerOneTarget
{
    public string DisplayTitle = "Sample Mod";

    public string LastPopupMessage = string.Empty;

    public void OnActivate()
    {
        LastPopupMessage = DisplayTitle
            + " contains scripts and has been permanently disabled in the options.\n{{K|(Options->Modding->Allow scripting mods)}}";
    }
}
