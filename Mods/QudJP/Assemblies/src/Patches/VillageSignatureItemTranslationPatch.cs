using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class VillageSignatureItemTranslationPatch
{
    private const string Context = nameof(VillageSignatureItemTranslationPatch);
    private const string Family = "VillageSignatureItem.HistoricObjectDisplayName";

    private static readonly Regex OldestPattern = new(
        "^(?:the )?oldest (?<item>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex PurestPattern = new(
        "^(?:the )?purest (?<item>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex OfOwnerPattern = new(
        "^the (?<item>.+) of (?<owner>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>(capacity: 2);
        AddNoArgumentTarget(targets, "XRL.World.ZoneBuilders.VillageBase", "generateSignatureItems");
        AddNoArgumentTarget(targets, "XRL.World.ZoneBuilders.VillageCodaBase", "generateSignatureItems");
        return targets;
    }

    public static void Postfix(object? __instance)
    {
        try
        {
            TranslateSignatureHistoricObject(__instance);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }

    internal static bool TryTranslateSignatureHistoricObjectName(string source, out string translated)
    {
        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out translated))
        {
            return true;
        }

        if (TryTranslateOldest(source, out translated)
            || TryTranslatePurest(source, out translated)
            || TryTranslateOfOwner(source, out translated))
        {
            return true;
        }

        translated = source;
        return false;
    }

    private static void TranslateSignatureHistoricObject(object? owner)
    {
        if (owner is null)
        {
            return;
        }

        var historicObject = UiBindingTranslationHelpers.GetMemberValue(owner, "signatureHistoricObjectInstance");
        if (historicObject is null)
        {
            return;
        }

        var source = UiBindingTranslationHelpers.GetStringMemberValue(historicObject, "DisplayName");
        if (!string.IsNullOrEmpty(source)
            && MessageFrameTranslator.TryStripDirectTranslationMarker(source!, out var directTranslated)
            && !string.Equals(directTranslated, source, StringComparison.Ordinal))
        {
            UiBindingTranslationHelpers.SetMemberValue(historicObject, "DisplayName", directTranslated);
            return;
        }

        if (string.IsNullOrEmpty(source)
            || !TryTranslateSignatureHistoricObjectName(source!, out var translated)
            || string.Equals(translated, source, StringComparison.Ordinal))
        {
            return;
        }

        UiBindingTranslationHelpers.SetMemberValue(historicObject, "DisplayName", translated);
        DynamicTextObservability.RecordTransform(Context, Family, source!, translated);
    }

    private static bool TryTranslateOldest(string source, out string translated)
    {
        var match = OldestPattern.Match(source);
        if (!match.Success || !TryTranslateItem(match.Groups["item"].Value, out var item))
        {
            translated = source;
            return false;
        }

        translated = "最古の" + item;
        return true;
    }

    private static bool TryTranslatePurest(string source, out string translated)
    {
        var match = PurestPattern.Match(source);
        if (!match.Success || !TryTranslateItem(match.Groups["item"].Value, out var item))
        {
            translated = source;
            return false;
        }

        translated = "最も純粋な" + item;
        return true;
    }

    private static bool TryTranslateOfOwner(string source, out string translated)
    {
        var match = OfOwnerPattern.Match(source);
        if (!match.Success || !TryTranslateItem(match.Groups["item"].Value, out var item))
        {
            translated = source;
            return false;
        }

        translated = TranslateOwner(match.Groups["owner"].Value) + "の" + item;
        return true;
    }

    private static bool TryTranslateItem(string source, out string translated)
    {
        translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(source, Context);
        return !string.Equals(translated, source, StringComparison.Ordinal);
    }

    private static string TranslateOwner(string source)
    {
        if (HistorySpiceComponentLookup.TryTranslateTitlePhrase(source, out var title))
        {
            return title;
        }

        return source;
    }

    private static void AddNoArgumentTarget(ICollection<MethodBase> targets, string typeName, string methodName)
    {
        var targetType = AccessTools.TypeByName(typeName);
        var method = targetType is null ? null : AccessTools.Method(targetType, methodName, Type.EmptyTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0} target not found: {1}.{2}().", Context, typeName, methodName);
            return;
        }

        targets.Add(method);
    }
}
