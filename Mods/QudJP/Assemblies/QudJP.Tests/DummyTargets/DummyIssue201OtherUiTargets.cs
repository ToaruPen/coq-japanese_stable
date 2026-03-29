using System;
using System.Collections;
using System.Collections.Generic;

namespace QudJP.Tests.DummyTargets;

internal sealed class DummyBookTarget
{
    public string Title { get; set; } = string.Empty;
}

internal static class BookUI
{
    public static IDictionary Books = new Dictionary<string, object>(StringComparer.Ordinal);

    public static void Reset()
    {
        Books = new Dictionary<string, object>(StringComparer.Ordinal);
    }
}

internal sealed class DummyBookLineDataTarget
{
    public string text { get; set; } = string.Empty;
}

internal sealed class DummyFallbackBookLineDataTarget
{
    public string FallbackText { get; set; } = "book line fallback";
}

internal sealed class DummyBookLineTarget
{
    public bool OriginalExecuted { get; private set; }

    public DummyStatusContext context = new DummyStatusContext();

    public DummyUITextSkin text = new DummyUITextSkin();

    public void setData(object data)
    {
        OriginalExecuted = true;
        context.data = data;
        if (data is DummyFallbackBookLineDataTarget fallback)
        {
            text.SetText(fallback.FallbackText);
            return;
        }

        if (data is DummyBookLineDataTarget lineData)
        {
            text.SetText(lineData.text);
        }
    }
}

internal sealed class DummyBookScreenTarget
{
    public static DummyMenuOption PREV_PAGE = new DummyMenuOption("Previous Page", "UI:Navigate/left", "Previous Page");

    public static DummyMenuOption NEXT_PAGE = new DummyMenuOption("Next Page", "UI:Navigate/right", "Next Page");

    public static List<DummyMenuOption> getItemMenuOptions = new List<DummyMenuOption>
    {
        PREV_PAGE,
        NEXT_PAGE,
        new DummyMenuOption("Close book", "Cancel"),
    };

    public DummyUITextSkin titleText = new DummyUITextSkin();

    public bool OriginalExecuted { get; private set; }

    public static void ResetStaticMenuOptions()
    {
        PREV_PAGE = new DummyMenuOption("Previous Page", "UI:Navigate/left", "Previous Page");
        NEXT_PAGE = new DummyMenuOption("Next Page", "UI:Navigate/right", "Next Page");
        getItemMenuOptions = new List<DummyMenuOption>
        {
            PREV_PAGE,
            NEXT_PAGE,
            new DummyMenuOption("Close book", "Cancel"),
        };
    }

    public void showScreen(DummyBookTarget book, string sound = "book.wav", Action<int>? onShowPage = null, Action<int>? afterShowPage = null)
    {
        _ = sound;
        _ = onShowPage;
        _ = afterShowPage;
        OriginalExecuted = true;
        titleText.SetText(book.Title);
    }

    public void showScreen(string bookId, string sound = "book.wav", Action<int>? onShowPage = null, Action<int>? afterShowPage = null)
    {
        _ = sound;
        _ = onShowPage;
        _ = afterShowPage;
        OriginalExecuted = true;
        if (BookUI.Books.Contains(bookId) && BookUI.Books[bookId] is DummyBookTarget book)
        {
            titleText.SetText(book.Title);
        }
    }
}

internal sealed class DummyPadding
{
    public int left { get; set; }
}

internal sealed class DummyLayoutGroup
{
    public DummyPadding padding = new DummyPadding();
}

internal sealed class DummyJournalStatusScreenTarget
{
    public static string NO_ENTRIES_TEXT = " No entries found.";

    public int CurrentCategory { get; set; } = 2;
}

internal sealed class DummyJournalLineDataTarget
{
    public bool category { get; set; }

    public bool categoryExpanded { get; set; }

    public string categoryName { get; set; } = string.Empty;

    public object? entry { get; set; }

    public object? renderable { get; set; }

    public DummyJournalStatusScreenTarget? screen { get; set; }
}

internal sealed class DummyFallbackJournalLineDataTarget
{
    public string FallbackText { get; set; } = "journal fallback";
}

internal sealed class DummyJournalRecipeTarget
{
    public string DisplayName { get; set; } = string.Empty;

    public string Ingredients { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string GetDisplayName() => DisplayName;

    public string GetIngredients() => Ingredients;

    public string GetDescription() => Description;
}

internal sealed class DummyJournalRecipeNoteEntry
{
    public DummyJournalRecipeTarget? Recipe { get; set; }
}

internal sealed class DummyJournalObservationEntry
{
    public string Text { get; set; } = string.Empty;

    public bool Tradable { get; set; }

    public bool TombPropaganda { get; set; }

    public string GetDisplayText() => Text;

    public bool Has(string key) => TombPropaganda && string.Equals(key, "sultanTombPropaganda", StringComparison.Ordinal);
}

internal sealed class DummyJournalMapNoteEntry
{
    public string Text { get; set; } = string.Empty;

    public bool Tradable { get; set; }

    public bool Tracked { get; set; }

    public string Category { get; set; } = "Merchants";

    public bool TombPropaganda { get; set; }

    public string GetDisplayText() => Text;

    public bool Has(string key) => TombPropaganda && string.Equals(key, "sultanTombPropaganda", StringComparison.Ordinal);
}

internal sealed class DummyJournalLineTarget
{
    public bool OriginalExecuted { get; private set; }

