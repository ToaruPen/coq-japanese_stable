using System;
using System.Reflection;

namespace QudJP.Patches;

internal static class DisplayNameSemanticPipeline
{
    internal static bool TryTranslateResult(ref string result, string context)
    {
        if (string.IsNullOrEmpty(result))
        {
            return false;
        }

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(result, context);
        if (string.Equals(translated, result, StringComparison.Ordinal))
        {
            return false;
        }

        result = translated;
        return true;
    }

    internal static bool TryTranslateResult(ref string result, MethodBase? originalMethod, string context)
    {
        return TryTranslateResult(ref result, ComposeMethodContext(originalMethod, context));
    }

    internal static string ComposeMethodContext(MethodBase? originalMethod, string context)
    {
        if (originalMethod is null)
        {
            return context;
        }

        return ObservabilityHelpers.ComposeContext(
            context,
            $"method={originalMethod.DeclaringType?.Name ?? "<unknown>"}.{originalMethod.Name}");
    }
}
