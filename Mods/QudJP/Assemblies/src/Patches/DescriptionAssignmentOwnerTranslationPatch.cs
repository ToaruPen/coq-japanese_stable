using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class DescriptionAssignmentOwnerTranslationPatch
{
    private const string Context = nameof(DescriptionAssignmentOwnerTranslationPatch);
    private const string BiocapacitorFamily = "Biocapacitor.Description";
    private const string CamouflageFamily = "Camouflage.Description";
    private const string MechanimistLibrarianFamily = "MechanimistLibrarian.Description";
    private const string MovementCapabilitiesFamily = "GetMovementCapabilitiesEvent.Description";
    private const string WingsFamily = "Wings.BodyPart.Description";
    private const string BannerFamily = "Banner.Description";

    private const string MechanimistLibrarianShortDescription =
        "In the narthex of the Stilt, cloistered beneath a marble arch and close to =pronouns.possessive= Argent Fathers, =pronouns.subjective= =verb:muse:afterpronoun= over a tattered codex. =pronouns.Subjective==verb:'re:afterpronoun= safe here, but it wasn't always that way. As a youngling, =pronouns.possessive= own kind understood =pronouns.objective= little. Only when =pronouns.subjective= =verb:were:afterpronoun= gifted a copy of the Canticles Chromaic did =pronouns.subjective= learn comfort, or mirth, or reason. =pronouns.Possessive= journey to the Stilt took several years, but now that =pronouns.subjective==verb:'re:afterpronoun= here, Sheba =verb:seek= to consolidate all the learning of the ages tucked away in Qud's innumerable chrome nooks. Here, =pronouns.subjective= =verb:prepare:afterpronoun= a residence where pilgrims can study the wisdom of others and bring themselves nearer to the divinity of the Kasaphescence.";

    private const string MechanimistLibrarianTranslatedShortDescription =
        "聖堂の前廊で、大理石のアーチの下にこもり、=pronouns.possessive=銀なる父たちの近くで、=pronouns.subjective=はぼろぼろの写本に思いを巡らせている。=pronouns.Subjective=はここでは安全だが、常にそうだったわけではない。幼い頃、=pronouns.possessive=同族は=pronouns.objective=をほとんど理解しなかった。=pronouns.subjective=が『彩の聖歌』を授けられて初めて、=pronouns.subjective=は安らぎ、喜び、理性を知った。=pronouns.Possessive=大寺院への旅には数年を要したが、今ここにいるシェバは、クドの無数のクロームの隅にしまい込まれた時代の知を集約しようとしている。ここで=pronouns.subjective=は、巡礼者が他者の知恵を学び、カサフェセンスの神性へ近づくための住まいを整えている。";

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var biocapacitorType = AccessTools.TypeByName("XRL.World.Parts.Biocapacitor");
        var biocapacitorCtor = biocapacitorType is null
            ? null
            : AccessTools.Constructor(biocapacitorType, Type.EmptyTypes);
        if (biocapacitorCtor is not null)
        {
            yield return biocapacitorCtor;
        }
        else
        {
            Trace.TraceError("QudJP: {0} target constructor not found: Biocapacitor().", Context);
        }

        var mechanimistLibrarianType = AccessTools.TypeByName("XRL.World.Parts.MechanimistLibrarian");
        var mechanimistInitialize = mechanimistLibrarianType is null
            ? null
            : AccessTools.Method(mechanimistLibrarianType, "Initialize", Type.EmptyTypes);
        if (mechanimistInitialize is not null)
        {
            yield return mechanimistInitialize;
        }
        else
        {
            Trace.TraceError("QudJP: {0} target method not found: MechanimistLibrarian.Initialize().", Context);
        }

        foreach (var targetTypeName in new[]
                 {
                     "XRL.World.Parts.FoliageCamouflage",
                     "XRL.World.Parts.UrbanCamouflage",
                 })
        {
            var targetType = AccessTools.TypeByName(targetTypeName);
            var targetCtor = targetType is null
                ? null
                : AccessTools.Constructor(targetType, Type.EmptyTypes);
            if (targetCtor is not null)
            {
                yield return targetCtor;
            }
            else
            {
                Trace.TraceError("QudJP: {0} target constructor not found: {1}().", Context, targetTypeName);
            }
        }

        var movementCapabilitiesType = AccessTools.TypeByName("XRL.World.GetMovementCapabilitiesEvent");
        var activatedAbilityEntryType = AccessTools.TypeByName("XRL.World.Parts.ActivatedAbilityEntry");
        if (movementCapabilitiesType is not null && activatedAbilityEntryType is not null)
        {
            var addMethod = AccessTools.Method(
                movementCapabilitiesType,
                "Add",
                new[] { typeof(string), typeof(string), typeof(int), activatedAbilityEntryType, typeof(bool) });
            if (addMethod is not null)
            {
                yield return addMethod;
            }
            else
            {
                Trace.TraceError("QudJP: {0} target method not found: GetMovementCapabilitiesEvent.Add(...).", Context);
            }
        }
        else
        {
            Trace.TraceError("QudJP: {0} target type not found for GetMovementCapabilitiesEvent.Add(...).", Context);
        }
    }

    public static void Postfix(object __instance, MethodBase __originalMethod)
    {
        try
        {
            var declaringType = __originalMethod.DeclaringType?.FullName ?? string.Empty;
            if (string.Equals(declaringType, "XRL.World.Parts.Biocapacitor", StringComparison.Ordinal))
            {
                TranslateBiocapacitorForTests(__instance);
                return;
            }

            if (string.Equals(declaringType, "XRL.World.Parts.FoliageCamouflage", StringComparison.Ordinal)
                || string.Equals(declaringType, "XRL.World.Parts.UrbanCamouflage", StringComparison.Ordinal))
            {
                TranslateCamouflageForTests(__instance);
                return;
            }

            if (string.Equals(declaringType, "XRL.World.Parts.MechanimistLibrarian", StringComparison.Ordinal)
                && TryGetMemberValue(__instance, "ParentObject", out var parent)
                && parent is not null)
            {
                TranslateMechanimistLibrarianForTests(parent);
                return;
            }

            if (string.Equals(declaringType, "XRL.World.GetMovementCapabilitiesEvent", StringComparison.Ordinal))
            {
                TranslateMovementCapabilityDescriptionsForTests(__instance);
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }

    internal static void TranslateBiocapacitorForTests(object? target)
    {
        if (target is null)
        {
            return;
        }

        TranslateStringMember(target, "Description", BiocapacitorFamily, static source =>
            string.Equals(source, "biocapacitor", StringComparison.Ordinal) ? "生体コンデンサー" : source);
    }

    internal static void TranslateCamouflageForTests(object? target)
    {
        if (target is null)
        {
            return;
        }

        TranslateStringMember(target, "Description", CamouflageFamily, static source =>
        {
            return source switch
            {
                "Foliage camouflage: This item grants the wearer +=level= DV in foliage." =>
                    "植生迷彩: 着用者は植生の中で+=level= DVを得る。",
                "Urban camouflage: This item grants the wearer +=level= DV near trash and furniture." =>
                    "都市迷彩: 着用者はゴミや家具の近くで+=level= DVを得る。",
                _ => source,
            };
        });
    }

    internal static void TranslateMechanimistLibrarianForTests(object? target)
    {
        if (target is null)
        {
            return;
        }

        TranslateStringMember(target, "DisplayName", MechanimistLibrarianFamily + ".DisplayName", static source =>
            string.Equals(source, "Sheba Hagadias", StringComparison.Ordinal) ? "シェバ・ハガディアス" : source);

        var titles = GetPartOrMember(target, "Titles", "Titles");
        if (titles is not null)
        {
            TranslateStringMember(titles, "TitleList", MechanimistLibrarianFamily + ".Title", static source =>
                source.Replace("librarian of the Stilt", "大寺院の司書"));
        }

        var descriptionPart = GetPartOrMember(target, "Description", "DescriptionPart");
        if (descriptionPart is not null)
        {
            TranslateStringMember(descriptionPart, "Short", MechanimistLibrarianFamily + ".Short", static source =>
                string.Equals(source, MechanimistLibrarianShortDescription, StringComparison.Ordinal)
                    ? MechanimistLibrarianTranslatedShortDescription
                    : source);
        }
    }

    internal static void TranslateWingsPartForTests(object? target)
    {
        if (target is null)
        {
            return;
        }

        TranslateStringMember(target, "Description", WingsFamily, static source =>
            string.Equals(source, "Worn around Wings", StringComparison.Ordinal) ? "翼の周囲に着用する" : source);
    }

    internal static void TranslateBannerDescriptionForTests(object? banner, object? eventInstance)
    {
        string? source = null;
        if (banner is not null && TryGetStringMemberValue(banner, "Description", out var current))
        {
            source = current;
            TranslateStringMember(banner, "Description", BannerFamily, TranslateBannerText);
        }

        if (eventInstance is null
            || !TryGetMemberValue(eventInstance, "Postfix", out var raw)
            || raw is not StringBuilder postfix
            || postfix.Length == 0)
        {
            return;
        }

        var postfixSource = postfix.ToString();
        var translated = TranslateBannerText(postfixSource);
        if (string.Equals(translated, postfixSource, StringComparison.Ordinal))
        {
            return;
        }

        postfix.Clear().Append(translated);
        DynamicTextObservability.RecordTransform(Context, BannerFamily + ".Postfix", postfixSource, translated);

        if (banner is not null
            && source is not null
            && string.Equals(source, postfixSource, StringComparison.Ordinal))
        {
            TrySetStringMemberValue(banner, "Description", translated);
        }
    }

    internal static void TranslateMovementCapabilityDescriptionsForTests(object? target)
    {
        if (target is null
            || !TryGetMemberValue(target, "Descriptions", out var raw)
            || raw is not IList<string> descriptions)
        {
            return;
        }

        for (var index = 0; index < descriptions.Count; index++)
        {
            var current = descriptions[index];
            if (string.IsNullOrEmpty(current)
                || current.StartsWith("\u0001", StringComparison.Ordinal))
            {
                continue;
            }

            var translated = current
                .Replace("[attack]", "[攻撃]")
                .Replace("[toggled off]", "[オフ]")
                .Replace("[toggled on]", "[オン]");
            if (string.Equals(translated, current, StringComparison.Ordinal))
            {
                continue;
            }

            descriptions[index] = translated;
            DynamicTextObservability.RecordTransform(
                Context,
                MovementCapabilitiesFamily,
                current,
                translated);
        }
    }

    private static string TranslateBannerText(string source)
    {
        if (string.IsNullOrEmpty(source) || source.StartsWith("\u0001", StringComparison.Ordinal))
        {
            return source;
        }

        const string prefix = "Bestows the ";
        const string middle = " effect to ";
        const string suffix = " who can see this item.";
        if (!source.StartsWith(prefix, StringComparison.Ordinal)
            || !source.EndsWith(suffix, StringComparison.Ordinal))
        {
            return source;
        }

        var start = prefix.Length;
        var middleIndex = source.IndexOf(middle, start, StringComparison.Ordinal);
        if (middleIndex < 0)
        {
            return source;
        }

        var effect = source.Substring(start, middleIndex - start);
        var faction = source.Substring(middleIndex + middle.Length, source.Length - middleIndex - middle.Length - suffix.Length);
        return "このアイテムを見ることができる" + faction + "に" + effect + "効果を与える。";
    }

    private static void TranslateStringMember(object target, string memberName, string family, Func<string, string> translate)
    {
        if (!TryGetStringMemberValue(target, memberName, out var current)
            || string.IsNullOrEmpty(current)
            || current!.StartsWith("\u0001", StringComparison.Ordinal))
        {
            return;
        }

        var translated = translate(current!);
        if (string.Equals(translated, current, StringComparison.Ordinal))
        {
            return;
        }

        if (TrySetStringMemberValue(target, memberName, translated))
        {
            DynamicTextObservability.RecordTransform(Context, family, current!, translated);
        }
    }

    private static object? GetPartOrMember(object target, string partName, string memberName)
    {
        var getPart = AccessTools.Method(target.GetType(), "GetPart", new[] { typeof(string) });
        if (getPart is not null)
        {
            try
            {
                var part = getPart.Invoke(target, new object[] { partName });
                if (part is not null)
                {
                    return part;
                }
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("QudJP: {0} could not get part '{1}' from '{2}': {3}", Context, partName, target.GetType().FullName, ex.Message);
            }
        }

        return TryGetMemberValue(target, memberName, out var value) ? value : null;
    }

    private static bool TryGetStringMemberValue(object target, string memberName, out string? value)
    {
        value = null;
        if (!TryGetMemberValue(target, memberName, out var raw))
        {
            return false;
        }

        value = raw as string;
        return true;
    }

    private static bool TryGetMemberValue(object target, string memberName, out object? value)
    {
        var type = target.GetType();
        var field = AccessTools.Field(type, memberName);
        if (field is not null)
        {
            value = field.GetValue(target);
            return true;
        }

        var property = AccessTools.Property(type, memberName);
        if (property is not null && property.CanRead)
        {
            value = property.GetValue(target);
            return true;
        }

        value = null;
        return false;
    }

    private static bool TrySetStringMemberValue(object target, string memberName, string value)
    {
        var type = target.GetType();
        var field = AccessTools.Field(type, memberName);
        if (field is not null && field.FieldType == typeof(string))
        {
            field.SetValue(target, value);
            return true;
        }

        var property = AccessTools.Property(type, memberName);
        if (property is not null && property.CanWrite && property.PropertyType == typeof(string))
        {
            property.SetValue(target, value);
            return true;
        }

        Trace.TraceWarning("QudJP: {0} could not set member '{1}' on '{2}'.", Context, memberName, type.FullName);
        return false;
    }
}
