using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using XRL.UI;

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
        var transformed = 0;
        foreach (var instruction in instructions)
        {
            if (TryGetClipTextReplacement(instruction, out var replacement))
            {
                transformed++;
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(
                    OpCodes.Call,
                    replacement);
                continue;
            }

            yield return instruction;
        }

        if (transformed == 0)
        {
            Trace.TraceWarning("QudJP: {0}.Transpiler found no StringFormat.ClipText calls.", Context);
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

            return StringFormat.ClipText(text, maxWidth);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.ClipTextTranslatingDeathCause failed: {1}", Context, ex);
            return StringFormat.ClipText(source, maxWidth);
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

            return StringFormat.ClipText(text, maxWidth, keepNewlines, transformMarkup, transformMarkupIfMultipleLines);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.ClipTextTranslatingDeathCause failed: {1}", Context, ex);
            return StringFormat.ClipText(source, maxWidth, keepNewlines, transformMarkup, transformMarkupIfMultipleLines);
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
            || parameters.Length is not (2 or 5)
            || parameters[0].ParameterType != typeof(string)
            || parameters[1].ParameterType != typeof(int))
        {
            replacement = null!;
            return false;
        }

        if (parameters.Length == 2)
        {
            replacement = AccessTools.Method(
                typeof(TombstoneDeathCauseTranslationPatch),
                nameof(ClipTextTranslatingDeathCause),
                [typeof(string), typeof(int), typeof(object)]);
            return true;
        }

        if (parameters.Length == 5
            && parameters[2].ParameterType == typeof(bool)
            && parameters[3].ParameterType == typeof(bool)
            && parameters[4].ParameterType == typeof(bool))
        {
            replacement = AccessTools.Method(
                typeof(TombstoneDeathCauseTranslationPatch),
                nameof(ClipTextTranslatingDeathCause),
                [typeof(string), typeof(int), typeof(bool), typeof(bool), typeof(bool), typeof(object)]);
            return true;
        }

        replacement = null!;
        return false;
    }
}
