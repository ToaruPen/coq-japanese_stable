using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class GetDisplayNameProcessPatch
{
    private const string TargetTypeName = "XRL.World.GetDisplayNameEvent";

    private static Type? cachedBuilderType;
    private static FieldInfo? cachedPrimaryBaseField;
    private static FieldInfo? cachedLastAddedField;
    private static bool warnedMissingBuilderFields;

#pragma warning disable CA2249 // net48 lacks string.Contains(char)
    private static bool ContainsChar(string value, char ch) => value.IndexOf(ch) >= 0;
#pragma warning restore CA2249

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (gameObjectType is not null)
        {
            var method = AccessTools.Method(TargetTypeName + ":ProcessFor", new[] { gameObjectType, typeof(bool) });
            if (method is not null)
            {
                return method;
            }
        }

        var targetType = AccessTools.TypeByName(TargetTypeName);
        if (targetType is null)
        {
            Trace.TraceError("QudJP: Failed to resolve GetDisplayNameEvent.ProcessFor(GameObject,bool). Patch will not apply.");
            return null;
        }

        var methods = AccessTools.GetDeclaredMethods(targetType);
        for (var index = 0; index < methods.Count; index++)
        {
            var candidate = methods[index];
            if (!string.Equals(candidate.Name, "ProcessFor", StringComparison.Ordinal)
                || candidate.ReturnType != typeof(string))
            {
                continue;
            }

            var parameters = candidate.GetParameters();
            if (parameters.Length == 2 && parameters[1].ParameterType == typeof(bool))
            {
                return candidate;
            }
        }

        Trace.TraceError("QudJP: Failed to resolve GetDisplayNameEvent.ProcessFor(GameObject,bool). Patch will not apply.");
        return null;
    }

    public static void Postfix(ref string __result, object? ___DB)
    {
        try
        {
            if (string.IsNullOrEmpty(__result))
            {
                return;
            }

            if (TryHandleFigurineFamily(__result, ___DB))
            {
                return;
            }

            if (TryHandleWarlordFamily(__result, ___DB))
            {
                return;
            }

            if (TryHandleLegendaryFamily(ref __result, ___DB))
            {
                return;
            }

            var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(__result, nameof(GetDisplayNameProcessPatch));
            if (!string.Equals(translated, __result, StringComparison.Ordinal))
            {
                __result = translated;
                return;
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: GetDisplayNameProcessPatch.Postfix failed: {0}", ex);
        }
    }

    private static (string? primaryBase, string? lastAdded) GetBuilderFields(object descriptionBuilder)
    {
        var dbType = descriptionBuilder.GetType();
        if (dbType != cachedBuilderType)
        {
            cachedBuilderType = dbType;
            cachedPrimaryBaseField = AccessTools.Field(dbType, "PrimaryBase");
            cachedLastAddedField = AccessTools.Field(dbType, "LastAdded");

            if (!warnedMissingBuilderFields
                && (cachedPrimaryBaseField is null || cachedLastAddedField is null))
            {
                warnedMissingBuilderFields = true;
                Trace.TraceWarning(
                    "QudJP: Builder type '{0}' missing field(s): PrimaryBase={1}, LastAdded={2}",
                    dbType.FullName,
                    cachedPrimaryBaseField is null ? "not found" : "ok",
                    cachedLastAddedField is null ? "not found" : "ok");
            }
        }

        var primaryBase = cachedPrimaryBaseField?.GetValue(descriptionBuilder) as string;
        var lastAdded = cachedLastAddedField?.GetValue(descriptionBuilder) as string;
        return (primaryBase, lastAdded);
    }

    private static bool TryHandleFigurineFamily(string current, object? descriptionBuilder)
    {
        if (descriptionBuilder is null || ContainsChar(current, ','))
        {
            return false;
        }

        var (primaryBase, lastAdded) = GetBuilderFields(descriptionBuilder);
        if (string.IsNullOrEmpty(primaryBase)
            || !string.Equals(lastAdded, "のフィギュリン", StringComparison.Ordinal))
        {
            return false;
        }

        // Match either exact "{base} のフィギュリン" or space-delimited "{material} {base} のフィギュリン"
        var baseName = primaryBase + " のフィギュリン";
        if (string.Equals(current, baseName, StringComparison.Ordinal))
        {
            return true;
        }

        return current.Length > baseName.Length
            && current[current.Length - baseName.Length - 1] == ' '
            && current.EndsWith(baseName, StringComparison.Ordinal);
    }

    private static bool TryHandleWarlordFamily(string current, object? descriptionBuilder)
    {
        if (descriptionBuilder is null || ContainsChar(current, ',') || ContainsChar(current, ' '))
        {
            return false;
        }

        var (primaryBase, lastAdded) = GetBuilderFields(descriptionBuilder);
        if (string.IsNullOrEmpty(primaryBase)
            || !string.Equals(lastAdded, "軍主", StringComparison.Ordinal))
        {
            return false;
        }

        var expected = primaryBase + "の軍主";
        return string.Equals(current, expected, StringComparison.Ordinal);
    }

    private static bool TryHandleLegendaryFamily(ref string current, object? descriptionBuilder)
    {
        if (descriptionBuilder is null)
        {
            return false;
        }

        var (primaryBase, lastAdded) = GetBuilderFields(descriptionBuilder);
        if (!string.Equals(lastAdded, "legendary", StringComparison.Ordinal))
        {
            return false;
        }

        if (!TryTransformLegendaryDisplayName(current, primaryBase, out var transformed))
        {
            return false;
        }

        current = transformed;
        return true;
    }

    internal static bool TryTransformLegendaryDisplayName(string current, string? primaryBase, out string transformed)
    {
        const string marker = ", legendary ";

        if (string.IsNullOrEmpty(primaryBase) || string.IsNullOrEmpty(current))
        {
            transformed = current;
            return false;
        }

        var markerIndex = current.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            transformed = current;
            return false;
        }

        var expectedSuffix = marker + primaryBase;
        if (!current.EndsWith(expectedSuffix, StringComparison.Ordinal))
        {
            transformed = current;
            return false;
        }

        transformed = current.Remove(markerIndex) + "、伝説の" + primaryBase;
        return true;
    }
}
