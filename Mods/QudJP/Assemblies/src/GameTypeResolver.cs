using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP;

internal static class GameTypeResolver
{
    internal static Type? FindType(string fullTypeName, string simpleTypeName)
    {
        var byFullName = AccessTools.TypeByName(fullTypeName);
        if (byFullName is not null)
        {
            return byFullName;
        }

        Type? firstMatch = null;
        string? firstMatchAssembly = null;
        System.Collections.Generic.List<string>? allCandidates = null;

        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
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

        return firstMatch;
    }
}
