using System.Collections.Generic;

namespace QudJP.Tests.DummyTargets;

internal sealed class DummyPickGameObjectTargetObject
{
    public string DisplayName { get; set; } = string.Empty;

    public bool OwnedByPlayer { get; set; }

    public int Weight { get; set; }

    public string ListDisplayContext { get; set; } = string.Empty;

    public string IDIfAssigned { get; set; } = "item-1";

    public object RenderForUI() => new DummyRenderable(DisplayName);

    public int GetWeight() => Weight;

    public string GetListDisplayContext(object? player)
    {
        _ = player;
        return ListDisplayContext;
    }
}

internal static class PickGameObjectScreen
{
    public static bool NotePlayerOwned;

    public static bool ShowContext;

    public static void Reset()
    {
        NotePlayerOwned = false;
        ShowContext = false;
    }
}

internal sealed class DummyPickGameObjectLineDataTarget
{
    public string? style { get; set; }

    public DummyPickGameObjectTargetObject? go { get; set; }

    public string category { get; set; } = string.Empty;

    public bool collapsed { get; set; }

    public bool indent { get; set; }

    public string? hotkeyDescription { get; set; }
}

internal sealed class DummyGameSignaturePickGameObjectLineDataTarget
{
    public string? style { get; set; }

    public object? go { get; set; }

    public string category { get; set; } = string.Empty;

    public bool collapsed { get; set; }

    public bool indent { get; set; }

    public string? hotkeyDescription { get; set; }
}

internal sealed class DummyGameSignaturePickGameObjectTargetObject
{
    public string DisplayName { get; set; } = string.Empty;

    public bool OwnedByPlayer { get; set; }

    public int Weight { get; set; }

    public string ListDisplayContext { get; set; } = string.Empty;

    public string IDIfAssigned { get; set; } = "item-1";

    public string? LastRenderContext { get; private set; }

    public bool? LastAsIfKnown { get; private set; }

    public object RenderForUI(string? context = null, bool asIfKnown = false)
    {
        LastRenderContext = context;
        LastAsIfKnown = asIfKnown;
        return new DummyRenderable(DisplayName);
    }

    public int GetWeight() => Weight;

    public string GetListDisplayContext(object? player)
    {
        _ = player;
        return ListDisplayContext;
    }
}

internal sealed class DummyFallbackPickGameObjectLineDataTarget
{
    public string FallbackText { get; set; } = "pick fallback";
}

internal sealed class DummyPickGameObjectLineTarget
{
    public static List<DummyMenuOption> categoryExpandOptions = new List<DummyMenuOption> { new DummyMenuOption("Expand", "Accept") };

    public static List<DummyMenuOption> categoryCollapseOptions = new List<DummyMenuOption> { new DummyMenuOption("Collapse", "Accept") };

    public static List<DummyMenuOption> itemOptions = new List<DummyMenuOption> { new DummyMenuOption("Select", "Accept") };

    public bool OriginalExecuted { get; private set; }

    public DummyStatusContext context = new DummyStatusContext();

    public DummyUITextSkin text = new DummyUITextSkin();

    public DummyUITextSkin check = new DummyUITextSkin();

    public DummyUITextSkin hotkey = new DummyUITextSkin();

    public DummyUITextSkin rightFloatText = new DummyUITextSkin();

    public DummyUiIconWithGameObject icon = new DummyUiIconWithGameObject();

    public DummyActiveObject iconSpacer = new DummyActiveObject();

    public static void ResetStaticMenuOptions()
    {
        categoryExpandOptions = new List<DummyMenuOption> { new DummyMenuOption("Expand", "Accept") };
        categoryCollapseOptions = new List<DummyMenuOption> { new DummyMenuOption("Collapse", "Accept") };
        itemOptions = new List<DummyMenuOption> { new DummyMenuOption("Select", "Accept") };
    }

    public void setData(object data)
    {
        OriginalExecuted = true;
        context.data = data;
        if (data is DummyFallbackPickGameObjectLineDataTarget fallback)
        {
            text.SetText(fallback.FallbackText);
            return;
        }

        if (data is not DummyPickGameObjectLineDataTarget lineData)
        {
            return;
        }

        if (lineData.go is null)
        {
            text.SetText("[" + (lineData.collapsed ? "+" : "-") + "] {{K|" + lineData.category + "}}");
            rightFloatText.SetText(string.Empty);
            icon.gameObject.SetActive(false);
            iconSpacer.SetActive(false);
            return;
        }

        text.SetText(lineData.go.DisplayName);
        rightFloatText.SetText("{{K|" + lineData.go.Weight + "#}}");
        icon.gameObject.SetActive(true);
        iconSpacer.SetActive(true);
        icon.FromRenderable(lineData.go.RenderForUI());
    }
}