    public DummyStatusContext context = new DummyStatusContext();

    public DummyUITextSkin text = new DummyUITextSkin();

    public DummyActiveObject imageContainer = new DummyActiveObject();

    public DummyUiThreeColorProperties image = new DummyUiThreeColorProperties();

    public DummyActiveObject headerContainer = new DummyActiveObject();

    public DummyUITextSkin headerText = new DummyUITextSkin();

    public DummyLayoutGroup layoutGroup = new DummyLayoutGroup();

    public object? screen;

    public void setData(object data)
    {
        OriginalExecuted = true;
        context.data = data;
        if (data is DummyFallbackJournalLineDataTarget fallback)
        {
            text.SetText(fallback.FallbackText);
            return;
        }

        if (data is DummyJournalLineDataTarget lineData)
        {
            screen = lineData.screen;
            text.SetText(lineData.categoryName);
        }
    }
}

internal sealed class DummyUiIconWithGameObject
{
    public DummyActiveObject gameObject = new DummyActiveObject();

    public object? LastRenderable { get; private set; }

    public void FromRenderable(object? renderable)
    {
        LastRenderable = renderable;
    }
}

internal sealed class DummyAbilityEntryTarget
{
    public string DisplayName { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    public bool IsAttack { get; set; }

    public bool IsRealityDistortionBased { get; set; }

    public int Cooldown { get; set; }

    public int CooldownRounds { get; set; }

    public bool Toggleable { get; set; }

    public bool ToggleState { get; set; }

    public object GetUITile() => new DummyRenderable(DisplayName);
}

internal sealed class DummyAbilityManagerLineDataTarget
{
    public string? category { get; set; }

    public bool collapsed { get; set; }

    public DummyAbilityEntryTarget? ability { get; set; }

    public char quickKey { get; set; } = 'a';

    public bool realityIsWeak { get; set; }

    public string? hotkeyDescription { get; set; }
}

internal sealed class DummyFallbackAbilityManagerLineDataTarget
{
    public string FallbackText { get; set; } = "ability fallback";
}

internal sealed class DummyAbilityManagerLineTarget
{
    public static DummyMenuOption MOVE_DOWN = new DummyMenuOption("Move Down", "V Positive");

    public static DummyMenuOption MOVE_UP = new DummyMenuOption("Move Up", "V Negative");

    public static DummyMenuOption BIND_KEY = new DummyMenuOption("Bind Key", "CmdInsert");

    public static DummyMenuOption UNBIND_KEY = new DummyMenuOption("Unbind Key", "CmdDelete");

    public bool OriginalExecuted { get; private set; }

    public DummyStatusContext context = new DummyStatusContext();

    public DummyUITextSkin text = new DummyUITextSkin();

    public DummyUiIconWithGameObject icon = new DummyUiIconWithGameObject();

    public static void ResetStaticMenuOptions()
    {
        MOVE_DOWN = new DummyMenuOption("Move Down", "V Positive");
        MOVE_UP = new DummyMenuOption("Move Up", "V Negative");
        BIND_KEY = new DummyMenuOption("Bind Key", "CmdInsert");
        UNBIND_KEY = new DummyMenuOption("Unbind Key", "CmdDelete");
    }

    public void setData(object data)
    {
        OriginalExecuted = true;
        context.data = data;
        if (data is DummyFallbackAbilityManagerLineDataTarget fallback)
        {
            text.SetText(fallback.FallbackText);
            return;
        }

        if (data is not DummyAbilityManagerLineDataTarget lineData)
        {
            return;
        }

        if (lineData.category is not null)
        {
            text.SetText("[+] " + lineData.category);
            icon.gameObject.SetActive(false);
            return;
        }

        text.SetText(lineData.quickKey + ") " + (lineData.ability?.DisplayName ?? string.Empty));
        icon.gameObject.SetActive(true);
        icon.FromRenderable(lineData.ability?.GetUITile());
    }
}

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

internal sealed class DummyFilterBarCategoryButtonTarget
{
    public bool OriginalExecuted { get; private set; }

    public string Tooltip { get; set; } = string.Empty;

    public DummyUITextSkin tooltipText = new DummyUITextSkin();

    public DummyUITextSkin text = new DummyUITextSkin();

    public string category = string.Empty;

    public void SetCategory(string category, string? tooltip = null)
    {
        OriginalExecuted = true;
        this.category = category;
        Tooltip = tooltip ?? category;
        tooltipText.SetText(Tooltip);
        text.SetText(category == "*All" ? "ALL" : category);
    }
}

internal sealed class DummyCyberneticsTerminalScreenTarget
{
    public bool OriginalExecuted { get; private set; }

    public string FooterText { get; set; } = string.Empty;

    public DummyUITextSkin footerTextSkin = new DummyUITextSkin();

    public List<DummyMenuOption> keyMenuOptions = new List<DummyMenuOption>();

    public void Show()
    {
        OriginalExecuted = true;
        footerTextSkin.SetText(FooterText);
        keyMenuOptions.Clear();
        keyMenuOptions.Add(new DummyMenuOption("navigate", "NavigationXYAxis"));
        keyMenuOptions.Add(new DummyMenuOption("accept", "Accept"));
        keyMenuOptions.Add(new DummyMenuOption("quit", "Cancel"));
    }
}
