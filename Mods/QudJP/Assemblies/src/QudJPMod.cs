using System;
using System.Diagnostics;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading;
using HarmonyLib;

namespace QudJP;

public static class QudJPMod
{
    private static int isInitialized;

    public static void Init()
    {
        Initialize();
    }

    public static void Reset()
    {
        Initialize();
    }

    internal static void Initialize()
    {
        if (Interlocked.Exchange(ref isInitialized, 1) == 1)
        {
            return;
        }

        FontManager.Initialize();
        ApplyHarmonyPatches();
    }

    internal static void ApplyHarmonyPatches()
    {
        var harmony = CreateHarmony("com.qudjp.localization");
        if (harmony is null)
        {
            throw new InvalidOperationException(
                "QudJP: Harmony runtime not available. The mod cannot function without Harmony.");
        }

        InvokePatchAll(harmony);
        LogPatchResults(harmony);
    }

    internal static object? CreateHarmony(string harmonyId)
    {
        var harmonyType = ResolveHarmonyType();
        if (harmonyType is null)
        {
            Trace.TraceError("QudJP: HarmonyLib.Harmony type not found in any loaded assembly.");
            return null;
        }

        var constructor = harmonyType.GetConstructor(new[] { typeof(string) });
        if (constructor is null)
        {
            Trace.TraceError("QudJP: HarmonyLib.Harmony(string) constructor not found.");
            return null;
        }

        return constructor.Invoke(new object[] { harmonyId });
    }

    internal static void InvokePatchAll(object harmony)
    {
        var harmonyType = harmony.GetType();
        var createClassProcessor = harmonyType.GetMethod(
            "CreateClassProcessor",
            BindingFlags.Instance | BindingFlags.Public,
            binder: null,
            types: new[] { typeof(Type) },
            modifiers: null);

        if (createClassProcessor is not null)
        {
            PatchByClassProcessor(harmony, createClassProcessor);
            return;
        }

        Trace.TraceWarning("QudJP: Harmony.CreateClassProcessor(Type) not available. Falling back to PatchAll(Assembly).");

        var patchAllWithAssembly = harmonyType.GetMethod(
            "PatchAll",
            BindingFlags.Instance | BindingFlags.Public,
            binder: null,
            types: new[] { typeof(Assembly) },
            modifiers: null);

        if (patchAllWithAssembly is null)
        {
            throw new MissingMethodException(harmonyType.FullName, "PatchAll");
        }

        try
        {
            patchAllWithAssembly.Invoke(harmony, new object[] { Assembly.GetExecutingAssembly() });
        }
        catch (TargetInvocationException ex)
        {
            // PatchAll may throw when individual patches fail to resolve their targets
            // (e.g., game types not available). Log the error but don't crash —
            // patches applied before the failure remain in effect.
            LogToUnity($"[QudJP] Warning: Some patches failed to apply: {ex.InnerException?.Message ?? ex.Message}");
        }
    }

    private static void PatchByClassProcessor(object harmony, MethodInfo createClassProcessor)
    {
        var patchAssembly = Assembly.GetExecutingAssembly();
        var patchTypes = GetHarmonyPatchTypes(patchAssembly);
        for (var index = 0; index < patchTypes.Length; index++)
        {
            var patchType = patchTypes[index];
            try
            {
                if (!TryPreparePatchType(patchType, out var preparationFailure))
                {
                    LogToUnity($"[QudJP] Warning: Skipping patch {patchType.FullName}: {preparationFailure}");
                    continue;
                }

                var processor = createClassProcessor.Invoke(harmony, new object[] { patchType });
                if (processor is null)
                {
                    LogToUnity($"[QudJP] Warning: Harmony returned null class processor for patch {patchType.FullName}.");
                    continue;
                }

                var patchMethod = processor.GetType().GetMethod("Patch", Type.EmptyTypes);
                if (patchMethod is null)
                {
                    LogToUnity($"[QudJP] Warning: Patch() missing on class processor for {patchType.FullName}.");
                    continue;
                }

                patchMethod.Invoke(processor, null);
            }
            catch (Exception ex)
            {
                var details = ex is TargetInvocationException tie
                    ? tie.InnerException?.ToString() ?? tie.ToString()
                    : ex.ToString();
                LogToUnity($"[QudJP] Warning: Failed to apply patch {patchType.FullName}: {details}");
            }
        }
    }

    internal static bool TryPreparePatchType(Type patchType, out string failureReason)
    {
        var methods = AccessTools.GetDeclaredMethods(patchType);
        for (var index = 0; index < methods.Count; index++)
        {
            var method = methods[index];

            if (HasHarmonyTargetMethodAttribute(method)
                && !TryResolveSingleTarget(method, out failureReason))
            {
                return false;
            }

            if (HasHarmonyTargetMethodsAttribute(method)
                && !TryResolveMultipleTargets(method, out failureReason))
            {
                return false;
            }
        }

        failureReason = string.Empty;
        return true;
    }

