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
        if (IsDirectMarkerStripOnly(source!, translated))
        {
            return true;
        }

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

        if (IsDirectMarkerStripOnly(source!, translated))
        {
            return true;
        }

        DynamicTextObservability.RecordTransform(Context, Family + "." + memberName, source!, translated);
        return true;
    }

    private static bool IsDirectMarkerStripOnly(string source, string translated)
    {
        return MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText)
            && string.Equals(markedText, translated, StringComparison.Ordinal);
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
