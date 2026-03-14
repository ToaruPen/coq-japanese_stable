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

        Type? match = null;
        string? matchAssemblyName = null;

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

            for (var typeIndex = 0; typeIndex < types.Length; typeIndex++)
            {
                if (types[typeIndex].Name != simpleTypeName)
                {
                    continue;
                }

                if (match is null)
                {
                    match = types[typeIndex];
                    matchAssemblyName = assemblies[assemblyIndex].GetName().Name;
                }
                else
                {
                    Trace.TraceWarning(
                        "QudJP: Ambiguous simple name '{0}': '{1}' (in '{2}') vs '{3}' (in '{4}'). Returning null.",
                        simpleTypeName,
                        match.FullName,
                        matchAssemblyName,
                        types[typeIndex].FullName,
                        assemblies[assemblyIndex].GetName().Name);
                    return null;
                }
            }
        }

        return match;
    }
}
