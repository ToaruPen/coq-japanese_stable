using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

internal static class GeneratedDisplayNameOwnerTranslationHelpers
{
    internal const string Context = "GeneratedDisplayNameOwnerTranslationPatch";
    internal const string Family = Context + ".DisplayName";

    private static readonly Regex RecoilerDisplayNamePattern = new(
        "^(?<destination>.+?) recoiler$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex PlayerCopyDescriptionPattern = new(
        "^one of your (?<context>.+?) clones$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal static bool TranslateVillageFactionDisplayName(object? faction)
    {
        return TranslateStringMember(faction, "DisplayName", TranslateGeneratedDisplayName);
    }

    internal static bool TranslateTemporalFugueCopy(object? gameObject)
    {
        var changed = TranslateRenderDisplayName(gameObject);
        changed |= TranslateStringProperty(
            gameObject,
            "PlayerCopyDescription",
            TranslatePlayerCopyDescription);
        return changed;
    }

    internal static bool TranslateMuralCells(object? muralCells)
    {
        if (muralCells is not IEnumerable cells)
        {
            return false;
        }

        var changed = false;
        foreach (var cell in cells)
        {
            var mural = InvokeGetFirstObjectWithPart(cell, "SultanMural");
            changed |= TranslateDisplayName(mural);
        }

        return changed;
    }

    internal static bool TranslatePlayerMuralPanel(object? instance, object? muralCells, int panel)
    {
        if (instance is null || muralCells is not IEnumerable cells || panel < 0)
        {
            return false;
        }

        var parent = GetMemberValue(instance, "ParentObject");
        var zone = GetMemberValue(parent, "CurrentZone");
        if (zone is null)
        {
            return false;
        }

        var index = 0;
        foreach (var location in cells)
        {
            if (index++ != panel)
            {
                continue;
            }

            var cell = InvokeMethod(zone, "GetCell", location);
            var mural = InvokeGetFirstObjectWithPart(cell, "SultanMural");
            return TranslateDisplayName(mural);
        }

        return false;
    }

    internal static bool TranslateRewardGameObject(object? gameObject)
    {
        return TranslateRenderDisplayName(gameObject);
    }

    private static bool TranslateDisplayName(object? target)
    {
        var render = GetMemberValue(target, "Render");
        if (render is not null
            && !string.IsNullOrEmpty(GetMemberValue(render, "DisplayName") as string))
        {
            return TranslateStringMember(render, "DisplayName", TranslateGeneratedDisplayName);
        }

        return TranslateStringMember(target, "DisplayName", TranslateGeneratedDisplayName);
    }

    private static bool TranslateRenderDisplayName(object? gameObject)
    {
        var render = GetMemberValue(gameObject, "Render");
        return TranslateStringMember(render, "DisplayName", TranslateGeneratedDisplayName);
    }

    private static bool TranslateStringProperty(
        object? target,
        string propertyName,
        Func<string, string?> translator)
    {
        if (target is null)
        {
            return false;
        }

        var getStringProperty = AccessTools.Method(target.GetType(), "GetStringProperty", [typeof(string)]);
        var setStringProperty = AccessTools.Method(target.GetType(), "SetStringProperty", [typeof(string), typeof(string), typeof(bool)]);
        if (getStringProperty is null || setStringProperty is null)
        {
            return false;
        }

        var source = getStringProperty.Invoke(target, [propertyName]) as string;
        if (string.IsNullOrEmpty(source))
        {
            return false;
        }

        var translated = translator(source!);
        if (translated is null || string.Equals(source, translated, StringComparison.Ordinal))
        {
            return false;
        }

        setStringProperty.Invoke(target, [propertyName, translated, false]);
        DynamicTextObservability.RecordTransform(Context, Family + "." + propertyName, source!, translated);
        return true;
    }

    private static bool TranslateStringMember(
        object? target,
        string memberName,
        Func<string, string?> translator)
    {
        if (target is null)
        {
            return false;
        }

        var source = GetMemberValue(target, memberName) as string;
        if (string.IsNullOrEmpty(source))
        {
            return false;
        }

        var translated = translator(source!);
        if (translated is null || string.Equals(source, translated, StringComparison.Ordinal))
        {
            return false;
        }

        if (!SetMemberValue(target, memberName, translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(Context, Family + "." + memberName, source!, translated);
        return true;
    }

    private static string? TranslateGeneratedDisplayName(string source)
    {
        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            return markedText;
        }

        if (ImportedFoodOrDrinkFactionNameTranslator.TryTranslate(source, out var importedFactionName))
        {
            return importedFactionName;
        }

        var recoiler = RecoilerDisplayNamePattern.Match(source);
        if (recoiler.Success)
        {
            var destination = GetDisplayNameRouteTranslator.TranslatePreservingColors(
                recoiler.Groups["destination"].Value,
                Context);
            return destination + "のリコイラー";
        }

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(source, Context);
        return string.Equals(source, translated, StringComparison.Ordinal) ? null : translated;
    }

    private static string? TranslatePlayerCopyDescription(string source)
    {
        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            return markedText;
        }

        var match = PlayerCopyDescriptionPattern.Match(source);
        if (!match.Success)
        {
            return null;
        }

        var sourceContext = match.Groups["context"].Value;
        var context = TranslatePlayerCopyContext(sourceContext);
        if (string.Equals(sourceContext, context, StringComparison.Ordinal))
        {
            return null;
        }

        return "あなたの" + context + "のクローンの一人";
    }

    private static string TranslatePlayerCopyContext(string source)
    {
        var mutation = StatusScreenPopupTranslationPatch.TranslateMutationDisplayName(source);
        if (!string.Equals(source, mutation, StringComparison.Ordinal))
        {
            return mutation;
        }

        return GetDisplayNameRouteTranslator.TranslatePreservingColors(source, Context);
    }

    private static object? InvokeGetFirstObjectWithPart(object? cell, string partName)
    {
        return InvokeMethod(cell, "GetFirstObjectWithPart", partName);
    }

    private static object? InvokeMethod(object? target, string methodName, params object?[] args)
    {
        if (target is null)
        {
            return null;
        }

        var argTypes = new Type[args.Length];
        for (var i = 0; i < args.Length; i++)
        {
            argTypes[i] = args[i]?.GetType() ?? typeof(object);
        }

        var method = AccessTools.Method(target.GetType(), methodName, argTypes);
        if (method is null)
        {
            Trace.TraceWarning(
                "QudJP: {0} falling back to name-only method lookup for {1}.{2}.",
                Context,
                target.GetType().FullName,
                methodName);
            method = AccessTools.Method(target.GetType(), methodName);
        }

        return method?.Invoke(target, args);
    }

    private static object? GetMemberValue(object? target, string memberName)
    {
        if (target is null)
        {
            return null;
        }

        var type = target.GetType();
        var property = AccessTools.Property(type, memberName);
        if (property is not null)
        {
            return property.GetValue(target);
        }

        return AccessTools.Field(type, memberName)?.GetValue(target);
    }

    private static bool SetMemberValue(object target, string memberName, object? value)
    {
        var type = target.GetType();
        var property = AccessTools.Property(type, memberName);
        if (property is not null && property.CanWrite)
        {
            property.SetValue(target, value);
            return true;
        }

        var field = AccessTools.Field(type, memberName);
        if (field is null)
        {
            return false;
        }

        field.SetValue(target, value);
        return true;
    }
}

[HarmonyPatch]
public static class VillageBaseCreateVillageFactionDisplayNameTranslationPatch
{
    private const string Context = nameof(VillageBaseCreateVillageFactionDisplayNameTranslationPatch);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName("XRL.World.ZoneBuilders.VillageBase");
        var snapshotType = AccessTools.TypeByName("HistoryKit.HistoricEntitySnapshot");
        var method = targetType is null || snapshotType is null
            ? null
            : AccessTools.Method(targetType, "CreateVillageFaction", [snapshotType]);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0} target not found.", Context);
        }

        return method;
    }

    public static void Postfix(object? __result)
    {
        try
        {
            _ = GeneratedDisplayNameOwnerTranslationHelpers.TranslateVillageFactionDisplayName(__result);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }
}

