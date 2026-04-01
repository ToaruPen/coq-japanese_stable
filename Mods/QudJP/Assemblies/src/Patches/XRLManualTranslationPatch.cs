using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class XrlManualTranslationPatch
{
    private const string Context = nameof(XrlManualTranslationPatch);

    private static readonly HashSet<string> TranslatableLiterals = new(StringComparer.Ordinal);

    private static readonly MethodInfo TranslateLiteralMethod =
        AccessTools.Method(typeof(XrlManualTranslationPatch), nameof(TranslateLiteral))
        ?? throw new InvalidOperationException("TranslateLiteral method not found.");

    private static readonly MethodInfo TranslateRenderedPromptMethod =
        AccessTools.Method(typeof(XrlManualTranslationPatch), nameof(TranslateRenderedPrompt))
        ?? throw new InvalidOperationException("TranslateRenderedPrompt method not found.");

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("XRL.Help.XRLManual", "XRLManual");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            return null;
        }

        var method = AccessTools.Method(targetType, "RenderIndex", new[] { typeof(int) });
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.RenderIndex(int) not found.", Context);
        }

        return method;
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        try
        {
            return LegacyGamepadPromptTranspilerHelpers.Apply(
                instructions,
                TranslatableLiterals,
                TranslateLiteralMethod,
                TranslateRenderedPromptMethod,
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
        try
        {
            return LegacyGamepadPromptTranslationHelpers.TranslateXrlManualLiteral(source);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.TranslateLiteral failed: {1}", Context, ex);
            return source;
        }
    }

    public static string TranslateRenderedPrompt(string source)
    {
        try
        {
            return LegacyGamepadPromptTranslationHelpers.TranslateXrlManualRendered(source);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.TranslateRenderedPrompt failed: {1}", Context, ex);
            return source;
        }
    }
}
