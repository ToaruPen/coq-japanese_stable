using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

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

    public string LastFailurePopupTitle = string.Empty;

    public string LastFailurePopupMessage = string.Empty;

    public string LastRetryText = string.Empty;

    public string LastWorkshopText = string.Empty;

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

    public void ConfirmFailure()
    {
        LastFailurePopupTitle = DisplayTitle + " - {{R|Errors}}";
        LastFailurePopupMessage = "first error\nsecond error\nthird error";
        LastFailurePopupMessage = LastFailurePopupMessage + "\n(... {{R|+" + 2 + "}} more)";
        LastFailurePopupMessage = LastFailurePopupMessage
            + "\n\nAutomatically on your clipboard should you wish to forward it to "
            + "the mod author"
            + ".";
        LastRetryText = "{{W|[R]}} {{y|Retry}}";
        LastWorkshopText = "{{W|[W]}} {{y|Workshop}}";
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

internal sealed class DummyXrlCoreRestoreModsLoadedTarget
{
    public async Task<DummyXrlCoreRestoreModsLoadedResult> RestoreModsLoadedAsync()
    {
        await Task.Yield();

        var unavailable = new StringBuilder()
            .Append("One or more mods enabled in this save are {{red|not available}}:{{red|")
            .Append("Sample Mod")
            .Append("}}Do you still wish to try to load this save?")
            .ToString();

        var differs = new StringBuilder()
            .Append("These mods are {{red|disabled}} in the save:{{red|")
            .Append("Extra Mod")
            .Append("}}")
            .AppendLine()
            .Append("These mods are {{green|enabled}} in the save:{{green|")
            .Append("Missing Mod")
            .Append("}}")
            .ToString();

        return new DummyXrlCoreRestoreModsLoadedResult
        {
            IncompleteTitle = "Incomplete Mod Configuration",
            ColoredIncompleteTitle = "{{red|Incomplete Mod Configuration}}",
            UnavailableMessage = unavailable,
            DiffersTitle = "Mod Configuration Differs",
            DirectMarkedDiffersTitle = "\u0001Mod構成が異なります",
            DiffersMessage = differs,
            Options =
            [
                "Restart using save game's mod configuration",
                "Load keeping current mod configuration",
            ],
        };
    }
}

internal sealed class DummyXrlCoreRestoreModsLoadedResult
{
    public string IncompleteTitle { get; init; } = string.Empty;

    public string ColoredIncompleteTitle { get; init; } = string.Empty;

    public string UnavailableMessage { get; init; } = string.Empty;

    public string DiffersTitle { get; init; } = string.Empty;

    public string DirectMarkedDiffersTitle { get; init; } = string.Empty;

    public string DiffersMessage { get; init; } = string.Empty;

    public string[] Options { get; init; } = [];
}
