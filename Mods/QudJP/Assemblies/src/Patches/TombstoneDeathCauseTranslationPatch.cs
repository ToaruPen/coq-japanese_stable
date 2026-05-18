using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class TombstoneDeathCauseTranslationPatch
{
    private const string Context = nameof(TombstoneDeathCauseTranslationPatch);

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>(capacity: 2);
        AddTarget(targets, "XRL.World.Parts.Tombstone", "GenerateTombstone");
        AddTarget(targets, "XRL.World.Parts.RachelsTombstone", "GenerateTombstone");
        return targets;
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        try
        {
            var originalInstructions = instructions.ToList();
            var transformed = 0;
            var transformedInstructions = new List<CodeInstruction>(originalInstructions.Count);
            foreach (var instruction in originalInstructions)
            {
                if (TryGetClipTextReplacement(instruction, out var replacement))
                {
                    transformed++;
                    transformedInstructions.Add(new CodeInstruction(OpCodes.Ldarg_0));
                    transformedInstructions.Add(new CodeInstruction(
                        OpCodes.Call,
                        replacement));
                    continue;
                }

                transformedInstructions.Add(instruction);
            }

            if (transformed == 0)
            {
                Trace.TraceWarning("QudJP: {0}.Transpiler found no StringFormat.ClipText calls.", Context);
            }

            return transformedInstructions;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Transpiler failed; returning original instructions: {1}", Context, ex);
            return instructions;
        }
    }

    public static string ClipTextTranslatingDeathCause(string source, int maxWidth, object owner)
    {
        try
        {
            var text = source;
            if (ShouldTranslateGeneratedDeathCause(owner)
                && TombstoneDeathCauseTranslator.TryTranslate(source, out var translated))
            {
                DynamicTextObservability.RecordTransform(Context, Context + ".DeathCause", source, translated);
                text = translated;
            }

            return ClipText(text, maxWidth);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.ClipTextTranslatingDeathCause failed: {1}", Context, ex);
            return ClipText(source, maxWidth);
        }
    }

    public static string ClipTextTranslatingDeathCause(
        string source,
        int maxWidth,
        bool keepNewlines,
        bool transformMarkup,
        bool transformMarkupIfMultipleLines,
        object owner)
    {
        try
        {
            var text = source;
            if (ShouldTranslateGeneratedDeathCause(owner)
                && TombstoneDeathCauseTranslator.TryTranslate(source, out var translated))
            {
                DynamicTextObservability.RecordTransform(Context, Context + ".DeathCause", source, translated);
                text = translated;
            }

            return ClipText(text, maxWidth, keepNewlines, transformMarkup, transformMarkupIfMultipleLines);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.ClipTextTranslatingDeathCause failed: {1}", Context, ex);
            return ClipText(source, maxWidth, keepNewlines, transformMarkup, transformMarkupIfMultipleLines);
        }
    }

    private static string ClipText(string source, int maxWidth)
    {
        var method = AccessTools.Method(AccessTools.TypeByName("XRL.UI.StringFormat"), "ClipText", [typeof(string), typeof(int)]);
        if (method is null)
        {
            return source;
        }

        try
        {
            var clipped = method.Invoke(null, [source, maxWidth]) as string;
            if (clipped is null)
            {
                return source;
            }

            return clipped;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.ClipText fallback failed: {1}", Context, ex);
            return source;
        }
    }

    private static string ClipText(
        string source,
        int maxWidth,
        bool keepNewlines,
        bool transformMarkup,
        bool transformMarkupIfMultipleLines)
    {
        var method = AccessTools.Method(
            AccessTools.TypeByName("XRL.UI.StringFormat"),
            "ClipText",
            [typeof(string), typeof(int), typeof(bool), typeof(bool), typeof(bool)]);
        if (method is null)
        {
            return source;
        }

        try
        {
            var clipped = method.Invoke(
                null,
                [source, maxWidth, keepNewlines, transformMarkup, transformMarkupIfMultipleLines]) as string;
            if (clipped is null)
            {
                return source;
            }

            return clipped;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.ClipText fallback failed: {1}", Context, ex);
            return source;
        }
    }

    private static void AddTarget(ICollection<MethodBase> targets, string typeName, string methodName)
    {
        var type = AccessTools.TypeByName(typeName);
        var method = type is null ? null : AccessTools.Method(type, methodName, Type.EmptyTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0} target not found: {1}.{2}().", Context, typeName, methodName);
            return;
        }

        targets.Add(method);
    }

    private static bool ShouldTranslateGeneratedDeathCause(object owner)
    {
        if (string.Equals(owner.GetType().FullName, "XRL.World.Parts.RachelsTombstone", StringComparison.Ordinal))
        {
            return true;
        }

        var inscription = AccessTools.Field(owner.GetType(), "Inscription")?.GetValue(owner)
            ?? AccessTools.Property(owner.GetType(), "Inscription")?.GetValue(owner);
        return string.IsNullOrEmpty(inscription as string);
    }

    private static bool TryGetClipTextReplacement(CodeInstruction instruction, out MethodInfo replacement)
    {
        if (instruction.opcode != OpCodes.Call || instruction.operand is not MethodInfo method)
        {
            replacement = null!;
            return false;
        }

        var parameters = method.GetParameters();
        if (method.ReturnType != typeof(string)
            || !string.Equals(method.Name, "ClipText", StringComparison.Ordinal)
            || !string.Equals(method.DeclaringType?.FullName, "XRL.UI.StringFormat", StringComparison.Ordinal)
            || parameters.Length is not (2 or 5)
            || parameters[0].ParameterType != typeof(string)
            || parameters[1].ParameterType != typeof(int))
        {
            replacement = null!;
            return false;
        }

        if (parameters.Length == 2)
        {
            var replacementMethod = AccessTools.Method(
                typeof(TombstoneDeathCauseTranslationPatch),
                nameof(ClipTextTranslatingDeathCause),
                [typeof(string), typeof(int), typeof(object)]);
            if (replacementMethod is null)
            {
                Trace.TraceError("QudJP: {0} replacement method not found for 2-argument ClipText.", Context);
                replacement = null!;
                return false;
            }

            replacement = replacementMethod;
            return true;
        }

        if (parameters.Length == 5
            && parameters[2].ParameterType == typeof(bool)
            && parameters[3].ParameterType == typeof(bool)
            && parameters[4].ParameterType == typeof(bool))
        {
            var replacementMethod = AccessTools.Method(
                typeof(TombstoneDeathCauseTranslationPatch),
                nameof(ClipTextTranslatingDeathCause),
                [typeof(string), typeof(int), typeof(bool), typeof(bool), typeof(bool), typeof(object)]);
            if (replacementMethod is null)
            {
                Trace.TraceError("QudJP: {0} replacement method not found for 5-argument ClipText.", Context);
                replacement = null!;
                return false;
            }

            replacement = replacementMethod;
            return true;
        }

        replacement = null!;
        return false;
    }
}
