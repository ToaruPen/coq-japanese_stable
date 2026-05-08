using System;
using System.Diagnostics;
using System.Threading;

namespace QudJP;

internal static class RuntimeDiagnostics
{
    private const int OverrideUnset = -1;
    private const int OverrideDisabled = 0;
    private const int OverrideEnabled = 1;

#if QUDJP_DEV_BUILD
    private const bool DefaultVerboseProbesEnabled = true;
    internal const string BuildFlavor = "dev";
#else
    private const bool DefaultVerboseProbesEnabled = false;
    internal const string BuildFlavor = "release";
#endif

    private static int verboseProbesOverride = OverrideUnset;

    internal static bool VerboseProbesEnabled
    {
        get
        {
            var overrideValue = Volatile.Read(ref verboseProbesOverride);
            return overrideValue switch
            {
                OverrideEnabled => true,
                OverrideDisabled => false,
                _ => DefaultVerboseProbesEnabled,
            };
        }
    }

    internal static void SetVerboseProbesEnabledForTests(bool? enabled)
    {
        var value = enabled switch
        {
            true => OverrideEnabled,
            false => OverrideDisabled,
            _ => OverrideUnset,
        };
        Volatile.Write(ref verboseProbesOverride, value);
    }

    internal static void LogStatus(string message)
    {
        QudJPMod.LogToUnity(message, RuntimeLogSeverity.Information);
    }

    internal static void LogWarning(string message)
    {
        QudJPMod.LogToUnity(message, RuntimeLogSeverity.Warning);
    }

    internal static void LogError(string message)
    {
        QudJPMod.LogToUnity(message, RuntimeLogSeverity.Error);
    }

    [Conditional("QUDJP_DEV_BUILD")]
    internal static void LogVerboseProbe(Func<string> messageFactory)
    {
        if (!VerboseProbesEnabled)
        {
            return;
        }

        var message = messageFactory();
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        QudJPMod.LogToUnity(message);
    }

    [Conditional("QUDJP_DEV_BUILD")]
    internal static void RunVerboseProbe(Action action)
    {
        if (VerboseProbesEnabled)
        {
            action();
        }
    }
}

internal enum RuntimeLogSeverity
{
    Information,
    Warning,
    Error,
}
