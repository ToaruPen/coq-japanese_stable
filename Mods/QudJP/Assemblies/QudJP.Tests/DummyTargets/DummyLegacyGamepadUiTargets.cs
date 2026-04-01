using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace QudJP.Tests.DummyTargets;

internal sealed class DummyLegacyBuffer
{
    public List<string> Writes { get; } = new();

    public void Write(string text)
    {
        Writes.Add(text);
    }

    public void WriteAt(int left, int top, string text)
    {
        _ = left;
        _ = top;
        Writes.Add(text);
    }
}

internal static class DummyLegacyMarkup
{
    private static readonly Regex ColorMarkupPattern =
        new Regex(@"\{\{[^|]+\|(?<text>.*?)\}\}", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static string StripFormatting(string text)
    {
        return ColorMarkupPattern.Replace(text, match => match.Groups["text"].Value);
    }
}

internal sealed class DummyXrlManualTarget
{
    public DummyLegacyBuffer Buffer { get; } = new();

    public void RenderIndex(int scrollPosition)
    {
        _ = scrollPosition;
        Buffer.Write(" [{{W|A}}] Select Topic ");
        Buffer.Write(" [{{W|B}}] Exit Help ");
    }
}

internal sealed class DummyInventoryScreenTarget
{
    public DummyLegacyBuffer Buffer { get; } = new();

    public int FooterLength { get; private set; }

    public void Show()
    {
        var text = "< {{W|LB}} Character | Equipment {{W|RB}} >";
        FooterLength = DummyLegacyMarkup.StripFormatting(text).Length;
        Buffer.Write(" {{W|B}} to exit ");
        Buffer.Write(text);
        Buffer.Write("<more...>");
        Buffer.Write("<...more>");
        Buffer.Write("5 items hidden by filter");
    }
}

internal sealed class DummyStatusScreenTarget
{
    public DummyLegacyBuffer Buffer { get; } = new();

    public int FooterLength { get; private set; }

    public void Show()
    {
        var text = "< {{W|LB}} Skills | Inventory {{W|RB}} >";
        FooterLength = DummyLegacyMarkup.StripFormatting(text).Length;
        Buffer.Write(" {{W|B}} to exit ");
        Buffer.Write(text);
        Buffer.WriteAt(4, 24, " [{{W|A}}] Raise");
        Buffer.Write("Buy a new random mutation for 4 MP");
    }
}

internal sealed class DummyJournalScreenTarget
{
    public DummyLegacyBuffer Buffer { get; } = new();

    public int FooterLength { get; private set; }

    public void Show()
    {
        var text = "< {{W|LB}} Quests | Tinkering {{W|RB}} >";
        FooterLength = DummyLegacyMarkup.StripFormatting(text).Length;
        Buffer.Write(" {{W|B}} to exit ");
        Buffer.Write(text);
        Buffer.Write(" {{W|X}} - Delete ");
        Buffer.Write(" {{W|Y}} Add {{W|X}} - Delete ");
    }
}

internal sealed class DummyTinkeringScreenTarget
{
    public DummyLegacyBuffer Buffer { get; } = new();

    public int FooterLength { get; private set; }

    public void Show(object? go, object? forModdingOf = null, object? fromEvent = null)
    {
        _ = go;
        _ = forModdingOf;
        _ = fromEvent;

        var text = "< {{W|LB}} Journal | Skills {{W|RB}} >";
        FooterLength = DummyLegacyMarkup.StripFormatting(text).Length;
        Buffer.Write("{{Y|>}} {{W|Build}}    {{w|Mod}}");
        Buffer.Write(" {{W|A}} Mod Item  {{W|Y}} List Mods  {{W|B}} Exit ");
        Buffer.Write(" {{W|A}} Build  {{W|RT}}/{{W|LT}} Scroll  {{W|B}} Exit ");
        Buffer.Write(text);
    }
}

internal sealed class DummyLegacyQuestLogScreenTarget
{
    public DummyLegacyBuffer Buffer { get; } = new();

    public int FooterLength { get; private set; }

    public void Show()
    {
        var text = "< {{W|LB}} Factions | Journal {{W|RB}} >";
        FooterLength = DummyLegacyMarkup.StripFormatting(text).Length;
        Buffer.Write(" {{W|B}} to exit ");
        Buffer.Write(text);
    }
}

internal sealed class DummyFactionsScreenTarget
{
    public DummyLegacyBuffer Buffer { get; } = new();

    public int FooterLength { get; private set; }

    public void Show()
    {
        var text = "< {{W|LB}} Equipment | Quests {{W|RB}} >";
        FooterLength = DummyLegacyMarkup.StripFormatting(text).Length;
        Buffer.Write(" {{W|B}} to exit ");
        Buffer.Write(text);
    }
}

internal sealed class DummyLegacySkillsAndPowersScreenTarget
{
    public DummyLegacyBuffer Buffer { get; } = new();

    public int FooterLength { get; private set; }

    public void Show()
    {
        var text = "< {{W|LB}} Tinkering | Character {{W|RB}} >";
        FooterLength = DummyLegacyMarkup.StripFormatting(text).Length;
        Buffer.Write(" {{W|B}} to exit ");
        Buffer.Write(text);
        Buffer.Write(" [{{W|A}}-Buy] ");
    }
}

internal sealed class DummyEquipmentScreenTarget
{
    public DummyLegacyBuffer Buffer { get; } = new();

    public int FooterLength { get; private set; }

    public void Show()
    {
        var text = "< {{W|LB}} Inventory | Factions {{W|RB}} >";
        FooterLength = DummyLegacyMarkup.StripFormatting(text).Length;
        Buffer.Write(" {{W|B}} to exit ");
        Buffer.Write(text);
        Buffer.Write("[{{W|Y - Set primary limb}}]");
        Buffer.Write("[{{K|Y - Set primary limb}}]");
    }
}