[HarmonyPatch]
public static class TemporalFugueCreateFugueCopyDisplayNameTranslationPatch
{
    private const string Context = nameof(TemporalFugueCreateFugueCopyDisplayNameTranslationPatch);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName("XRL.World.Parts.Mutation.TemporalFugue");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var cellType = AccessTools.TypeByName("XRL.World.Cell");
        var partType = AccessTools.TypeByName("XRL.World.IPart");
        var method = targetType is null || gameObjectType is null || cellType is null || partType is null
            ? null
            : AccessTools.Method(
                targetType,
                "CreateFugueCopyOf",
                [
                    gameObjectType,
                    gameObjectType,
                    cellType,
                    gameObjectType,
                    typeof(bool),
                    typeof(int),
                    typeof(int),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    partType,
                ]);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0} target not found.", Context);
        }

        return method;
    }

    public static void Postfix(object? __result)
    {
        try
        {
            _ = GeneratedDisplayNameOwnerTranslationHelpers.TranslateTemporalFugueCopy(__result);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }
}

[HarmonyPatch]
public static class SultanMuralDisplayNameTranslationPatch
{
    private const string Context = nameof(SultanMuralDisplayNameTranslationPatch);

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>(capacity: 2);
        var targetType = AccessTools.TypeByName("XRL.World.Parts.SultanMuralController");
        var cellType = AccessTools.TypeByName("XRL.World.Cell");
        var eventType = AccessTools.TypeByName("HistoryKit.HistoricEvent");
        if (targetType is null || cellType is null || eventType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            return targets;
        }

