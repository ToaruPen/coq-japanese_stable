using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class PickTargetShowPickerTranslationPatch
{
    private const string Context = nameof(PickTargetShowPickerTranslationPatch);

    private static readonly Regex RangeFailurePattern = new(
        "^You must select a location within (?<range>\\d+) tiles!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [ThreadStatic]
    private static string? directMarkerPassThroughText;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var pickTargetType = FindGameType("XRL.UI.PickTarget");
        var pickStyleType = FindGameType("XRL.UI.PickTarget+PickStyle");
        var allowVisType = FindGameType("XRL.World.AllowVis");
        var gameObjectType = FindGameType("XRL.World.GameObject");
        var point2DType = FindGameType("Genkit.Point2D");
        if (pickTargetType is null
            || pickStyleType is null
            || allowVisType is null
            || gameObjectType is null
            || point2DType is null)
        {
            Trace.TraceError("QudJP: {0} target parameter types not found.", Context);
            return targets;
        }

        var gameObjectPredicateType = typeof(Predicate<>).MakeGenericType(gameObjectType);
        var nullablePoint2DType = typeof(Nullable<>).MakeGenericType(point2DType);
        var method = AccessTools.Method(
            pickTargetType,
            "ShowPicker",
            [
                pickStyleType,
                typeof(int),
                typeof(int),
                typeof(int),
                typeof(int),
                typeof(bool),
                allowVisType,
                gameObjectPredicateType,
                gameObjectPredicateType,
                gameObjectType,
                nullablePoint2DType,
                typeof(string),
                typeof(bool),
                typeof(bool),
            ]);
        if (method is not null)
        {
            targets.Add(method);
        }
        else
        {
            Trace.TraceError("QudJP: {0}.XRL.UI.PickTarget.ShowPicker target not found.", Context);
        }

        return targets;
    }

    public static void Prefix()
    {
        try
        {
            OwnerTranslationScope.Enter(ref activeDepth);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Prefix failed: {1}", Context, ex);
        }
    }

    public static Exception? Finalizer(Exception? __exception)
    {
        try
        {
            OwnerTranslationScope.Exit(ref activeDepth);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Finalizer failed: {1}", Context, ex);
        }

        return __exception;
    }

    internal static bool TryTranslatePopupMessage(string source, string route, string family, out string translated)
    {
        if (!OwnerTranslationScope.IsActive(activeDepth) || string.IsNullOrEmpty(source))
        {
            translated = source;
            return false;
        }

        if (PopupShowTranslationPatch.TryTranslateDirectMarkedOwnerPopup(
            source,
            ref directMarkerPassThroughText,
            out translated))
        {
            return true;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = RangeFailurePattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = match.Groups["range"].Value + "マス以内の場所を選択しなければならない！";
        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translated,
            spans,
            stripped.Length,
            source);
        DynamicTextObservability.RecordTransform(route, family + "." + Context + ".RangeFailure", source, translated);
        return true;
    }

    private static Type? FindGameType(string fullTypeName)
    {
        var assemblyType = FindTypeInAssemblyCSharp(fullTypeName);
        if (assemblyType is not null)
        {
            return assemblyType;
        }

        var accessToolsType = AccessTools.TypeByName(fullTypeName);
        if (accessToolsType is not null)
        {
            return accessToolsType;
        }

        return GameTypeResolver.FindType(fullTypeName, SimpleTypeName(fullTypeName));
    }

    private static Type? FindTypeInAssemblyCSharp(string fullTypeName)
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (var index = 0; index < assemblies.Length; index++)
        {
            if (!string.Equals(assemblies[index].GetName().Name, "Assembly-CSharp", StringComparison.Ordinal))
            {
                continue;
            }

            var type = assemblies[index].GetType(fullTypeName, throwOnError: false);
            if (type is not null)
            {
                return type;
            }
        }

        return null;
    }

    private static string SimpleTypeName(string typeName)
    {
        var separator = typeName.LastIndexOf('.');
        return separator >= 0 ? typeName.Substring(separator + 1) : typeName;
    }
}
