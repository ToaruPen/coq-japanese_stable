using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class TradeUiLegacyScreenTranslationPatch
{
    private const string Context = nameof(TradeUiLegacyScreenTranslationPatch);

    private static readonly HashSet<string> TranslatableLiterals = new(StringComparer.Ordinal);

    private static readonly MethodInfo TranslateLiteralMethod =
        AccessTools.Method(typeof(TradeUiLegacyScreenTranslationPatch), nameof(TranslateLiteral))
        ?? throw new InvalidOperationException("TranslateLiteral method not found.");

    private static readonly MethodInfo TranslateRenderedPromptMethod =
        AccessTools.Method(typeof(TradeUiLegacyScreenTranslationPatch), nameof(TranslateRenderedPrompt))
        ?? throw new InvalidOperationException("TranslateRenderedPrompt method not found.");

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        try
        {
            var tradeUiType = GameTypeResolver.FindType("XRL.UI.TradeUI", "TradeUI");
            var gameObjectType = GameTypeResolver.FindType("XRL.World.GameObject", "GameObject");
            var tradeScreenModeType = GameTypeResolver.FindType("XRL.UI.TradeUI+TradeScreenMode", "TradeScreenMode");
            if (tradeUiType is null || gameObjectType is null || tradeScreenModeType is null)
            {
                Trace.TraceError("QudJP: {0}.ShowTradeScreen parameter types could not be resolved.", Context);
                return null;
            }

            var method = AccessTools.Method(
                tradeUiType,
                "ShowTradeScreen",
                new[] { gameObjectType, typeof(float), tradeScreenModeType });
            if (method is null)
            {
                Trace.TraceError("QudJP: {0}.ShowTradeScreen target method not found.", Context);
            }

            return method;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.TargetMethod failed: {1}", Context, ex);
            return null;
        }
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
            return LegacyGamepadPromptTranslationHelpers.TranslateTradeUiLegacyLiteral(source);
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
            return LegacyGamepadPromptTranslationHelpers.TranslateTradeUiLegacyRendered(source);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.TranslateRenderedPrompt failed: {1}", Context, ex);
            return source;
        }
    }
}