        var listType = typeof(List<>).MakeGenericType(cellType);
        AddTarget(targets, targetType, "updateHistoricMural", listType, eventType);
        AddTarget(targets, targetType, "ruinMural", listType, eventType);
        return targets;
    }

    public static void Postfix(object? __0)
    {
        try
        {
            _ = GeneratedDisplayNameOwnerTranslationHelpers.TranslateMuralCells(__0);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }

    private static void AddTarget(ICollection<MethodBase> targets, Type targetType, string methodName, params Type[] parameters)
    {
        var method = AccessTools.Method(targetType, methodName, parameters);
        if (method is not null)
        {
            targets.Add(method);
            return;
        }

        Trace.TraceError("QudJP: {0} target not found: {1}.", Context, methodName);
    }
}

[HarmonyPatch]
public static class PlayerMuralDisplayNameTranslationPatch
{
    private const string Context = nameof(PlayerMuralDisplayNameTranslationPatch);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName("XRL.World.Parts.PlayerMuralController");
        var locationType = AccessTools.TypeByName("Genkit.Location2D");
        var accomplishmentType = AccessTools.TypeByName("Qud.API.JournalAccomplishment");
        var method = targetType is null || locationType is null || accomplishmentType is null
            ? null
            : AccessTools.Method(
                targetType,
                "updatePlayerMural",
                [typeof(List<>).MakeGenericType(locationType), accomplishmentType, typeof(int)]);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0} target not found.", Context);
        }

        return method;
    }

    public static void Postfix(object? __instance, object? __0, int __2)
    {
        try
        {
            _ = GeneratedDisplayNameOwnerTranslationHelpers.TranslatePlayerMuralPanel(__instance, __0, __2);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }
}

[HarmonyPatch]
public static class VillageDynamicQuestRewardDisplayNameTranslationPatch
{
    private const string Context = nameof(VillageDynamicQuestRewardDisplayNameTranslationPatch);

    [ThreadStatic]
    private static int activeDepth;

    internal static bool IsActive => activeDepth > 0;

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName("XRL.World.VillageDynamicQuestContext");
        var method = targetType is null ? null : AccessTools.Method(targetType, "getQuestReward", Type.EmptyTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0} target not found.", Context);
        }

        return method;
    }

    public static void Prefix()
    {
        try
        {
            activeDepth++;
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
            if (activeDepth > 0)
            {
                activeDepth--;
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Finalizer failed: {1}", Context, ex);
        }

        return __exception;
    }
}

[HarmonyPatch]
public static class VillageDynamicQuestRewardGameObjectTranslationPatch
{
    private const string Context = nameof(VillageDynamicQuestRewardGameObjectTranslationPatch);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName("XRL.World.DynamicQuestRewardElement_GameObject");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var constructor = targetType is null || gameObjectType is null
            ? null
            : AccessTools.Constructor(targetType, [gameObjectType]);
        if (constructor is null)
        {
            Trace.TraceError("QudJP: {0} target not found.", Context);
        }

        return constructor;
    }

    public static void Prefix(object? __0)
    {
        try
        {
            if (VillageDynamicQuestRewardDisplayNameTranslationPatch.IsActive)
            {
                _ = GeneratedDisplayNameOwnerTranslationHelpers.TranslateRewardGameObject(__0);
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Prefix failed: {1}", Context, ex);
        }
    }
}
