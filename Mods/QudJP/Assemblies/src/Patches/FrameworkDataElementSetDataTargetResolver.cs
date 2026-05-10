using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

internal static class FrameworkDataElementSetDataTargetResolver
{
    private const string FrameworkDataElementFullTypeName = "XRL.UI.Framework.FrameworkDataElement";
    private const string FrameworkDataElementSimpleTypeName = "FrameworkDataElement";

    internal static MethodBase? Resolve(string context, string targetTypeName, string targetSimpleTypeName)
    {
        var targetType = GameTypeResolver.FindType(targetTypeName, targetSimpleTypeName);
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", context);
            return null;
        }

        var frameworkDataElementType = GameTypeResolver.FindType(
            FrameworkDataElementFullTypeName,
            FrameworkDataElementSimpleTypeName);
        return Resolve(context, targetType, frameworkDataElementType);
    }

    internal static MethodBase? Resolve(string context, Type targetType, Type? frameworkDataElementType)
    {
        var method = frameworkDataElementType is null
            ? null
            : AccessTools.Method(targetType, "setData", new[] { frameworkDataElementType });
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.setData(FrameworkDataElement) not found.", context);
        }

        return method;
    }
}
