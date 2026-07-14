#pragma warning disable CS0649

using System.Globalization;

namespace QudJP.Tests.DummyTargets;

internal static class Options
{
    public static bool ShowNumberOfItems = true;
}

internal sealed class DummyFallbackInventoryLineDataTarget
{
    public string FallbackText { get; set; } = "inventory fallback";
}

internal sealed class DummyInventoryLineDataTarget
{
    public bool category { get; set; }

    public string categoryName { get; set; } = string.Empty;

    public bool categoryExpanded { get; set; }

    public int categoryWeight { get; set; }

    public int categoryAmount { get; set; }

    public DummyStatusGameObject? go { get; set; }

    public string? displayName { get; set; }
}

internal sealed class DummyInventoryLineTarget
{
    public bool OriginalExecuted { get; private set; }

    public DummyCommandContext context = new DummyCommandContext();

    public DummyUITextSkin hotkeyText = new DummyUITextSkin();

    public DummyEnabledObject dotImage = new DummyEnabledObject();

    public DummyActiveObject hotkeySpacer = new DummyActiveObject();

    public DummyActiveObject categoryMode = new DummyActiveObject();

    public DummyActiveObject itemMode = new DummyActiveObject();

    public DummyUITextSkin categoryLabel = new DummyUITextSkin();

    public DummyUITextSkin categoryExpandLabel = new DummyUITextSkin();

    public DummyUITextSkin categoryWeightText = new DummyUITextSkin();

    public DummyUITextSkin itemWeightText = new DummyUITextSkin();

    public DummyUITextSkin text = new DummyUITextSkin();

    public DummyIconTarget icon = new DummyIconTarget();

    public object? tooltipGo;

    public object? tooltipCompareGo;

    public object? hotkey;

    public bool UpdateHotkeyCalled { get; private set; }

    public void UpdateHotkey()
    {
        UpdateHotkeyCalled = true;
    }

    public void setData(object data)
    {
        OriginalExecuted = true;
        context.data = data;

        if (data is DummyFallbackInventoryLineDataTarget fallback)
        {
            text.SetText(fallback.FallbackText);
            return;
        }

        if (data is not DummyInventoryLineDataTarget lineData)
        {
            return;
        }

        dotImage.enabled = lineData.category;
        if (lineData.category)
        {
            hotkeySpacer.SetActive(active: false);
            categoryMode.SetActive(active: true);
            itemMode.SetActive(active: false);
            categoryLabel.SetText(lineData.categoryName);
            categoryExpandLabel.SetText(lineData.categoryExpanded ? "[-]" : "[+]");
            categoryWeightText.SetText($"|{lineData.categoryAmount.ToString(CultureInfo.InvariantCulture)} items|{lineData.categoryWeight.ToString(CultureInfo.InvariantCulture)} lbs.|");
            itemWeightText.SetText(string.Empty);
            return;
        }

        hotkeySpacer.SetActive(active: true);
        categoryMode.SetActive(active: false);
        itemMode.SetActive(active: true);
        tooltipGo = lineData.go;
        categoryWeightText.SetText(string.Empty);
        itemWeightText.SetText($"[{(lineData.go?.Weight ?? 0).ToString(CultureInfo.InvariantCulture)} lbs.]");
        text.SetText(lineData.displayName ?? lineData.go?.DisplayName ?? string.Empty);
        icon.FromRenderable(lineData.go?.RenderForUI("Inventory"));
    }
}
