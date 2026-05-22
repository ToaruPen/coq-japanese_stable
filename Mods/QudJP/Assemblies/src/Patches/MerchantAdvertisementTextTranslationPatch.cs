using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class MerchantAdvertisementTextTranslationPatch
{
    private const string Context = nameof(MerchantAdvertisementTextTranslationPatch);
    private static FieldInfo? bookTitleField;
    private static FieldInfo? parentObjectField;
    private static PropertyInfo? parentObjectProperty;
    private static FieldInfo? parentDisplayNameField;
    private static PropertyInfo? parentDisplayNameProperty;

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType(
            "XRL.World.Parts.MerchantRevealer",
            "MerchantRevealer");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            return null;
        }

        var method = AccessTools.Method(targetType, "GenerateMerchantLocation", Type.EmptyTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.GenerateMerchantLocation() target not found.", Context);
        }

        return method;
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
                    AccessTools.Method(typeof(MerchantAdvertisementTextTranslationPatch), nameof(TranslateExpandedText)));
            }
        }

        if (transformed == 0)
        {
            Trace.TraceWarning("QudJP: {0}.Transpiler found no HistoricStringExpander.ExpandString calls.", Context);
        }
    }

    public static void Postfix(object? __instance)
    {
        try
        {
            TranslateGeneratedBookTitle(__instance);
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
            if (!MerchantAdvertisementTextTranslator.TryTranslate(source, out var translated))
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

    private static void TranslateGeneratedBookTitle(object? instance)
    {
        if (instance is null)
        {
            return;
        }

        var currentBookTitleField = bookTitleField;
        if (currentBookTitleField is null || currentBookTitleField.DeclaringType != instance.GetType())
        {
            currentBookTitleField = AccessTools.Field(instance.GetType(), "bookTitle");
            bookTitleField = currentBookTitleField;
        }

        var source = currentBookTitleField?.GetValue(instance) as string;
        if (string.IsNullOrEmpty(source)
            || !MerchantAdvertisementTextTranslator.TryTranslateBookTitle(source, out var translated)
            || string.Equals(translated, source, StringComparison.Ordinal))
        {
            return;
        }

        currentBookTitleField?.SetValue(instance, translated);
        TrySetParentDisplayName(instance, translated);
        DynamicTextObservability.RecordTransform(Context, Context + ".BookTitle", source!, translated);
    }

    private static void TrySetParentDisplayName(object instance, string translated)
    {
        var currentParentObjectField = parentObjectField;
        var currentParentObjectProperty = parentObjectProperty;
        if ((currentParentObjectField is null
                || currentParentObjectField.DeclaringType is null
                || !currentParentObjectField.DeclaringType.IsInstanceOfType(instance))
            && (currentParentObjectProperty is null
                || currentParentObjectProperty.DeclaringType is null
                || !currentParentObjectProperty.DeclaringType.IsInstanceOfType(instance)))
        {
            currentParentObjectProperty = AccessTools.Property(instance.GetType(), "ParentObject");
            currentParentObjectField = AccessTools.Field(instance.GetType(), "ParentObject");
            parentObjectProperty = currentParentObjectProperty;
            parentObjectField = currentParentObjectField;
            parentDisplayNameField = null;
            parentDisplayNameProperty = null;
        }

        var parentObject = currentParentObjectProperty?.CanRead == true
            ? currentParentObjectProperty.GetValue(instance)
            : currentParentObjectField?.GetValue(instance);
        if (parentObject is null)
        {
            return;
        }

        var currentParentDisplayNameProperty = parentDisplayNameProperty;
        if (currentParentDisplayNameProperty is null
            || currentParentDisplayNameProperty.DeclaringType != parentObject.GetType())
        {
            currentParentDisplayNameProperty = AccessTools.Property(parentObject.GetType(), "DisplayName");
            parentDisplayNameProperty = currentParentDisplayNameProperty;
        }

        if (currentParentDisplayNameProperty is not null && currentParentDisplayNameProperty.CanWrite)
        {
            currentParentDisplayNameProperty.SetValue(parentObject, translated);
            return;
        }

        var currentParentDisplayNameField = parentDisplayNameField;
        if (currentParentDisplayNameField is null || currentParentDisplayNameField.DeclaringType != parentObject.GetType())
        {
            currentParentDisplayNameField = AccessTools.Field(parentObject.GetType(), "DisplayName");
            parentDisplayNameField = currentParentDisplayNameField;
        }

        currentParentDisplayNameField?.SetValue(parentObject, translated);
    }

    private static bool IsHistoricStringExpanderCall(CodeInstruction instruction)
    {
        return instruction.opcode == OpCodes.Call
            && instruction.operand is MethodInfo { ReturnType: var returnType, Name: "ExpandString" }
            && returnType == typeof(string);
    }
}
