using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class CharGenChromeTranslationPatch
{
    private const string Context = nameof(CharGenChromeTranslationPatch);

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var frameworkScrollerType = AccessTools.TypeByName("XRL.UI.Framework.FrameworkScroller");
        var descriptorType = AccessTools.TypeByName("XRL.CharacterBuilds.EmbarkBuilderModuleWindowDescriptor");
        var elementType = AccessTools.TypeByName("XRL.UI.Framework.FrameworkDataElement");

        if (frameworkScrollerType is null)
        {
            Trace.TraceWarning("QudJP: {0} target type 'XRL.UI.Framework.FrameworkScroller' not found.", Context);
        }

        if (descriptorType is null)
        {
            Trace.TraceWarning("QudJP: {0} target type 'XRL.CharacterBuilds.EmbarkBuilderModuleWindowDescriptor' not found.", Context);
        }

        if (elementType is null)
        {
            Trace.TraceWarning("QudJP: {0} target type 'XRL.UI.Framework.FrameworkDataElement' not found.", Context);
        }

        if (frameworkScrollerType is not null && descriptorType is not null && elementType is not null)
        {
            var enumerableType = typeof(IEnumerable<>).MakeGenericType(elementType);
            var beforeShow = AccessTools.Method(frameworkScrollerType, "BeforeShow", new[] { descriptorType, enumerableType });
            if (beforeShow is null)
            {
                Trace.TraceWarning(
                    "QudJP: {0} method 'BeforeShow({1}, {2})' not found on '{3}'.",
                    Context,
                    descriptorType.FullName,
                    enumerableType.FullName,
                    frameworkScrollerType.FullName);
            }
            else
            {
                yield return beforeShow;
            }
        }

        var categoryMenuControllerType = AccessTools.TypeByName("XRL.UI.Framework.CategoryMenuController");
        if (categoryMenuControllerType is null)
        {
            Trace.TraceWarning("QudJP: {0} target type 'XRL.UI.Framework.CategoryMenuController' not found.", Context);
        }

        if (categoryMenuControllerType is not null && elementType is not null)
        {
            var setData = AccessTools.Method(categoryMenuControllerType, "setData", new[] { elementType });
            if (setData is null)
            {
                Trace.TraceWarning(
                    "QudJP: {0} method 'setData({1})' not found on '{2}'.",
                    Context,
                    elementType.FullName,
                    categoryMenuControllerType.FullName);
            }
            else
            {
                yield return setData;
            }
        }
    }

    public static void Prefix(object[] __args, MethodBase __originalMethod)
    {
        try
        {
            if (__args.Length == 0 || __args[0] is null)
            {
                return;
            }

            if (string.Equals(__originalMethod.Name, "BeforeShow", StringComparison.Ordinal))
            {
                CharGenProducerTranslationHelpers.TranslateStringMember(__args[0], "title", Context);
                return;
            }

            if (string.Equals(__originalMethod.Name, "setData", StringComparison.Ordinal))
            {
                CharGenProducerTranslationHelpers.TranslateStringMember(__args[0], "Title", Context);
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Prefix failed: {1}", Context, ex);
        }
    }
}
