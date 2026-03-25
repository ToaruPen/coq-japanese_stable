using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

/// <summary>
/// Translates UI text fields set by non-setData methods (Update, BeforeShow, HandleHighlight, etc.)
/// across 9 classes. Prerequisite for sink cutover (#103).
/// </summary>
[HarmonyPatch]
public static class SinkPrereqUiMethodTranslationPatch
{
    private const string Context = nameof(SinkPrereqUiMethodTranslationPatch);

    private static readonly (string TypeName, string MethodName)[] Targets =
    {
        ("XRL.UI.Framework.CategoryMenusScroller", "UpdateDescriptions"),
        ("XRL.UI.Framework.FrameworkScroller", "BeforeShow"),
        ("XRL.UI.Framework.HorizontalScroller", "BeforeShow"),
        ("XRL.UI.TitledIconButton", "Update"),
        ("Qud.UI.CyberneticsTerminalRow", "Update"),
        ("Qud.UI.AbilityManagerScreen", "HandleHighlightLeft"),
        ("Qud.UI.TradeScreen", "HandleHighlightObject"),
        ("MapScrollerPinItem", "SetData"),
        ("Qud.UI.PlayerStatusBar", "Update"),
    };

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();

        for (var index = 0; index < Targets.Length; index++)
        {
            var (typeName, methodName) = Targets[index];
            var type = AccessTools.TypeByName(typeName);
            if (type is null)
            {
                continue;
            }

            var method = ResolveMethod(type, typeName, methodName);

            if (method is not null)
            {
                targets.Add(method);
            }
            else
            {
                Trace.TraceWarning(
                    "QudJP: {0} failed to resolve {1}.{2}.",
                    Context, typeName, methodName);
            }
        }

        if (targets.Count == 0)
        {
            Trace.TraceError("QudJP: {0} resolved zero target methods. Patch will not apply.", Context);
        }

        return targets;
    }

    private static MethodInfo? ResolveMethod(Type type, string typeName, string methodName)
    {
        if (methodName == "BeforeShow")
        {
            return ResolveBeforeShowMethod(type);
        }

        if (methodName == "UpdateDescriptions" || methodName == "HandleHighlightLeft"
            || methodName == "HandleHighlightObject")
        {
            var dataElementType = AccessTools.TypeByName("XRL.UI.Framework.FrameworkDataElement");
            if (dataElementType is null)
            {
                Trace.TraceWarning("QudJP: {0} FrameworkDataElement type not found for {1}.{2}.", Context, typeName, methodName);
            }

            return dataElementType is not null
                ? AccessTools.Method(type, methodName, new[] { dataElementType })
                : AccessTools.Method(type, methodName);
        }

        if (methodName == "SetData")
        {
            return AccessTools.Method(type, methodName);
        }

        return AccessTools.Method(type, methodName, Type.EmptyTypes);
    }

    private static MethodInfo? ResolveBeforeShowMethod(Type type)
    {
        var descriptorType = AccessTools.TypeByName(
            "XRL.CharacterBuilds.EmbarkBuilderModuleWindowDescriptor");
        var elementType = AccessTools.TypeByName("XRL.UI.Framework.FrameworkDataElement");
        if (elementType is null)
        {
            Trace.TraceWarning("QudJP: {0} FrameworkDataElement type not found for BeforeShow.", Context);
            return AccessTools.Method(type, "BeforeShow");
        }

        var enumerableType = typeof(IEnumerable<>).MakeGenericType(elementType);
        return descriptorType is not null
            ? AccessTools.Method(type, "BeforeShow", new[] { descriptorType, enumerableType })
            : AccessTools.Method(type, "BeforeShow");
    }

    private static readonly string[] TextSkinFieldNames =
    {
        "selectedTitleText", "selectedDescriptionText",
        "titleText", "descriptionText", "TitleText",
        "description", "rightSideHeaderText", "rightSideDescriptionArea",
        "detailsRightText", "detailsLeftText", "detailsText", "ZoneText",
    };

    public static void Postfix(object __instance)
    {
        try
        {
            for (var index = 0; index < TextSkinFieldNames.Length; index++)
            {
                SinkPrereqTextFieldTranslator.TranslateField(
                    __instance, TextSkinFieldNames[index], Context);
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }
}
