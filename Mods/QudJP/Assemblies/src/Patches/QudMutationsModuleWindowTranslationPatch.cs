using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class QudMutationsModuleWindowTranslationPatch
{
    private static readonly MethodInfo TranslateFormattedDescriptionMethod =
        AccessTools.Method(typeof(QudMutationsModuleWindowTranslationPatch), nameof(TranslateFormattedDescription))
        ?? throw new InvalidOperationException("TranslateFormattedDescription method not found.");

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType(
            "XRL.CharacterBuilds.Qud.UI.QudMutationsModuleWindow",
            "QudMutationsModuleWindow");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: QudMutationsModuleWindowTranslationPatch target type not found.");
            return null;
        }

        var method = AccessTools.Method(targetType, "UpdateControls", Type.EmptyTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: QudMutationsModuleWindowTranslationPatch.UpdateControls not found.");
        }

        return method;
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var rewritten = new List<CodeInstruction>();
        var injectedTranslation = false;

        foreach (var instruction in instructions)
        {
            rewritten.Add(instruction);

            if (IsFormatNodeDescriptionCall(instruction))
            {
                rewritten.Add(new CodeInstruction(OpCodes.Call, TranslateFormattedDescriptionMethod));
                injectedTranslation = true;
            }
        }

        if (!injectedTranslation)
        {
            Trace.TraceWarning(
                "QudJP: QudMutationsModuleWindowTranslationPatch.Transpiler could not find FormatNodeDescription call; leaving instructions unchanged.");
        }

        return rewritten;
    }

    public static void Postfix(object? __instance)
    {
        try
        {
            if (__instance is null)
            {
                return;
            }

            TranslateMenuOptions(__instance);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: QudMutationsModuleWindowTranslationPatch.Postfix failed: {0}", ex);
        }
    }

    public static string TranslateFormattedDescription(string source)
    {
        try
        {
            return ChargenStructuredTextTranslator.TranslateMutationMenuDescription(source);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: QudMutationsModuleWindowTranslationPatch.TranslateFormattedDescription failed: {0}", ex);
            return source;
        }
    }

    private static void TranslateMenuOptions(object instance)
    {
        var categoryMenusField = AccessTools.Field(instance.GetType(), "categoryMenus");
        if (categoryMenusField?.GetValue(instance) is not IEnumerable categoryMenus)
        {
            return;
        }

        foreach (var categoryMenu in categoryMenus)
        {
            if (categoryMenu is null)
            {
                continue;
            }

            if (!TryGetMemberValue(categoryMenu, "menuOptions", out var menuOptionsValue)
                || menuOptionsValue is not IEnumerable menuOptions)
            {
                continue;
            }

            foreach (var menuOption in menuOptions)
            {
                if (menuOption is null)
                {
                    continue;
                }

                TranslateMenuOption(menuOption);
            }
        }
    }

    private static void TranslateMenuOption(object menuOption)
    {
        if (!TryGetStringMemberValue(menuOption, "Id", out var mutationName)
            || string.IsNullOrWhiteSpace(mutationName))
        {
            return;
        }

        if (TryGetStringMemberValue(menuOption, "Description", out var description)
            && !string.IsNullOrEmpty(description))
        {
            var translatedDescription = ChargenStructuredTextTranslator.TranslateMutationMenuDescription(description!);
            if (!string.Equals(description, translatedDescription, StringComparison.Ordinal))
            {
                SetStringMemberValue(menuOption, "Description", translatedDescription);
            }
        }

        if (ChargenStructuredTextTranslator.TryTranslateMutationLongDescription(mutationName!, out var translatedLongDescription))
        {
            SetStringMemberValue(menuOption, "LongDescription", translatedLongDescription);
        }
    }

    private static bool IsFormatNodeDescriptionCall(CodeInstruction instruction)
    {
        if ((instruction.opcode != OpCodes.Call && instruction.opcode != OpCodes.Callvirt)
            || instruction.operand is not MethodInfo method)
        {
            return false;
        }

        return string.Equals(method.Name, "FormatNodeDescription", StringComparison.Ordinal)
               && method.ReturnType == typeof(string)
               && method.GetParameters().Length == 2;
    }

    private static bool TryGetMemberValue(object instance, string memberName, out object? value)
    {
        var type = instance.GetType();
        var field = AccessTools.Field(type, memberName);
        if (field is not null)
        {
            value = field.GetValue(instance);
            return true;
        }

        var property = AccessTools.Property(type, memberName);
        if (property is not null && property.CanRead)
        {
            value = property.GetValue(instance);
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryGetStringMemberValue(object instance, string memberName, out string? value)
    {
        value = null;
        if (!TryGetMemberValue(instance, memberName, out var raw))
        {
            return false;
        }

        value = raw as string;
        return true;
    }

    private static void SetStringMemberValue(object instance, string memberName, string value)
    {
        var type = instance.GetType();
        var field = AccessTools.Field(type, memberName);
        if (field is not null && field.FieldType == typeof(string))
        {
            field.SetValue(instance, value);
            return;
        }

        var property = AccessTools.Property(type, memberName);
        if (property is not null && property.CanWrite && property.PropertyType == typeof(string))
        {
            property.SetValue(instance, value);
        }
    }
}
