using System.Collections.Generic;
using System.Runtime.CompilerServices;
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

internal sealed class DummyXrlCoreStartMainMenuTarget
{
    public DummyLegacyBuffer Buffer { get; } = new();

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void _Start()
    {
        Buffer.Write("{{W|N}}ew game");
        Buffer.Write("New game");
        Buffer.Write("{{K|Continue}}");
        Buffer.Write("{{W|O}}ptions");
        Buffer.Write("{{W|H}}igh Scores");
        Buffer.Write("[{{W|?}}] Help");
        Buffer.Write("{{W|Q}}uit");
        Buffer.Write("{{W|R}}edeem Code");
        Buffer.Write("{{g|Dromad Edition}}");
        Buffer.Write("{{W|M}}ods");
        Buffer.WriteAt(40, 21, "{{y|-}} {{R|You have mods with errors.}}");
        Buffer.WriteAt(40, 21, "{{y|-}} {{R|You have mods with missing dependencies.}}");
        Buffer.WriteAt(40, 21, "{{y|-}} {{R|You have unapproved scripting mods.}}");
        Buffer.WriteAt(32, 0, "  {{C|Caves of Qud}}  ");
        Buffer.WriteAt(27, 24, " {{Y|Copyright ({{w|c}}) Freehold Games({{w|tm}})}} ");
    }
}

internal sealed class DummyMissileWeaponShowPickerTarget
{
    public DummyLegacyBuffer Buffer { get; } = new();

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void ShowPicker()
    {
        Buffer.Write("{{G|marked target}}");
        Buffer.Write(" {{W|M}} - mark target");
        Buffer.Write("{{K|A - Flattening Fire (not marked)}}");
        Buffer.Write("{{W|B}} - {{W|Suppressive Fire}}");
        Buffer.Write("{{K|C - Wounding Fire ({{C|2}} turns)}}");
        Buffer.Write("[{{W|M}}] Menu");
        Buffer.WriteAt(0, 0, "{{W|space}}-select | unlock ({{hotkey|F1}}) | Fire Missile Weapon");
    }
}

internal sealed class DummyInventoryScreenTarget
{
    public DummyLegacyBuffer Buffer { get; } = new();

    public int FooterLength { get; private set; }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Show()
    {
        var text = "< {{W|LB}} Character | Equipment {{W|RB}} >";
        FooterLength = DummyLegacyMarkup.StripFormatting(text).Length;
        Buffer.Write("[ {{W|Inventory}} ]");
        Buffer.Write(" {{W|B}} to exit ");
        Buffer.Write(" {{W|ESC}} or {{W|5}} to exit ");
        Buffer.Write(text);
        Buffer.Write("<more...>");
        Buffer.Write("<{{W|8}} to scroll up>");
        Buffer.Write("<...more>");
        Buffer.Write("<{{W|2}} to scroll down>");
        Buffer.Write("Total weight: {{Y|12 {{y|/}}  250 lbs.}}");
        Buffer.Write("{{K|, 2 items}}");
        Buffer.Write("{{K|, 1 item}}");
        Buffer.Write("[{{W|?}} view quick keys]");
        Buffer.Write("5 items hidden by filter");
    }
}

internal sealed class InventoryScreen
{
    public DummyLegacyBuffer Buffer { get; } = new();

    public void Show()
    {
        Buffer.Write(" {{W|B}} to exit ");
        Buffer.Write("< {{W|LB}} Character | Equipment {{W|RB}} >");
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
        Buffer.Write("[ {{W|Tinkering}} ]");
        Buffer.WriteAt(10, 0, " {{R|hostiles nearby}} ");
        Buffer.Write("{{Y|>}} {{W|Build}}    {{w|Mod}}");
        Buffer.Write("  {{w|Build}}  {{Y|>}} {{W|Mod}}");
        Buffer.Write("You don't have the Tinkering skill.");
        Buffer.Write("You don't have any modification schematics.");
        Buffer.Write("You don't have any moddable items.");
        Buffer.Write("You don't have any item schematics.");
        Buffer.Write(" {{W|A}} Mod Item  {{W|Y}} List Mods  {{W|B}} Exit ");
        Buffer.Write(" {{W|A}} Build  {{W|RT}}/{{W|LT}} Scroll  {{W|B}} Exit ");
        Buffer.WriteAt(53, 0, " Bit Locker ");
        Buffer.Write("{{R|A scrap power systems}}");
        Buffer.Write("{{G|\a scrap crystal}}");
        Buffer.Write("-or-");
        Buffer.Write(text);
    }
}

internal sealed class DummyAbilityManagerShowTarget
{
    public DummyLegacyBuffer Buffer { get; } = new();

    public int FooterLength { get; private set; }

    public void Show()
    {
        Buffer.Write("[ {{W|Manage Abilities}} ]");
        Buffer.Write(" {{W|T}}-custom ");
        Buffer.Write(" {{W|ESC}}-exit ");
        FooterLength = DummyLegacyMarkup.StripFormatting("{{W|Maneuvers}}").Length;
        Buffer.Write("{{W|Maneuvers}}");
        Buffer.Write("  a) Sprint [{{W|attack}}] {{Y|<{{w|S}}>}}");
        Buffer.Write("{{K|  b) Teleport [attack] [disabled]}}");
        Buffer.Write("  {{K|c}}) Sprint [{{C|7}} turn cooldown, astrally tethered] {{K|[{{g|Toggled on}}]}}");
        Buffer.Write("Cooldown: {{C|7}} rounds");
        Buffer.Write("[ {{W|Enter}}-Use Ability {{W|Ins}}-Map key {{W|Del}}-unbind {{W|Up}}/{{W|Down}}-Change Order ]");
        Buffer.Write("{{W|<More...>}}");
        Buffer.Write("\u0001既訳能力");
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
        Buffer.Write("[{{C|100}}sp] {{w|Tinkering}}");
        Buffer.Write("{{g|Tinker II}} [{{C|200}}sp] {{C|23}} {{R|Intelligence}}");
        Buffer.Write(", {{G|Tinker I}}");
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
