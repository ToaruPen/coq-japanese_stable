using System;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

internal static class WorldPartGeneratedDisplayNameTranslator
{
    private static readonly Regex TombCultistPattern = new(
        "^(?<base>.+?) and death pilgrim of the (?<cult>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static void TranslateRenderDisplayName(object? owner, string route, string family)
    {
        if (owner is null)
        {
            return;
        }

        var render = UiBindingTranslationHelpers.GetMemberValue(owner, "Render");
        TranslateStringMember(render, "DisplayName", route, family, GetDisplayNameRouteTranslator.TranslatePreservingColors);
    }

    public static void TranslateParentRenderDisplayName(object? part, string route, string family)
    {
        if (part is null)
        {
            return;
        }

        var parent = UiBindingTranslationHelpers.GetMemberValue(part, "ParentObject");
        if (parent is null)
        {
            return;
        }

        TranslateRenderDisplayName(parent, route, family);
    }

    public static void TranslateTombCultistDisplayName(object? go, string route, string family)
    {
        TranslateStringMember(go, "DisplayName", route, family, TranslateTombCultistName);
    }

    private static void TranslateStringMember(
        object? instance,
        string memberName,
        string route,
        string family,
        Func<string, string?, string> translate)
    {
        if (instance is null)
        {
            return;
        }

        var source = UiBindingTranslationHelpers.GetStringMemberValue(instance, memberName);
        if (string.IsNullOrEmpty(source))
        {
            return;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source!, out var markedText))
        {
            UiBindingTranslationHelpers.SetMemberValue(instance, memberName, markedText);
            return;
        }

        var translated = translate(source!, route);
        if (string.Equals(translated, source, StringComparison.Ordinal))
        {
            return;
        }

        UiBindingTranslationHelpers.SetMemberValue(instance, memberName, translated);
        DynamicTextObservability.RecordTransform(route, family, source!, translated);
    }

    private static string TranslateTombCultistName(string source, string? route)
    {
        var match = TombCultistPattern.Match(source);
        if (!match.Success)
        {
            return source;
        }

        var baseName = GetDisplayNameRouteTranslator.TranslatePreservingColors(match.Groups["base"].Value, route);
        return match.Groups["cult"].Value + "の死の巡礼者、" + baseName;
    }
}

[HarmonyPatch]
public static class ModQuantumReverbDisplayNameTranslationPatch
{
    internal const string Context = nameof(ModQuantumReverbDisplayNameTranslationPatch);
    private const string Family = "WorldPartGeneratedDisplayName.Hologram";

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName("XRL.World.Parts.ModQuantumReverb");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var method = targetType is null || gameObjectType is null
            ? null
            : AccessTools.Method(targetType, "CreateHologramOf", [gameObjectType]);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0} target not found: ModQuantumReverb.CreateHologramOf(GameObject).", Context);
        }

        return method;
    }

    public static void Postfix(object? __result)
    {
        try
        {
            WorldPartGeneratedDisplayNameTranslator.TranslateRenderDisplayName(__result, Context, Family);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }
}

[HarmonyPatch]
public static class RandomStatueDisplayNameTranslationPatch
{
    internal const string Context = nameof(RandomStatueDisplayNameTranslationPatch);
    private const string Family = "WorldPartGeneratedDisplayName.Statue";

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName("XRL.World.Parts.RandomStatue");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var method = targetType is null || gameObjectType is null
            ? null
            : AccessTools.Method(targetType, "SetCreature", [gameObjectType]);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0} target not found: RandomStatue.SetCreature(GameObject).", Context);
        }

        return method;
    }

    public static void Postfix(object? __instance)
    {
        try
        {
            WorldPartGeneratedDisplayNameTranslator.TranslateParentRenderDisplayName(__instance, Context, Family);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }
}

[HarmonyPatch]
public static class PetPhylacteryDisplayNameTranslationPatch
{
    internal const string Context = nameof(PetPhylacteryDisplayNameTranslationPatch);
    private const string Family = "WorldPartGeneratedDisplayName.PetPhylactery";

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName("XRL.World.Parts.PetPhylactery");
        var eventType = AccessTools.TypeByName("XRL.World.AfterObjectCreatedEvent");
        var method = targetType is null || eventType is null
            ? null
            : AccessTools.Method(targetType, "HandleEvent", [eventType]);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0} target not found: PetPhylactery.HandleEvent(AfterObjectCreatedEvent).", Context);
        }

        return method;
    }

    public static void Postfix(object? __instance)
    {
        try
        {
            WorldPartGeneratedDisplayNameTranslator.TranslateParentRenderDisplayName(__instance, Context, Family);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }
}

[HarmonyPatch]
public static class TombCultistTemplateDisplayNameTranslationPatch
{
    internal const string Context = nameof(TombCultistTemplateDisplayNameTranslationPatch);
    private const string Family = "WorldPartGeneratedDisplayName.TombCultist";

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName("XRL.World.Parts.TombCultistTemplate");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var snapshotType = AccessTools.TypeByName("HistoryKit.HistoricEntitySnapshot");
        var method = targetType is null || gameObjectType is null || snapshotType is null
            ? null
            : AccessTools.Method(targetType, "Apply", [gameObjectType, snapshotType]);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0} target not found: TombCultistTemplate.Apply(GameObject, HistoricEntitySnapshot).", Context);
        }

        return method;
    }

    public static void Postfix(object? GO)
    {
        try
        {
            WorldPartGeneratedDisplayNameTranslator.TranslateTombCultistDisplayName(GO, Context, Family);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }
}
