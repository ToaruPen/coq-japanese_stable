using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;

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

    internal static void LogToUnity(string message)
    {
#if HAS_TMP
        UnityEngine.Debug.Log(message);
#else
        Trace.TraceInformation(message);
#endif
    }

    internal static void LogPatchResults(object harmony)
    {
        var getPatchedMethods = harmony.GetType().GetMethod("GetPatchedMethods");
        if (getPatchedMethods is null)
        {
            LogToUnity("[QudJP] Warning: GetPatchedMethods not available.");
            return;
        }

        var methods = (System.Collections.IEnumerable)getPatchedMethods.Invoke(harmony, null)!;
        var count = 0;
        foreach (var _ in methods)
        {
            count++;
        }

        LogToUnity($"[QudJP] Harmony patching complete: {count} method(s) patched.");
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
