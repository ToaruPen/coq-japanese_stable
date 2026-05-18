using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class PsychicHunterGeneratedTitleTranslationPatch
{
    private const string Context = nameof(PsychicHunterGeneratedTitleTranslationPatch);

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>(capacity: 4);
        var targetType = AccessTools.TypeByName("XRL.PsychicHunterSystem");
        var zoneType = AccessTools.TypeByName("XRL.World.Zone");
        if (targetType is null || zoneType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            return targets;
        }

        AddTarget(targets, targetType, "CreateSeekerHunters", typeof(int), zoneType);
        AddTarget(targets, targetType, "CreateExtradimensionalSoloHunters", zoneType, typeof(int), typeof(List<>));
        AddTarget(targets, targetType, "CreateExtradimensionalSoloDeviant", zoneType);
        AddTarget(targets, targetType, "CreateExtradimensionalCultHunters", zoneType, typeof(int), typeof(List<>));
        return targets;
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var expandedTextTransforms = 0;
        var addTitleTransforms = 0;
        foreach (var instruction in instructions)
        {
            if (IsTitlesAddTitleCall(instruction))
            {
                addTitleTransforms++;
                yield return new CodeInstruction(
                    OpCodes.Call,
                    AccessTools.Method(typeof(PsychicHunterGeneratedTitleTranslationPatch), nameof(AddTranslatedTitle)));
                continue;
            }

            yield return instruction;
            if (IsHistoricStringExpanderCall(instruction))
            {
                expandedTextTransforms++;
                yield return new CodeInstruction(
                    OpCodes.Call,
                    AccessTools.Method(typeof(PsychicHunterGeneratedTitleTranslationPatch), nameof(TranslateExpandedText)));
            }
        }

        if (expandedTextTransforms == 0 || addTitleTransforms == 0)
        {
            Trace.TraceWarning(
                "QudJP: {0}.Transpiler transformed ExpandString={1}, AddTitle={2}.",
                Context,
                expandedTextTransforms,
                addTitleTransforms);
        }
    }

    public static string TranslateExpandedText(string source)
    {
        try
        {
            if (!PsychicHunterGeneratedTitleTranslator.TryTranslateExpandedText(source, out var translated))
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

    public static void AddTranslatedTitle(object titles, string title, int order)
    {
        try
        {
            var source = title;
            if (!PsychicHunterGeneratedTitleTranslator.TryTranslateTitle(title, out var translated))
            {
                InvokeAddTitle(titles, translated, order);
                return;
            }

            DynamicTextObservability.RecordTransform(Context, Context + ".AddTitle", source, translated);
            InvokeAddTitle(titles, translated, order);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.AddTranslatedTitle failed: {1}", Context, ex);
            try
            {
                InvokeAddTitle(titles, title, order);
            }
            catch (Exception fallbackEx)
            {
                Trace.TraceError(
                    "QudJP: {0}.AddTranslatedTitle fallback failed for title '{1}' order {2}: {3}",
                    Context,
                    title,
                    order,
                    fallbackEx);
            }
        }
    }

    private static void AddTarget(ICollection<MethodBase> targets, Type targetType, string methodName, params Type[] parameterTypes)
    {
        var method = ResolveMethod(targetType, methodName, parameterTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0} target not found: {1}.{2}.", Context, targetType.FullName, methodName);
            return;
        }

        targets.Add(method);
    }

    private static MethodInfo? ResolveMethod(Type targetType, string methodName, Type[] parameterTypes)
    {
        foreach (var method in AccessTools.GetDeclaredMethods(targetType))
        {
            if (!string.Equals(method.Name, methodName, StringComparison.Ordinal))
            {
                continue;
            }

            var parameters = method.GetParameters();
            if (parameters.Length < parameterTypes.Length)
            {
                continue;
            }

            var matches = true;
            for (var index = 0; index < parameterTypes.Length; index++)
            {
                var expected = parameterTypes[index];
                if (expected.IsGenericTypeDefinition)
                {
                    matches &= parameters[index].ParameterType.IsGenericType
                        && parameters[index].ParameterType.GetGenericTypeDefinition() == expected;
                }
                else
                {
                    matches &= parameters[index].ParameterType == expected;
                }
            }

            if (matches)
            {
                return method;
            }
        }

        return null;
    }

    private static void InvokeAddTitle(object titles, string title, int order)
    {
        var method = AccessTools.Method(titles.GetType(), "AddTitle", [typeof(string), typeof(int)]);
        if (method is null)
        {
            Trace.TraceWarning("QudJP: {0}.InvokeAddTitle could not resolve AddTitle on '{1}'.", Context, titles.GetType().FullName);
            return;
        }

        method.Invoke(titles, [title, order]);
    }

    private static bool IsHistoricStringExpanderCall(CodeInstruction instruction)
    {
        return instruction.opcode == OpCodes.Call
            && instruction.operand is MethodInfo { ReturnType: var returnType, Name: "ExpandString" }
            && returnType == typeof(string);
    }

    private static bool IsTitlesAddTitleCall(CodeInstruction instruction)
    {
        return instruction.opcode == OpCodes.Callvirt
            && instruction.operand is MethodInfo { Name: "AddTitle" } method
            && method.GetParameters() is { Length: 2 } parameters
            && parameters[0].ParameterType == typeof(string)
            && parameters[1].ParameterType == typeof(int)
            && string.Equals(method.DeclaringType?.FullName, "XRL.World.Parts.Titles", StringComparison.Ordinal);
    }
}
