using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class XrlCoreStartMainMenuTranslationPatch
{
    private const string Context = nameof(XrlCoreStartMainMenuTranslationPatch);

    private static readonly HashSet<string> TranslatableLiterals = new(StringComparer.Ordinal);

    private static readonly MethodInfo TranslateLiteralMethod =
        AccessTools.Method(typeof(XrlCoreStartMainMenuTranslationPatch), nameof(TranslateLiteral))
        ?? throw new InvalidOperationException("TranslateLiteral method not found.");

    private static readonly MethodInfo TranslateRenderedMethod =
        AccessTools.Method(typeof(XrlCoreStartMainMenuTranslationPatch), nameof(TranslateRendered))
        ?? throw new InvalidOperationException("TranslateRendered method not found.");

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var coreType = GameTypeResolver.FindType("XRL.Core.XRLCore", "XRLCore");
        if (coreType is null)
        {
            Trace.TraceError("QudJP: {0} failed to resolve XRL.Core.XRLCore.", Context);
            yield break;
        }

        var method = AccessTools.Method(coreType, "_Start", Type.EmptyTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0} failed to resolve XRLCore._Start().", Context);
            yield break;
        }

        yield return method;
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        try
        {
            return LegacyGamepadPromptTranspilerHelpers.Apply(
                instructions,
                TranslatableLiterals,
                TranslateLiteralMethod,
                TranslateRenderedMethod,
                Context);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Transpiler failed: {1}", Context, ex);
            return instructions;
        }
    }

    public static string TranslateLiteral(string source)
    {
        return source;
    }

    public static string TranslateRendered(string source)
    {
        try
        {
            return LegacyGamepadPromptTranslationHelpers.TranslateXrlCoreStartMainMenuRendered(source);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.TranslateRendered failed: {1}", Context, ex);
            return source;
        }
    }
}
