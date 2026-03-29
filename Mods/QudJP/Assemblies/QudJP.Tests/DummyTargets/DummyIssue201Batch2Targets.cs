#pragma warning disable CS0649

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace QudJP.Tests.DummyTargets;

internal sealed class DummyActiveObject
{
    public bool activeSelf = true;

    public bool Active => activeSelf;

    public void SetActive(bool active)
    {
        activeSelf = active;
    }
}

internal sealed class DummyEnabledObject
{
    public bool enabled;
}

internal sealed class DummyIconTarget
{
    public object? LastRenderable { get; private set; }

    public void FromRenderable(object? renderable)
    {
        LastRenderable = renderable;
    }
}

internal sealed class DummyCommandContext
{
    public object? data;

    public Dictionary<string, Action>? commandHandlers;

    public IDictionary? axisHandlers;
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

internal sealed class DummyBody
{
    public int PartDepth { get; set; }

    public int GetPartDepth(object? part)
    {
        _ = part;
        return PartDepth;
    }
}

internal sealed class DummyBodyPart
{
    public string Name { get; set; } = "Hand";

    public bool Primary { get; set; }

    public string CardinalDescription { get; set; } = "Right Hand";

    public DummyStatusGameObject? Equipped { get; set; }

    public DummyStatusGameObject? DefaultBehavior { get; set; }

    public DummyStatusGameObject? Cybernetics { get; set; }

    public DummyBody ParentBody { get; set; } = new DummyBody();

    public string GetCardinalDescription()
    {
        return CardinalDescription;
    }
}

internal sealed class DummyFallbackEquipmentLineDataTarget
{
    public string FallbackText { get; set; } = "equipment fallback";
}

internal sealed class DummyEquipmentLineDataTarget
{
    public bool showCybernetics { get; set; }

    public DummyBodyPart? bodyPart { get; set; }

    public object? line { get; set; }
}

internal sealed class DummyEquipmentLineTarget
{
    public bool OriginalExecuted { get; private set; }

    public DummyCommandContext context = new DummyCommandContext();

    public DummyUITextSkin text = new DummyUITextSkin();

    public DummyUITextSkin itemText = new DummyUITextSkin();

    public DummyIconTarget icon = new DummyIconTarget();

    public object? tooltipGo;

    public object? tooltipCompareGo;

    public bool isDefaultBehavior;

    public void setData(object data)
    {
        OriginalExecuted = true;
        tooltipCompareGo = null;
        context.data = data;

        if (data is DummyFallbackEquipmentLineDataTarget fallback)
        {
            itemText.SetText(fallback.FallbackText);
            return;
        }

        if (data is not DummyEquipmentLineDataTarget lineData || lineData.bodyPart is null)
        {
            return;
        }

        lineData.line = this;
        tooltipGo = lineData.showCybernetics
            ? lineData.bodyPart.Cybernetics
            : lineData.bodyPart.Equipped ?? lineData.bodyPart.DefaultBehavior;
        var prefix = lineData.bodyPart.Primary ? "{{G|*}}" : string.Empty;
        var cardinalDescription = lineData.bodyPart.GetCardinalDescription();
        text.SetText(prefix + cardinalDescription);
        var item = lineData.showCybernetics
            ? lineData.bodyPart.Cybernetics
            : lineData.bodyPart.Equipped ?? lineData.bodyPart.DefaultBehavior;
        itemText.SetText(item?.DisplayName ?? "{{K|-}}");
        icon.FromRenderable(item?.RenderForUI("Equipment"));
    }
}

internal sealed class DummyHelpDataRowTarget
{
    public string Description { get; set; } = string.Empty;

    public string HelpText { get; set; } = string.Empty;

    public bool Collapsed { get; set; }
}

internal sealed class DummyFallbackHelpDataRowTarget
{
    public string FallbackText { get; set; } = "help fallback";
}

internal sealed class DummyHelpRowTarget
{
    public bool OriginalExecuted { get; private set; }

