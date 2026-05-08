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
}
