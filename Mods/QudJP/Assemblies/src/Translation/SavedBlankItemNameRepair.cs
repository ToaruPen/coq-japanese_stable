using System;
using System.Collections.Generic;

namespace QudJP;

internal static class SavedBlankItemNameRepair
{
    // Reported mandible, card and key names corrected by 2d9826fc (#841).
    // Exact old values only: do not replace custom names or unrelated blank objects.
    private static readonly Dictionary<string, (string Legacy, string Repaired)> Repairs = new(StringComparer.Ordinal)
    {
        ["SalthopperMandible"] = ("{{G|}}", "{{G|ソルトホッパーの大顎}}"),
        ["Red Security Card"] = ("{{r|}}", "{{r|労働者用セキュリティカード}}"),
        ["Green Security Card"] = ("{{G|}}", "{{G|緊急サービス用セキュリティカード}}"),
        ["Blue Security Card"] = ("{{B|}}", "{{B|法執行機関用セキュリティカード}}"),
        ["Purple Security Card"] = ("{{M|}}", "{{M|軍用セキュリティカード}}"),
        ["Copper Trollking Key"] = ("{{w|}}", "{{w|青銅}}の鍵"),
        ["Silver Trollking Key"] = ("{{silvery|}}", "{{silvery|銀}}の鍵"),
        ["BarathrumKey"] = ("{{c|}}", "{{c|クローム}}の鍵"),
        ["GritGateGridKey"] = ("{{c|}}", "{{c|クローム}}のセキュリティカード"),
        ["CrystalKey"] = ("{{m|}}", "{{m|水晶}}の鍵"),
    };

    internal static bool IsKnownBlueprint(string? blueprint)
    {
        return blueprint is not null && Repairs.ContainsKey(blueprint);
    }

    internal static string? Repair(string? blueprint, string? displayName)
    {
        return blueprint is not null
            && Repairs.TryGetValue(blueprint, out var repair)
            && string.Equals(displayName, repair.Legacy, StringComparison.Ordinal)
                ? repair.Repaired
                : displayName;
    }
}