    public DummyUITextSkin categoryDescription = new DummyUITextSkin();

    public DummyUITextSkin description = new DummyUITextSkin();

    public DummyUITextSkin categoryExpander = new DummyUITextSkin();

    public List<string>? keysByLength;

    public Dictionary<string, string> formattedBindings = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Interact"] = "{{W|I}}",
        ["Highlight"] = "{{W|Alt}}",
    };

    public void setData(object data)
    {
        OriginalExecuted = true;
        if (data is DummyFallbackHelpDataRowTarget fallback)
        {
            description.text = fallback.FallbackText;
            description.Apply();
            return;
        }

        if (data is not DummyHelpDataRowTarget row)
        {
            return;
        }

        keysByLength ??= new List<string> { "Highlight", "Interact" };
        categoryDescription.text = "{{C|" + row.Description.ToUpperInvariant() + "}}";
        categoryDescription.Apply();

        var value = row.HelpText;
        if (value.Contains("~Highlight", StringComparison.Ordinal))
        {
            value = value.Replace("~Highlight", "{{W|Alt}}", StringComparison.Ordinal);
        }

        foreach (var key in keysByLength)
        {
            if (formattedBindings.TryGetValue(key, out var replacement))
            {
                value = value.Replace("~" + key, replacement, StringComparison.Ordinal);
            }
        }

        description.text = value;
        description.Apply();
        description.gameObject.SetActive(!row.Collapsed);
        categoryExpander.SetText(row.Collapsed ? "{{C|[+]}}" : "{{C|[-]}}");
    }
}

internal sealed class DummyKeybindCategoryRowTarget
{
    public string CategoryId { get; set; } = string.Empty;

    public string CategoryDescription { get; set; } = string.Empty;

    public bool Collapsed { get; set; }
}

internal sealed class DummyKeybindDataRowTarget
{
    public string CategoryId { get; set; } = string.Empty;

    public string KeyId { get; set; } = string.Empty;

    public string KeyDescription { get; set; } = string.Empty;

    public string SearchWords { get; set; } = string.Empty;

    public string? Bind1 { get; set; }

    public string? Bind2 { get; set; }

    public string? Bind3 { get; set; }

    public string? Bind4 { get; set; }
}

internal sealed class DummyFallbackKeybindRowDataTarget
{
    public string FallbackText { get; set; } = "keybind fallback";
}

internal sealed class DummyKeybindBox
{
    public string? boxText;

    public bool forceUpdate;

    public DummyActiveObject gameObject = new DummyActiveObject();
}

internal sealed class DummyKeybindRowTarget
{
    public bool OriginalExecuted { get; private set; }

    public DummyActiveObject bindingDisplay = new DummyActiveObject();

    public DummyActiveObject categoryDisplay = new DummyActiveObject();

    public DummyUITextSkin categoryDescription = new DummyUITextSkin();

    public DummyUITextSkin categoryExpander = new DummyUITextSkin();

    public DummyUITextSkin description = new DummyUITextSkin();

    public DummyKeybindBox box1 = new DummyKeybindBox();

    public DummyKeybindBox box2 = new DummyKeybindBox();

    public DummyKeybindBox box3 = new DummyKeybindBox();

    public DummyKeybindBox box4 = new DummyKeybindBox();

    public object? dataRow;

    public object? categoryRow;

    public bool NavigationContextRequested { get; private set; }

    public object GetNavigationContext()
    {
        NavigationContextRequested = true;
        return new object();
    }

