using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

/// <summary>
/// Translates UI text fields set by setData(FrameworkDataElement) across 9 framework classes.
/// Prerequisite for sink cutover (#103) — replaces UITextSkinTranslationPatch coverage for these sites.
/// </summary>
[HarmonyPatch]
public static class SinkPrereqSetDataTranslationPatch
{
    private const string Context = nameof(SinkPrereqSetDataTranslationPatch);

    private static readonly string[] TargetTypeNames =
    {
        "Qud.UI.LeftSideCategory",
        "XRL.UI.Framework.CategoryIconScroller",
        "XRL.UI.Framework.CategoryMenuController",
        "XRL.UI.Framework.FrameworkHeader",
        "XRL.UI.Framework.SummaryBlockControl",
        "XRL.UI.ObjectFinderLine",
        "Qud.UI.CharacterEffectLine",
        "Qud.UI.CharacterAttributeLine",
        "Qud.UI.TinkeringDetailsLine",
    };

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        for (var index = 0; index < TargetTypeNames.Length; index++)
        {
            var type = AccessTools.TypeByName(TargetTypeNames[index]);
            if (type is null)
            {
                continue;
            }

            var method = AccessTools.Method(type, "setData",
                new[] { AccessTools.TypeByName("XRL.UI.Framework.FrameworkDataElement") });
            if (method is null)
            {
                method = AccessTools.Method(type, "setData");
            }

            if (method is not null)
            {
                targets.Add(method);
            }
            else
            {
                Trace.TraceWarning(
                    "QudJP: {0} failed to resolve setData on {1}.",
                    Context, TargetTypeNames[index]);
            }
        }

        if (targets.Count == 0)
        {
            Trace.TraceError("QudJP: {0} resolved zero target methods. Patch will not apply.", Context);
        }

        return targets;
    }

    private static readonly string[] TextSkinFieldNames =
    {
        "text", "textSkin", "title", "attributeText",
        "descriptionText", "modDescriptionText", "modBitCostText",
        "requirementsHeaderText", "RightText", "ObjectDescription",
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

            SinkPrereqTextFieldTranslator.TranslateChainedField(
                __instance, "_scroller", "titleText", Context);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }
}