    internal static Type[] GetHarmonyPatchTypes(Assembly assembly)
    {
        Type[] allTypes;
        try
        {
            allTypes = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            Trace.TraceWarning(
                "QudJP: ReflectionTypeLoadException loading types from {0}. Proceeding with {1} partially loaded type(s).",
                assembly.FullName,
                ex.Types?.Count(static t => t is not null) ?? 0);
            var loadedTypes = ex.Types;
            if (loadedTypes is null)
            {
                allTypes = Array.Empty<Type>();
            }
            else
            {
                var nonNull = new System.Collections.Generic.List<Type>(loadedTypes.Length);
                for (var i = 0; i < loadedTypes.Length; i++)
                {
                    if (loadedTypes[i] is { } t)
                    {
                        nonNull.Add(t);
                    }
                }

                allTypes = nonNull.ToArray();
            }
        }

        return allTypes
            .Where(HasHarmonyPatchAttribute)
            .ToArray();
    }

    private static bool HasHarmonyPatchAttribute(Type type)
    {
        var attributes = CustomAttributeData.GetCustomAttributes(type);
        for (var index = 0; index < attributes.Count; index++)
        {
            var attributeType = attributes[index].AttributeType;
            if (attributeType.FullName == "HarmonyLib.HarmonyPatch"
                || attributeType.Name == "HarmonyPatch")
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasHarmonyTargetMethodAttribute(MethodInfo method)
    {
        return HasHarmonyAttribute(method, "HarmonyTargetMethod");
    }

    private static bool HasHarmonyTargetMethodsAttribute(MethodInfo method)
    {
        return HasHarmonyAttribute(method, "HarmonyTargetMethods");
    }

    private static bool HasHarmonyAttribute(MemberInfo member, string attributeName)
    {
        var attributes = CustomAttributeData.GetCustomAttributes(member);
        for (var index = 0; index < attributes.Count; index++)
        {
            var attributeType = attributes[index].AttributeType;
            if (attributeType.FullName == "HarmonyLib." + attributeName
                || attributeType.Name == attributeName)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryResolveSingleTarget(MethodInfo resolver, out string failureReason)
    {
        try
        {
            if (resolver.Invoke(null, null) is MethodBase)
            {
                failureReason = string.Empty;
                return true;
            }

            failureReason = $"{resolver.DeclaringType?.FullName}.{resolver.Name} returned null.";
            return false;
        }
        catch (Exception ex)
        {
            failureReason = ex is TargetInvocationException tie
                ? tie.InnerException?.Message ?? tie.Message
                : ex.Message;
            return false;
        }
    }

    private static bool TryResolveMultipleTargets(MethodInfo resolver, out string failureReason)
    {
        try
        {
            if (resolver.Invoke(null, null) is not IEnumerable enumerable)
            {
                failureReason = $"{resolver.DeclaringType?.FullName}.{resolver.Name} returned null.";
                return false;
            }

            if (enumerable.Cast<object?>().OfType<MethodBase>().Any())
            {
                failureReason = string.Empty;
                return true;
            }

            failureReason = $"{resolver.DeclaringType?.FullName}.{resolver.Name} returned no target methods.";
            return false;
        }
        catch (Exception ex)
        {
            failureReason = ex is TargetInvocationException tie
                ? tie.InnerException?.Message ?? tie.Message
                : ex.Message;
            return false;
        }
    }

    internal static void LogToUnity(string message)
    {
        try
        {
            var debugType = Type.GetType("UnityEngine.Debug, UnityEngine.CoreModule", throwOnError: false);
            if (debugType is null)
            {
                Trace.TraceWarning("QudJP: UnityEngine.Debug not found in UnityEngine.CoreModule. Trying UnityEngine assembly name.");
                debugType = Type.GetType("UnityEngine.Debug, UnityEngine", throwOnError: false);
            }

            var logMethod = debugType?.GetMethod("Log", BindingFlags.Public | BindingFlags.Static, binder: null, types: new[] { typeof(object) }, modifiers: null);
            if (logMethod is not null)
            {
                logMethod.Invoke(null, new object[] { message });
            }
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("QudJP: Unity debug logging failed; falling back to trace. {0}", ex.Message);
        }

        Trace.TraceInformation(message);
    }

    internal static void LogPatchResults(object harmony)
    {
        var getPatchedMethods = harmony.GetType().GetMethod("GetPatchedMethods");
        if (getPatchedMethods is null)
        {
            LogToUnity("[QudJP] Warning: GetPatchedMethods not available.");
            return;
        }

        try
        {
            var methods = (System.Collections.IEnumerable)getPatchedMethods.Invoke(harmony, null)!;
            var count = 0;
            foreach (var _ in methods)
            {
                count++;
            }

            LogToUnity($"[QudJP] Harmony patching complete: {count} method(s) patched.");
        }
        catch (Exception ex)
        {
            var message = ex is TargetInvocationException tie
                ? tie.InnerException?.Message ?? tie.Message
                : ex.Message;
            LogToUnity($"[QudJP] Warning: Failed to enumerate patched methods: {message}");
        }
    }

    internal static Type? ResolveHarmonyType()
    {
        var typeFrom0Harmony = Type.GetType("HarmonyLib.Harmony, 0Harmony", throwOnError: false);
        if (typeFrom0Harmony is not null)
        {
            return typeFrom0Harmony;
        }

        Trace.TraceWarning("QudJP: Harmony type was not found in 0Harmony. Trying HarmonyLib assembly name.");
        return Type.GetType("HarmonyLib.Harmony, HarmonyLib", throwOnError: false);
    }
}