    public void setData(object data)
    {
        OriginalExecuted = true;
        if (data is DummyFallbackKeybindRowDataTarget fallback)
        {
            description.text = fallback.FallbackText;
            description.Apply();
            return;
        }

        if (data is DummyKeybindDataRowTarget row)
        {
            categoryDisplay.SetActive(active: false);
            bindingDisplay.SetActive(active: true);
            categoryRow = null;
            dataRow = row;
            description.text = "{{C|" + row.KeyDescription + "}}";
            description.Apply();
            ApplyBindings(row);
        }
        else if (data is DummyKeybindCategoryRowTarget category)
        {
            categoryDisplay.SetActive(active: true);
            bindingDisplay.SetActive(active: false);
            categoryRow = category;
            dataRow = null;
            categoryDescription.text = "{{C|" + category.CategoryDescription.ToUpperInvariant() + "}}";
            categoryDescription.Apply();
            categoryExpander.SetText(category.Collapsed ? "{{C|[+]}}" : "{{C|[-]}}");
        }

        _ = GetNavigationContext();
    }

    private void ApplyBindings(DummyKeybindDataRowTarget row)
    {
        if (string.IsNullOrEmpty(row.Bind1))
        {
            box1.boxText = "{{K|None}}";
            box2.boxText = "{{K|None}}";
            box3.boxText = "{{K|None}}";
            box4.boxText = "{{K|None}}";
            box2.gameObject.SetActive(active: false);
            box3.gameObject.SetActive(active: false);
            box4.gameObject.SetActive(active: false);
        }
        else
        {
            box1.boxText = "{{w|" + row.Bind1 + "}}";
            box2.gameObject.SetActive(active: true);
            if (string.IsNullOrEmpty(row.Bind2))
            {
                box2.boxText = "{{K|None}}";
                box3.boxText = "{{K|None}}";
                box4.boxText = "{{K|None}}";
                box3.gameObject.SetActive(active: false);
                box4.gameObject.SetActive(active: false);
            }
            else
            {
                box2.boxText = "{{w|" + row.Bind2 + "}}";
                box3.gameObject.SetActive(active: true);
                if (string.IsNullOrEmpty(row.Bind3))
                {
                    box3.boxText = "{{K|None}}";
                    box4.boxText = "{{K|None}}";
                    box4.gameObject.SetActive(active: false);
                }
                else
                {
                    box3.boxText = "{{w|" + row.Bind3 + "}}";
                    box4.gameObject.SetActive(active: true);
                    box4.boxText = string.IsNullOrEmpty(row.Bind4) ? "{{K|None}}" : "{{w|" + row.Bind4 + "}}";
                }
            }
        }

        box1.forceUpdate = true;
        box2.forceUpdate = true;
        box3.forceUpdate = true;
        box4.forceUpdate = true;
    }
}

internal sealed class DummyKeybindsScreenTarget
{
    public bool OriginalExecuted { get; private set; }

    public static DummyMenuOption REMOVE_BIND = new DummyMenuOption("remove keybind", "CmdDelete", "Delete");

    public static DummyMenuOption RESTORE_DEFAULTS = new DummyMenuOption("restore defaults", "Restore", "R");

    public DummyUITextSkin inputTypeText = new DummyUITextSkin();

    public readonly List<object> menuItems = new List<object>();

    public bool KeyboardMode { get; set; } = true;

    public string CurrentControllerName { get; set; } = "Arcade Pad";

    public void QueryKeybinds()
    {
        OriginalExecuted = true;
        menuItems.Clear();
        if (KeyboardMode)
        {
            inputTypeText.SetText("{{C|Configuring Controller:}} {{c|Keyboard && Mouse}}");
        }
        else
        {
            inputTypeText.SetText("{{C|Configuring Controller:}} {{c|" + CurrentControllerName + "}}");
        }

        menuItems.Add(new DummyKeybindCategoryRowTarget
        {
            CategoryId = "General",
            CategoryDescription = "General",
        });
        menuItems.Add(new DummyKeybindDataRowTarget
        {
            CategoryId = "General",
            KeyId = "InteractNearby",
            KeyDescription = "Interact Nearby",
            SearchWords = "General Interact Nearby",
            Bind1 = "Ctrl+Space",
        });
    }
}
