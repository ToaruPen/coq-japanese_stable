using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using HarmonyLib;

namespace QudJP;

internal static class GameTypeResolver
{
    private static readonly ConcurrentDictionary<string, Type> FallbackResolutionCache =
        new ConcurrentDictionary<string, Type>(StringComparer.Ordinal);

    private static int fallbackScanCount;

    internal static Type? FindType(string fullTypeName, string simpleTypeName)
    {
        var byFullName = AccessTools.TypeByName(fullTypeName);
        if (byFullName is not null)
        {
            return byFullName;
        }

        var cacheKey = BuildCacheKey(fullTypeName, simpleTypeName);
        if (FallbackResolutionCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var resolved = FindTypeBySimpleName(fullTypeName, simpleTypeName, AppDomain.CurrentDomain.GetAssemblies());
        if (resolved is not null)
        {
            _ = FallbackResolutionCache.TryAdd(cacheKey, resolved);
        }

        return resolved;
    }

    internal static void ResetCacheForTests()
    {
        FallbackResolutionCache.Clear();
        Interlocked.Exchange(ref fallbackScanCount, 0);
    }

    internal static int FallbackScanCountForDiagnostics => Volatile.Read(ref fallbackScanCount);

    private static Type? FindTypeBySimpleName(string fullTypeName, string simpleTypeName, Assembly[] assemblies)
    {
        Interlocked.Increment(ref fallbackScanCount);

        Type? firstMatch = null;
        string? firstMatchAssembly = null;
        System.Collections.Generic.List<string>? allCandidates = null;

        for (var assemblyIndex = 0; assemblyIndex < assemblies.Length; assemblyIndex++)
        {
            Type[] types;
            try
            {
                types = assemblies[assemblyIndex].GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = Array.FindAll(ex.Types, static type => type is not null)!;
            }

            var asmName = assemblies[assemblyIndex].GetName().Name;
            for (var typeIndex = 0; typeIndex < types.Length; typeIndex++)
            {
                if (types[typeIndex].Name != simpleTypeName)
                {
                    continue;
                }

                if (firstMatch is null)
                {
                    firstMatch = types[typeIndex];
                    firstMatchAssembly = asmName;
                }
                else
                {
                    allCandidates ??= new System.Collections.Generic.List<string>
                    {
                        $"'{firstMatch.FullName}' (in '{firstMatchAssembly}')",
                    };
                    allCandidates.Add($"'{types[typeIndex].FullName}' (in '{asmName}')");
                }
            }
        }

        if (allCandidates is not null)
        {
            Trace.TraceWarning(
                "QudJP: Ambiguous simple name '{0}' resolved to {1} types: {2}. Returning null.",
                simpleTypeName,
                allCandidates.Count,
                string.Join(", ", allCandidates));
            return null;
        }

        if (firstMatch is null)
        {
            Trace.TraceWarning(
                "QudJP: GameTypeResolver failed to resolve type '{0}' (simple name: '{1}').",
                fullTypeName,
                simpleTypeName);
        }

        return firstMatch;
    }

    private static string BuildCacheKey(string fullTypeName, string simpleTypeName)
    {
        return fullTypeName + "\n" + simpleTypeName;
    }
}
