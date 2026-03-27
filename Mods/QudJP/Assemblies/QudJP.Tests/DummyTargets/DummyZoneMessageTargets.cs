namespace QudJP.Tests.DummyTargets;

using System.Collections.Generic;

internal sealed class DummyZoneDisplayNameTarget
{
    public string Result { get; set; } = string.Empty;

    public string GetZoneDisplayName(string zoneId, int zPos, object zoneBlueprint, bool withIndefiniteArticle = false, bool withDefiniteArticle = false, bool withStratum = true, bool mutate = true)
    {
        _ = zoneId;
        _ = zPos;
        _ = zoneBlueprint;
        _ = withIndefiniteArticle;
        _ = withDefiniteArticle;
        _ = withStratum;
        _ = mutate;
        return Result;
    }

    public string GetZoneDisplayName(string zoneId, string worldId, int wXPos, int wYPos, int xPos, int yPos, int zPos, bool withIndefiniteArticle = false, bool withDefiniteArticle = false, bool withStratum = true, bool mutate = true)
    {
        _ = zoneId;
        _ = worldId;
        _ = wXPos;
        _ = wYPos;
        _ = xPos;
        _ = yPos;
        _ = zPos;
        _ = withIndefiniteArticle;
        _ = withDefiniteArticle;
        _ = withStratum;
        _ = mutate;
        return Result;
    }

    public string GetZoneDisplayName(string zoneId, bool withIndefiniteArticle = false, bool withDefiniteArticle = false, bool withStratum = true, bool mutate = true)
    {
        _ = zoneId;
        _ = withIndefiniteArticle;
        _ = withDefiniteArticle;
        _ = withStratum;
        _ = mutate;
        return Result;
    }
}

internal static class DummyZoneWorldFactory
{
    public static string WorldId { get; set; } = "JoppaWorld";

    public static string ZoneId { get; set; } = "JoppaWorld.11.22.1.1.10";

    public static string ZoneDisplayNameValue { get; set; } = "Joppa";

    public static string ZoneDisplayName(string zoneId)
    {
        _ = zoneId;
        return ZoneDisplayNameValue;
    }

    public static void Reset()
    {
        WorldId = "JoppaWorld";
        ZoneId = "JoppaWorld.11.22.1.1.10";
        ZoneDisplayNameValue = "Joppa";
    }
}

internal static class DummyZoneCalendar
{
    public static string TimeValue { get; set; } = "06:00";

    public static string GetTime()
    {
        return TimeValue;
    }

    public static void Reset()
    {
        TimeValue = "06:00";
    }
}

internal static class DummyZoneMessageDispatcher
{
    public static void AddPlayerMessage(string message, string? color = null, bool capitalize = true)
    {
        DummyMessageQueue.AddPlayerMessage(message, color, capitalize);
    }

    public static void AddPlayerMessage(string message, char color, bool capitalize = true)
    {
        DummyMessageQueue.AddPlayerMessage(message, color.ToString(), capitalize);
    }
}

internal sealed class DummyPhysicsEnterCellTarget
{
    public readonly List<string> PassingBy = new List<string>();

    public void EnterCell()
    {
        DummyZoneMessageDispatcher.AddPlayerMessage("You pass by " + DummyGrammar.MakeAndList(PassingBy) + ".");
    }
}

internal sealed class DummyZoneManagerSetActiveZoneTarget
{
    public int CallCount { get; private set; }

    public void SetActiveZone()
    {
        CallCount++;
        if (DummyZoneWorldFactory.WorldId == "JoppaWorld")
        {
            DummyZoneMessageDispatcher.AddPlayerMessage(DummyZoneWorldFactory.ZoneDisplayName(DummyZoneWorldFactory.ZoneId) + ", " + DummyZoneCalendar.GetTime(), 'C');
        }
        else
        {
            DummyZoneMessageDispatcher.AddPlayerMessage(DummyZoneWorldFactory.ZoneDisplayName(DummyZoneWorldFactory.ZoneId), 'C');
        }
    }
}
