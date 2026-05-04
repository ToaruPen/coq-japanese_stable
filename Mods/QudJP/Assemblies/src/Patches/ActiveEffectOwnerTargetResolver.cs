using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

internal static class ActiveEffectOwnerTargetResolver
{
    internal static IEnumerable<MethodBase> ResolveTargetMethods(Type effectBaseType, string methodName)
    {
        var seen = new HashSet<MethodBase>();
        foreach (var type in EnumerateAssignableTypes(effectBaseType))
        {
            if (ShouldSkipType(type))
            {
                continue;
            }

            var method = AccessTools.DeclaredMethod(type, methodName, Type.EmptyTypes);
            if (method is null || method.IsAbstract || !seen.Add(method))
            {
                continue;
            }

            yield return method;
        }
    }

    private static IEnumerable<Type> EnumerateAssignableTypes(Type effectBaseType)
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        var types = new List<Type>();
        for (var assemblyIndex = 0; assemblyIndex < assemblies.Length; assemblyIndex++)
        {
            Type[] assemblyTypes;
            try
            {
                assemblyTypes = assemblies[assemblyIndex].GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                assemblyTypes = Array.FindAll(ex.Types, static type => type is not null)!;
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("QudJP: Failed to inspect assembly '{0}' for active effect owner targets: {1}", assemblies[assemblyIndex].FullName, ex);
                continue;
            }

            for (var typeIndex = 0; typeIndex < assemblyTypes.Length; typeIndex++)
            {
                var type = assemblyTypes[typeIndex];
                if (!effectBaseType.IsAssignableFrom(type))
                {
                    continue;
                }

                types.Add(type);
            }
        }

        types.Sort(static (left, right) => string.CompareOrdinal(left.FullName, right.FullName));
        return types;
    }

    private static bool ShouldSkipType(Type type)
    {
        if (type.IsInterface || type.ContainsGenericParameters)
        {
            return true;
        }

        var name = type.Name;
        return StringHelpers.ContainsOrdinal(name, "Cooking");
    }
}
