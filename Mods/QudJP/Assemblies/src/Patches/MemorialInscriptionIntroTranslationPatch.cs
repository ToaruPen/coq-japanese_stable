using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class MemorialInscriptionIntroTranslationPatch
{
    private const string Context = nameof(MemorialInscriptionIntroTranslationPatch);

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>(capacity: 3);
        AddTarget(targets, "XRL.World.Parts.Tombstone", "GenerateTombstone");
        AddTarget(targets, "XRL.World.Parts.RachelsTombstone", "GenerateTombstone");
        AddTarget(targets, "XRL.World.Parts.EaterUrn", "GenerateUrn");
        return targets;
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var transformed = 0;
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (IsExpandStringCall(instruction))
            {
                transformed++;
                yield return new CodeInstruction(
                    OpCodes.Call,
                    AccessTools.Method(typeof(MemorialInscriptionIntroTranslationPatch), nameof(TranslateExpandedText)));
            }
        }

        if (transformed == 0)
        {
            Trace.TraceWarning("QudJP: {0}.Transpiler found no ExpandString calls.", Context);
        }
    }

    public static string TranslateExpandedText(string source)
    {
        try
        {
            if (!MemorialInscriptionIntroTranslator.TryTranslate(source, out var translated))
            {
                return translated;
            }

            DynamicTextObservability.RecordTransform(Context, Context + ".Intro", source, translated);
            return translated;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.TranslateExpandedText failed: {1}", Context, ex);
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

    private static bool IsExpandStringCall(CodeInstruction instruction)
    {
        return instruction.opcode == OpCodes.Call
            && instruction.operand is MethodInfo { ReturnType: var returnType, Name: "ExpandString" }
            && returnType == typeof(string);
    }
}
