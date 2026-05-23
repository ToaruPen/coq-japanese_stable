using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace QudJP.Tests.DummyTargets;

internal sealed class DummyTradeScreenUiTarget
{
    public static DummyMenuOption SET_FILTER = new("Filter", "CmdFilter", "Filter");
    public static DummyMenuOption TOGGLE_SORT = new("sort: a-z/by class", "Toggle", "Toggle Sort");
    public static DummyMenuOption OFFER_TRADE = new("offer", "CmdTradeOffer", "Offer");
    public static DummyMenuOption ADD_ONE = new("add one", "CmdTradeAdd", "Add One");
    public static DummyMenuOption REMOVE_ONE = new("remove one", "CmdTradeRemove", "Remove One");
    public static DummyMenuOption TOGGLE_ALL = new("toggle all", "CmdTradeToggleAll", "Toggle All");
    public static DummyMenuOption VENDOR_ACTIONS = new("vendor actions", "CmdVendorActions", "Vendor Actions");

    public static List<DummyMenuOption> defaultMenuOptions = new()
    {
        new DummyMenuOption("Close Menu", "Cancel"),
        new DummyMenuOption("navigate", "NavigationXYAxis"),
        TOGGLE_SORT,
        SET_FILTER,
    };

    public static List<DummyMenuOption> getItemMenuOptions = new()
    {
        new DummyMenuOption("Close Menu", "Cancel"),
        new DummyMenuOption("navigate", "NavigationXYAxis"),
        TOGGLE_SORT,
        SET_FILTER,
        VENDOR_ACTIONS,
        ADD_ONE,
        REMOVE_ONE,
        TOGGLE_ALL,
        OFFER_TRADE,
    };

    public List<DummyMenuOption> renderedDefaultMenuOptions = new();
    public List<DummyMenuOption> renderedGetItemMenuOptions = new();

    public static void ResetDefaults()
    {
        SET_FILTER = new DummyMenuOption("Filter", "CmdFilter", "Filter");
        TOGGLE_SORT = new DummyMenuOption("sort: a-z/by class", "Toggle", "Toggle Sort");
        OFFER_TRADE = new DummyMenuOption("offer", "CmdTradeOffer", "Offer");
        ADD_ONE = new DummyMenuOption("add one", "CmdTradeAdd", "Add One");
        REMOVE_ONE = new DummyMenuOption("remove one", "CmdTradeRemove", "Remove One");
        TOGGLE_ALL = new DummyMenuOption("toggle all", "CmdTradeToggleAll", "Toggle All");
        VENDOR_ACTIONS = new DummyMenuOption("vendor actions", "CmdVendorActions", "Vendor Actions");
        defaultMenuOptions = new List<DummyMenuOption>
        {
            new DummyMenuOption("Close Menu", "Cancel"),
            new DummyMenuOption("navigate", "NavigationXYAxis"),
            TOGGLE_SORT,
            SET_FILTER,
        };
        getItemMenuOptions = new List<DummyMenuOption>
        {
            new DummyMenuOption("Close Menu", "Cancel"),
            new DummyMenuOption("navigate", "NavigationXYAxis"),
            TOGGLE_SORT,
            SET_FILTER,
            VENDOR_ACTIONS,
            ADD_ONE,
            REMOVE_ONE,
            TOGGLE_ALL,
            OFFER_TRADE,
        };
    }

    public void UpdateMenuBars()
    {
        renderedDefaultMenuOptions = defaultMenuOptions
            .Select(option => new DummyMenuOption(option.Description, option.InputCommand, option.KeyDescription))
            .ToList();
        renderedGetItemMenuOptions = getItemMenuOptions
            .Select(option => new DummyMenuOption(option.Description, option.InputCommand, option.KeyDescription))
            .ToList();
    }
}

internal static class DummyPopupAskNumberTarget
{
    public static string? LastMessage { get; private set; }

    public static void Reset()
    {
        LastMessage = null;
    }

    public static Task<int?> AskNumberAsync(string message, int start = 0, int min = 0, int max = int.MaxValue, string restrictChars = "", bool pushView = false)
    {
        _ = min;
        _ = max;
        _ = restrictChars;
        _ = pushView;
        LastMessage = message;
        return Task.FromResult<int?>(start);
    }
}

internal static class DummyLegacyTradeUiTarget
{
    public static string sReadout = string.Empty;

    public static void Reset()
    {
        sReadout = string.Empty;
    }

    public static void UpdateTotals(double[] totals, int[] weight, List<string>[] objects, int[][] numberSelected)
    {
        _ = totals;
        _ = weight;
        _ = objects;
        _ = numberSelected;
        sReadout = " {{C|42}} drams <-> {{C|10}} drams ÄÄ {{W|$50}} ";
    }
}

internal sealed class DummyLegacyTradeUiScreenTarget
{
    public DummyLegacyBuffer Buffer { get; } = new();

    public int AddRemoveLength { get; private set; }

    public int OfferLength { get; private set; }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void ShowTradeScreen()
    {
        var addRemove = "[{{W|A}}/{{W|R}} Add/Remove]";
        AddRemoveLength = DummyLegacyMarkup.StripFormatting(addRemove).Length;
        var offer = "[{{W|O}} Offer]";
        OfferLength = DummyLegacyMarkup.StripFormatting(offer).Length;

        Buffer.Write("[ {{W|dromad's inventory}} ]");
        Buffer.Write("[ {{W|Your inventory}} ]");
        Buffer.Write(" {{G|[owned by you]}}");
        Buffer.Write("{{R|[ owned by someone else ]}}");
        Buffer.Write("[{{W|ESC}} Exit]");
        Buffer.Write(addRemove);
        Buffer.Write("[{{W|0-9}} Pick]");
        Buffer.Write(offer);
        Buffer.Write("[{{W|H}} Haggle]");
        Buffer.Write("[{{W|T}} Transfer]");
        Buffer.Write("[{{W|V}} Actions]");
        Buffer.Write(" {{C|42}} drams <-> {{C|10}} drams ");
        Buffer.Write(" {{K|12/250 lbs.}} ");
    }
}
