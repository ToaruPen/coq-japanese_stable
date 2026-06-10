using System;
using System.Diagnostics;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using QudJP.Patches;

namespace QudJP;

public static class QudJPMod
{
    internal const string BuildMarker = "inventory-action-fallback-no-resort-v1";

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
        InitializeCore(FontManager.Initialize, ApplyHarmonyPatches);
    }

    internal static void InitializeForTests(Action initializeFonts, Action applyPatches)
    {
        InitializeCore(initializeFonts, applyPatches);
    }

    internal static void ResetInitializationForTests()
    {
        Interlocked.Exchange(ref isInitialized, 0);
    }

    internal static bool IsInitializedForTests()
    {
        return Volatile.Read(ref isInitialized) == 1;
    }

    private static void InitializeCore(Action initializeFonts, Action applyPatches)
    {
        if (Interlocked.CompareExchange(ref isInitialized, 1, 0) == 1)
        {
            return;
        }

        using var totalTiming = RuntimeStartupTiming.Measure("qudjp.initialize_core");
        try
        {
            var assemblyVersion = typeof(QudJPMod).Assembly.GetName().Version;
            RuntimeDiagnostics.LogStatus(
                $"[QudJP] Build marker: {BuildMarker}, Version: {assemblyVersion}, BuildFlavor: {RuntimeDiagnostics.BuildFlavor}");
            using (RuntimeStartupTiming.Measure("font.initialize"))
            {
                initializeFonts();
            }

            using (RuntimeStartupTiming.Measure("harmony.setup_and_apply"))
            {
                applyPatches();
            }

            DisplayNameRouteTranslationRegistration.Register();
        }
        catch
        {
            Interlocked.Exchange(ref isInitialized, 0);
            throw;
        }
    }

    internal static void ApplyHarmonyPatches()
    {
        object? harmony;
        using (RuntimeStartupTiming.Measure("harmony.create"))
        {
            harmony = CreateHarmony("com.qudjp.localization");
        }

        if (harmony is null)
        {
            throw new InvalidOperationException(
                "QudJP: Harmony runtime not available. The mod cannot function without Harmony.");
        }

        using (RuntimeStartupTiming.Measure("harmony.invoke_patch_all"))
        {
            InvokePatchAll(harmony);
        }

        using (RuntimeStartupTiming.Measure("harmony.log_patch_results"))
        {
            LogPatchResults(harmony);
        }
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
            RuntimeDiagnostics.LogWarning($"[QudJP] Warning: Some patches failed to apply: {ex.InnerException?.Message ?? ex.Message}");
        }
    }

    private static void PatchByClassProcessor(object harmony, MethodInfo createClassProcessor)
    {
        var patchAssembly = Assembly.GetExecutingAssembly();
        Type[] patchTypes;
        using (RuntimeStartupTiming.Measure("harmony.scan_patch_types"))
        {
            patchTypes = GetHarmonyPatchTypes(patchAssembly);
        }

        var preparedCount = 0;
        var prepareSkippedCount = 0;
        var applySkippedCount = 0;
        var appliedCount = 0;
        var fallbackScansBeforePatchLoop = GameTypeResolver.FallbackScanCountForDiagnostics;
        var preparationStopwatch = new Stopwatch();
        var patchStopwatch = new Stopwatch();
        var detailedPatchTiming = IsDetailedPatchTimingEnabled();
        var preflightPatchTargets = IsPatchTargetPreflightEnabled();
        for (var index = 0; index < patchTypes.Length; index++)
        {
            var patchType = patchTypes[index];
            var patchTypeName = patchType.FullName ?? patchType.Name;
            var resolvedTargetCount = 0;
            var currentPhase = "prepare";
            var detailedStopwatch = detailedPatchTiming ? Stopwatch.StartNew() : null;
            try
            {
                if (ShouldPreflightPatchType(patchType, preflightPatchTargets))
                {
                    preparationStopwatch.Start();
                    if (!TryPreparePatchType(patchType, out var preparationFailure, out resolvedTargetCount))
                    {
                        preparationStopwatch.Stop();
                        detailedStopwatch?.Stop();
                        prepareSkippedCount++;
                        LogPatchTypeTiming(
                            detailedPatchTiming,
                            "harmony.patch_prepare",
                            patchTypeName,
                            detailedStopwatch?.Elapsed ?? TimeSpan.Zero,
                            "skipped",
                            resolvedTargetCount);
                        RuntimeDiagnostics.LogWarning($"[QudJP] Warning: Skipping patch {patchType.FullName}: {preparationFailure}");
                        continue;
                    }
                    preparationStopwatch.Stop();
                    detailedStopwatch?.Stop();
                    preparedCount++;
                    LogPatchTypeTiming(
                        detailedPatchTiming,
                        "harmony.patch_prepare",
                        patchTypeName,
                        detailedStopwatch?.Elapsed ?? TimeSpan.Zero,
                        "prepared",
                        resolvedTargetCount);
                }
                else
                {
                    detailedStopwatch?.Stop();
                    preparedCount++;
                    LogPatchTypeTiming(
                        detailedPatchTiming,
                        "harmony.patch_prepare",
                        patchTypeName,
                        detailedStopwatch?.Elapsed ?? TimeSpan.Zero,
                        "preflight_disabled",
                        resolvedTargetCount);
                }

                currentPhase = "apply";
                detailedStopwatch = detailedPatchTiming ? Stopwatch.StartNew() : null;
                patchStopwatch.Start();
                var processor = createClassProcessor.Invoke(harmony, new object[] { patchType });
                if (processor is null)
                {
                    patchStopwatch.Stop();
                    detailedStopwatch?.Stop();
                    applySkippedCount++;
                    LogPatchTypeTiming(
                        detailedPatchTiming,
                        "harmony.patch_apply",
                        patchTypeName,
                        detailedStopwatch?.Elapsed ?? TimeSpan.Zero,
                        "skipped_null_processor",
                        resolvedTargetCount);
                    RuntimeDiagnostics.LogWarning($"[QudJP] Warning: Harmony returned null class processor for patch {patchType.FullName}.");
                    continue;
                }

                var patchMethod = processor.GetType().GetMethod("Patch", Type.EmptyTypes);
                if (patchMethod is null)
                {
                    patchStopwatch.Stop();
                    detailedStopwatch?.Stop();
                    applySkippedCount++;
                    LogPatchTypeTiming(
                        detailedPatchTiming,
                        "harmony.patch_apply",
                        patchTypeName,
                        detailedStopwatch?.Elapsed ?? TimeSpan.Zero,
                        "skipped_missing_patch_method",
                        resolvedTargetCount);
                    RuntimeDiagnostics.LogWarning($"[QudJP] Warning: Patch() missing on class processor for {patchType.FullName}.");
                    continue;
                }

                patchMethod.Invoke(processor, null);
                patchStopwatch.Stop();
                detailedStopwatch?.Stop();
                appliedCount++;
                LogPatchTypeTiming(
                    detailedPatchTiming,
                    "harmony.patch_apply",
                    patchTypeName,
                    detailedStopwatch?.Elapsed ?? TimeSpan.Zero,
                    "applied",
                    resolvedTargetCount);
            }
            catch (Exception ex)
            {
                preparationStopwatch.Stop();
                patchStopwatch.Stop();
                detailedStopwatch?.Stop();
                if (currentPhase == "prepare")
                {
                    prepareSkippedCount++;
                }
                else
                {
                    applySkippedCount++;
                }

                LogPatchTypeTiming(
                    detailedPatchTiming,
                    currentPhase == "prepare" ? "harmony.patch_prepare" : "harmony.patch_apply",
                    patchTypeName,
                    detailedStopwatch?.Elapsed ?? TimeSpan.Zero,
                    "failed",
                    resolvedTargetCount);
                var details = ex is TargetInvocationException tie
                    ? tie.InnerException?.ToString() ?? tie.ToString()
                    : ex.ToString();
                RuntimeDiagnostics.LogWarning($"[QudJP] Warning: Failed to apply patch {patchType.FullName}: {details}");
            }
        }

        var patchLoopSummary = FormatPatchLoopSummary(
            new PatchLoopSummary(
                patchTypes.Length,
                preparedCount,
                prepareSkippedCount,
                appliedCount,
                applySkippedCount,
                preflightPatchTargets));
        RuntimeStartupTiming.LogElapsed(
            "harmony.prepare_patch_types",
            preparationStopwatch.Elapsed,
            patchLoopSummary.PrepareDetail);
        RuntimeStartupTiming.LogElapsed(
            "harmony.apply_patch_types",
            patchStopwatch.Elapsed,
            patchLoopSummary.ApplyDetail);
        RuntimeStartupTiming.LogElapsed(
            "harmony.type_fallback_scans_total",
            TimeSpan.Zero,
            $"count={GameTypeResolver.FallbackScanCountForDiagnostics - fallbackScansBeforePatchLoop}");
    }

    internal readonly struct PatchLoopSummary
    {
        internal PatchLoopSummary(
            int patchTypes,
            int prepared,
            int prepareSkipped,
            int applied,
            int applySkipped,
            bool preflight)
        {
            PatchTypes = patchTypes;
            Prepared = prepared;
            PrepareSkipped = prepareSkipped;
            Applied = applied;
            ApplySkipped = applySkipped;
            Preflight = preflight;
        }

        internal int PatchTypes { get; }

        internal int Prepared { get; }

        internal int PrepareSkipped { get; }

        internal int Applied { get; }

        internal int ApplySkipped { get; }

        internal bool Preflight { get; }
    }

    internal static (string PrepareDetail, string ApplyDetail) FormatPatchLoopSummary(PatchLoopSummary summary)
    {
        return (
            $"patch_types={summary.PatchTypes};prepared={summary.Prepared};skipped={summary.PrepareSkipped};"
            + $"preflight={summary.Preflight}",
            $"patch_types={summary.PatchTypes};applied={summary.Applied};skipped={summary.ApplySkipped}");
    }

    internal static bool TryPreparePatchType(Type patchType, out string failureReason)
    {
        return TryPreparePatchType(patchType, out failureReason, out _);
    }

    internal static bool TryPreparePatchType(Type patchType, out string failureReason, out int resolvedTargetCount)
    {
        resolvedTargetCount = 0;
        var methods = AccessTools.GetDeclaredMethods(patchType);
        for (var index = 0; index < methods.Count; index++)
        {
            var method = methods[index];

            if (HasHarmonyTargetMethodAttribute(method))
            {
                if (!TryResolveSingleTarget(method, out failureReason, out var singleTargetCount))
                {
                    return false;
                }

                resolvedTargetCount += singleTargetCount;
            }

            if (HasHarmonyTargetMethodsAttribute(method))
            {
                if (!TryResolveMultipleTargets(method, out failureReason, out var multipleTargetCount))
                {
                    return false;
                }

                resolvedTargetCount += multipleTargetCount;
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

    private static bool TryResolveSingleTarget(MethodInfo resolver, out string failureReason, out int resolvedTargetCount)
    {
        resolvedTargetCount = 0;
        try
        {
            if (resolver.Invoke(null, null) is MethodBase)
            {
                resolvedTargetCount = 1;
                failureReason = string.Empty;
                return true;
            }

            failureReason = $"{resolver.DeclaringType?.FullName}.{resolver.Name} returned null.";
            return false;
        }
        catch (Exception ex)
        {
            var details = ex is TargetInvocationException tie
                ? tie.InnerException?.ToString() ?? tie.ToString()
                : ex.ToString();
            failureReason = $"{resolver.DeclaringType?.FullName}.{resolver.Name} threw: {details}";
            return false;
        }
    }

    private static bool TryResolveMultipleTargets(MethodInfo resolver, out string failureReason, out int resolvedTargetCount)
    {
        resolvedTargetCount = 0;
        try
        {
            if (resolver.Invoke(null, null) is not IEnumerable enumerable)
            {
                failureReason = $"{resolver.DeclaringType?.FullName}.{resolver.Name} returned null.";
                return false;
            }

            resolvedTargetCount = enumerable.Cast<object?>().OfType<MethodBase>().Count();
            if (resolvedTargetCount > 0)
            {
                failureReason = string.Empty;
                return true;
            }

            failureReason = $"{resolver.DeclaringType?.FullName}.{resolver.Name} returned no target methods.";
            return false;
        }
        catch (Exception ex)
        {
            var details = ex is TargetInvocationException tie
                ? tie.InnerException?.ToString() ?? tie.ToString()
                : ex.ToString();
            failureReason = $"{resolver.DeclaringType?.FullName}.{resolver.Name} threw: {details}";
            return false;
        }
    }

    private static bool IsDetailedPatchTimingEnabled()
    {
        return IsEnvironmentFlagEnabled("QUDJP_STARTUP_PATCH_TIMING");
    }

    private static bool IsPatchTargetPreflightEnabled()
    {
        return IsEnvironmentFlagEnabled("QUDJP_PATCH_TARGET_PREFLIGHT");
    }

    private static bool IsEnvironmentFlagEnabled(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.Equals(value, "1", StringComparison.Ordinal)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldPreflightPatchType(Type patchType, bool preflightAllPatchTargets)
    {
        return preflightAllPatchTargets
            || string.Equals(
                patchType.FullName,
                "QudJP.Patches.HistoricStringExpanderPatch",
                StringComparison.Ordinal);
    }

    private static void LogPatchTypeTiming(
        bool enabled,
        string phasePrefix,
        string patchTypeName,
        TimeSpan elapsed,
        string status,
        int resolvedTargetCount)
    {
        if (!enabled)
        {
            return;
        }

        RuntimeStartupTiming.LogElapsed(
            phasePrefix + "." + patchTypeName,
            elapsed,
            $"status={status};targets={resolvedTargetCount}");
    }

    internal static void LogToUnity(string message)
    {
        LogToUnity(message, RuntimeLogSeverity.Information);
    }

    internal static void LogToUnity(string message, RuntimeLogSeverity severity)
    {
        try
        {
            var debugType = Type.GetType("UnityEngine.Debug, UnityEngine.CoreModule", throwOnError: false);
            if (debugType is null)
            {
                Trace.TraceWarning("QudJP: UnityEngine.Debug not found in UnityEngine.CoreModule. Trying UnityEngine assembly name.");
                debugType = Type.GetType("UnityEngine.Debug, UnityEngine", throwOnError: false);
            }

            var logMethod = debugType?.GetMethod(GetUnityLogMethodName(severity), BindingFlags.Public | BindingFlags.Static, binder: null, types: new[] { typeof(object) }, modifiers: null);
            if (logMethod is not null)
            {
                logMethod.Invoke(null, new object[] { message });
            }
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("QudJP: Unity debug logging failed; falling back to trace. {0}", ex.Message);
        }

        WriteTrace(message, severity);
    }

    private static string GetUnityLogMethodName(RuntimeLogSeverity severity)
    {
        return severity switch
        {
            RuntimeLogSeverity.Warning => "LogWarning",
            RuntimeLogSeverity.Error => "LogError",
            _ => "Log",
        };
    }

    private static void WriteTrace(string message, RuntimeLogSeverity severity)
    {
        switch (severity)
        {
            case RuntimeLogSeverity.Warning:
                Trace.TraceWarning("QudJP: {0}", message);
                break;
            case RuntimeLogSeverity.Error:
                Trace.TraceError("QudJP: {0}", message);
                break;
            default:
                Trace.TraceInformation(message);
                break;
        }
    }

    internal static void LogPatchResults(object harmony)
    {
        var getPatchedMethods = harmony.GetType().GetMethod("GetPatchedMethods");
        if (getPatchedMethods is null)
        {
            RuntimeDiagnostics.LogWarning("[QudJP] Warning: GetPatchedMethods not available.");
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

            RuntimeDiagnostics.LogStatus($"[QudJP] Harmony patching complete: {count} method(s) patched.");
            if (count == 0)
            {
                RuntimeDiagnostics.LogWarning(
                    "[QudJP] Warning: Harmony patched zero methods. On Apple Silicon macOS, "
                    + "'mprotect returned EACCES' in Player.log usually means the game-bundled "
                    + "0Harmony.dll cannot patch under native ARM64; launch Caves of Qud with "
                    + "Rosetta 2 as the recommended workaround, for example: arch -x86_64 "
                    + "<CoQ binary>. Advanced users can also back up and replace the game "
                    + "Managed/0Harmony.dll with Harmony 2.4.2.");
            }
        }
        catch (Exception ex)
        {
            var message = ex is TargetInvocationException tie
                ? tie.InnerException?.Message ?? tie.Message
                : ex.Message;
            RuntimeDiagnostics.LogWarning($"[QudJP] Warning: Failed to enumerate patched methods: {message}");
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
