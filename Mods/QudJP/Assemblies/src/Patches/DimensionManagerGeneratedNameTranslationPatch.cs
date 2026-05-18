using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class DimensionManagerGeneratedNameTranslationPatch
{
    private const string Context = nameof(DimensionManagerGeneratedNameTranslationPatch);

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>(capacity: 1);
        var targetType = AccessTools.TypeByName("XRL.World.Encounters.DimensionManager");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            return targets;
        }

        AddTarget(targets, targetType, "InitializeFaction");
        return targets;
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var transformed = 0;
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (IsHistoricStringExpanderCall(instruction))
            {
                transformed++;
                yield return new CodeInstruction(
                    OpCodes.Call,
                    AccessTools.Method(typeof(DimensionManagerGeneratedNameTranslationPatch), nameof(TranslateExpandedText)));
            }
        }

        if (transformed == 0)
        {
            Trace.TraceWarning("QudJP: {0}.Transpiler found no HistoricStringExpander.ExpandString calls.", Context);
        }
    }

    public static void Postfix(object? __result)
    {
        try
        {
            TranslateStringMember(__result, "cultForm", "CultForm");
            TranslateStringMember(__result, "dimensionName", "DimensionName");
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }

    public static string TranslateExpandedText(string source)
    {
        try
        {
            if (!DimensionManagerGeneratedNameTranslator.TryTranslateExpandedText(source, out var translated))
            {
                return translated;
            }

            DynamicTextObservability.RecordTransform(Context, Context + ".ExpandString", source, translated);
            return translated;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.TranslateExpandedText failed: {1}", Context, ex);
            return source;
        }
    }

    internal static void TranslateStringMember(object? instance, string memberName, string route)
    {
        if (instance is null)
        {
            return;
        }

        var source = GetMemberValue(instance, memberName) as string;
        if (!DimensionManagerGeneratedNameTranslator.TryTranslateStoredName(source, out var translated))
        {
            if (!string.Equals(source, translated, StringComparison.Ordinal))
            {
                SetStringMemberValue(instance, memberName, translated);
            }

            return;
        }

        if (SetStringMemberValue(instance, memberName, translated))
        {
            DynamicTextObservability.RecordTransform(Context, Context + "." + route, source ?? string.Empty, translated);
        }
    }

    private static void AddTarget(ICollection<MethodBase> targets, Type targetType, string methodName)
    {
        var method = AccessTools.Method(targetType, methodName, Type.EmptyTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0} target not found: {1}.{2}().", Context, targetType.FullName, methodName);
            return;
        }

        targets.Add(method);
    }

    internal static object? GetMemberValue(object? instance, string memberName)
    {
        if (instance is null)
        {
            return null;
        }

        var type = instance.GetType();
        var field = AccessTools.Field(type, memberName);
        if (field is not null)
        {
            return field.GetValue(instance);
        }

        var property = AccessTools.Property(type, memberName);
        return property is { CanRead: true } ? property.GetValue(instance) : null;
    }

    internal static bool SetStringMemberValue(object instance, string memberName, string value)
    {
        var type = instance.GetType();
        var field = AccessTools.Field(type, memberName);
        if (field is not null && field.FieldType == typeof(string))
        {
            field.SetValue(instance, value);
            return true;
        }

        var property = AccessTools.Property(type, memberName);
        if (property is { CanWrite: true } && property.PropertyType == typeof(string))
        {
            property.SetValue(instance, value);
            return true;
        }

        return false;
    }

    private static bool IsHistoricStringExpanderCall(CodeInstruction instruction)
    {
        return instruction.opcode == OpCodes.Call
            && instruction.operand is MethodInfo { ReturnType: var returnType, Name: "ExpandString" }
            && returnType == typeof(string);
    }
}
