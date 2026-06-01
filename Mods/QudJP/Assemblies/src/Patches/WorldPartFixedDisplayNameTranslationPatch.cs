using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class WorldPartFixedDisplayNameTranslationPatch
{
    internal const string Context = nameof(WorldPartFixedDisplayNameTranslationPatch);
    internal const string Family = Context + ".FixedLeaves";

    private static readonly IReadOnlyDictionary<string, string> FixedTextTranslations =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Bey Lah"] = "ベイ・ラー",
            ["Hydropon"] = "ハイドロポン",
            ["molting basilisk husk"] = "脱皮中のバジリスクの抜け殻",
            ["molting basilisk"] = "脱皮中のバジリスク",
            ["At the center of a particularly thick copse, the vegetation clears. Flower-bedecked huts huddle in the clearing within, surrounded by phalanxes of tidy watervine rows and carefully-tended lah."] =
                "濃い林の中心で草木が開けている。花で飾られた小屋がその空き地に寄り集まり、整然としたウォーターヴァインの列とよく手入れされたラーに囲まれている。",
            ["It's the hydropon."] = "ここがハイドロポンだ。",
            ["The sloughed off skin is dull quartz and statuesque."] =
                "脱ぎ捨てられた皮は鈍い水晶のようで、彫像めいている。",
            ["A lizard of quartz scales reposes in the stillness of an artist's mould. When prey gets too comfortable with =pronouns.possessive= lifelessness and traipeses by, =pronouns.subjective= =verb:quicken:afterpronoun= and snaps like a thunder clap."] =
                "水晶の鱗を持つトカゲが、芸術家の型の中にいるような静けさで横たわっている。獲物がその死んだような様子に油断して通り過ぎると、=pronouns.subjective=は=verb:quicken:afterpronoun=して雷鳴のように噛みつく。",
        };

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>(capacity: 3);
        AddEventTarget(targets, "XRL.World.Parts.BeyLahTerrain", "FireEvent");
        AddEventTarget(targets, "XRL.World.Parts.HydroponTerrain", "FireEvent");
        AddNoArgumentTarget(targets, "XRL.World.Parts.MoltingBasilisk", "SyncState");
        return targets;
    }

    public static void Postfix(object? __instance)
    {
        try
        {
            TranslateOwner(__instance);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }

    private static void TranslateOwner(object? instance)
    {
        if (instance is null)
        {
            return;
        }

        var parent = GetMemberValue(instance, "ParentObject");
        if (parent is null)
        {
            return;
        }

        var render = GetMemberValue(parent, "Render");
        TranslateStringMember(render, "DisplayName");
        TranslateStringMember(parent, "DisplayName");

        var description = GetDescriptionPart(parent);
        TranslateStringMember(description, "Short");
    }

    private static object? GetDescriptionPart(object parent)
    {
        var type = parent.GetType();
        var getPartByName = AccessTools.Method(type, "GetPart", [typeof(string)]);
        if (getPartByName is not null)
        {
            var description = getPartByName.Invoke(parent, ["Description"]);
            if (description is not null)
            {
                return description;
            }
        }

        return GetMemberValue(parent, "Description");
    }

    private static void TranslateStringMember(object? instance, string memberName)
    {
        if (instance is null)
        {
            return;
        }

        var source = GetMemberValue(instance, memberName) as string;
        if (string.IsNullOrEmpty(source))
        {
            return;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source!, out var markedText))
        {
            _ = SetMemberValue(instance, memberName, markedText);
            return;
        }

        var translated = ColorAwareTranslationComposer.TranslatePreservingColors(
            source!,
            visible => FixedTextTranslations.TryGetValue(visible, out var fixedTranslation)
                ? fixedTranslation
                : visible);
        if (string.Equals(source, translated, StringComparison.Ordinal))
        {
            return;
        }

        if (SetMemberValue(instance, memberName, translated))
        {
            DynamicTextObservability.RecordTransform(Context, Family, source!, translated);
        }
    }

    private static void AddEventTarget(ICollection<MethodBase> targets, string typeName, string methodName)
    {
        var targetType = AccessTools.TypeByName(typeName);
        var eventType = AccessTools.TypeByName("XRL.World.Event");
        var method = targetType is null || eventType is null
            ? null
            : AccessTools.Method(targetType, methodName, [eventType]);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0} target not found: {1}.{2}(Event).", Context, typeName, methodName);
            return;
        }

        targets.Add(method);
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

    private static object? GetMemberValue(object instance, string memberName)
    {
        var type = instance.GetType();
        var property = AccessTools.Property(type, memberName);
        if (property is not null)
        {
            return property.GetValue(instance);
        }

        return AccessTools.Field(type, memberName)?.GetValue(instance);
    }

    private static bool SetMemberValue(object instance, string memberName, object? value)
    {
        var type = instance.GetType();
        var property = AccessTools.Property(type, memberName);
        if (property is not null && property.CanWrite)
        {
            property.SetValue(instance, value);
            return true;
        }

        var field = AccessTools.Field(type, memberName);
        if (field is null)
        {
            return false;
        }

        field.SetValue(instance, value);
        return true;
    }
}
