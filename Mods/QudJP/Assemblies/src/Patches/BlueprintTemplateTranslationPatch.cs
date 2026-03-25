using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class BlueprintTemplateTranslationPatch
{
    private static FieldInfo? blueprintsField;
    private static FieldInfo? partsField;
    private static FieldInfo? settersCacheField;
    private static FieldInfo? originalValueField;
    private static FieldInfo? parsedValueField;

    private static Dictionary<string, string>? templateTranslations;
    private static string? dictionaryPathOverride;

    private static readonly Dictionary<string, string[]> TranslatablePartFields =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["PowerSwitch"] = new[]
            {
                "ActivateSuccessMessage", "ActivateFailureMessage",
                "DeactivateSuccessMessage", "DeactivateFailureMessage",
                "KeyObjectAccessMessage", "PsychometryAccessMessage", "AccessFailureMessage",
            },
            ["ForceProjector"] = new[] { "PsychometryAccessMessage", "AccessFailureMessage" },
            ["Consumer"] = new[] { "Message" },
            ["DesalinationPellet"] = new[] { "Message", "DestroyMessage", "ConvertMessage" },
            ["Explores"] = new[] { "ExploreMessage" },
            ["LootOnStep"] = new[] { "SuccessMessage", "FailMessage" },
            ["Preacher"] = new[] { "Prefix", "Postfix", "Frozen" },
            ["BlowAwayGas"] = new[] { "Message" },
            ["CancelRangedAttacks"] = new[] { "Message" },
            ["Interactable"] = new[] { "Message" },
            ["LifeSaver"] = new[]
            {
                "LethalMessage", "MaxHitpointsThresholdMessage",
                "DestroyWhenUsedUpMessage",
            },
            ["NephalProperties"] = new[] { "PhaseMessage" },
            ["RandomLongRangeTeleportOnDamage"] = new[] { "Message" },
            ["Reconstitution"] = new[] { "DropMessage" },
            ["SpawnVessel"] = new[] { "SpawnMessage" },
            ["Spawner"] = new[] { "SpawnMessage" },
            ["SplitOnDeath"] = new[] { "Message" },
            ["SwapOnUse"] = new[] { "Message" },
            ["TimeCubeProtection"] = new[] { "Message" },
        };

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var factoryType = GameTypeResolver.FindType("XRL.World.GameObjectFactory", "GameObjectFactory");
        if (factoryType is null)
        {
            Trace.TraceError($"QudJP: {nameof(BlueprintTemplateTranslationPatch)} — GameObjectFactory type not found.");
            return null;
        }

        var method = AccessTools.Method(factoryType, "LoadBlueprints");
        if (method is null)
        {
            Trace.TraceError($"QudJP: {nameof(BlueprintTemplateTranslationPatch)} — LoadBlueprints method not found.");
        }

        return method;
    }

    public static void Postfix(object __instance)
    {
        try
        {
            if (!InitializeReflection())
            {
                return;
            }

            var translations = LoadTranslations();
            if (translations is null || translations.Count == 0)
            {
                Trace.TraceWarning("QudJP: BlueprintTemplateTranslation: no template translations loaded.");
                return;
            }

            if (blueprintsField?.GetValue(__instance) is not IDictionary blueprints)
            {
                Trace.TraceError("QudJP: BlueprintTemplateTranslation: Blueprints field not found or not a dictionary.");
                return;
            }

            var translatedCount = 0;
            foreach (DictionaryEntry entry in blueprints)
            {
                if (entry.Value is null)
                {
                    continue;
                }

                translatedCount += TranslateBlueprintParts(entry.Value, translations);
            }

            Trace.TraceInformation(
                "QudJP: BlueprintTemplateTranslation: translated {0} field(s) across {1} blueprint(s).",
                translatedCount,
                blueprints.Count);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0} postfix failed: {1}", nameof(BlueprintTemplateTranslationPatch), ex);
        }
    }

    internal static void SetDictionaryPathForTests(string? path)
    {
        dictionaryPathOverride = path;
        templateTranslations = null;
    }

    internal static void ResetForTests()
    {
        SetDictionaryPathForTests(null);
        blueprintsField = null;
        partsField = null;
        settersCacheField = null;
        originalValueField = null;
        parsedValueField = null;
    }

    internal static Dictionary<string, string>? LoadTranslations()
    {
        if (templateTranslations is not null)
        {
            return templateTranslations;
        }

        var path = dictionaryPathOverride
            ?? LocalizationAssetResolver.GetLocalizationPath("BlueprintTemplates/templates.ja.json");

        if (!File.Exists(path))
        {
            Trace.TraceWarning("QudJP: BlueprintTemplateTranslation: dictionary not found at {0}", path);
            return null;
        }

        TemplateDictionaryDocument? document;
        try
        {
            using var stream = File.OpenRead(path);
            var serializer = new DataContractJsonSerializer(typeof(TemplateDictionaryDocument));
            document = serializer.ReadObject(stream) as TemplateDictionaryDocument;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: BlueprintTemplateTranslation: failed to parse dictionary {0}: {1}", path, ex.Message);
            return null;
        }

        if (document?.Entries is null)
        {
            Trace.TraceError("QudJP: BlueprintTemplateTranslation: dictionary has no entries array: {0}", path);
            return null;
        }

        var translations = new Dictionary<string, string>(document.Entries.Count, StringComparer.Ordinal);
        for (var index = 0; index < document.Entries.Count; index++)
        {
            var entry = document.Entries[index];
            if (string.IsNullOrEmpty(entry?.Key) || entry?.Text is null)
            {
                continue;
            }

            translations[entry!.Key!] = entry.Text;
        }

        templateTranslations = translations;
        Trace.TraceInformation("QudJP: BlueprintTemplateTranslation: loaded {0} template entries.", translations.Count);
        return translations;
    }

    internal static IReadOnlyDictionary<string, string[]> GetTranslatablePartFields()
    {
        return TranslatablePartFields;
    }

    private static int TranslateBlueprintParts(object blueprint, Dictionary<string, string> translations)
    {
        if (partsField?.GetValue(blueprint) is not IDictionary parts)
        {
            return 0;
        }

        var count = 0;
        foreach (DictionaryEntry partEntry in parts)
        {
            if (partEntry.Key is not string partName
                || !TranslatablePartFields.TryGetValue(partName, out var fieldNames)
                || partEntry.Value is null)
            {
                continue;
            }

            count += TranslatePartFields(partEntry.Value, fieldNames, translations);
        }

        return count;
    }

    private static int TranslatePartFields(
        object partBlueprint,
        string[] fieldNames,
        Dictionary<string, string> translations)
    {
        if (settersCacheField is null || originalValueField is null || parsedValueField is null)
        {
            return 0;
        }

        if (settersCacheField.GetValue(partBlueprint) is not IDictionary cache)
        {
            return 0;
        }

        var count = 0;
        for (var index = 0; index < fieldNames.Length; index++)
        {
            var fieldName = fieldNames[index];
            if (!cache.Contains(fieldName))
            {
                continue;
            }

            var setter = cache[fieldName];
            if (setter is null)
            {
                continue;
            }

            var originalValue = originalValueField.GetValue(setter) as string;
            if (originalValue is null || !translations.TryGetValue(originalValue, out var translated))
            {
                continue;
            }

            originalValueField.SetValue(setter, translated);
            parsedValueField.SetValue(setter, translated);
            cache[fieldName] = setter;
            count++;
        }

        return count;
    }

    private static bool InitializeReflection()
    {
        if (settersCacheField is not null)
        {
            return true;
        }

        var factoryType = AccessTools.TypeByName("XRL.World.GameObjectFactory");
        var localBlueprintsField = factoryType?.GetField("Blueprints");

        var blueprintType = AccessTools.TypeByName("XRL.World.GameObjectBlueprint");
        var localPartsField = blueprintType?.GetField("Parts");

        var partBlueprintType = AccessTools.TypeByName("XRL.World.GamePartBlueprint");
        if (partBlueprintType is null)
        {
            Trace.TraceError("QudJP: BlueprintTemplateTranslation — GamePartBlueprint type not found.");
            return false;
        }

#pragma warning disable S3011 // Reflection on non-public members is required to modify blueprint PartSetter cache
        var localSettersCacheField = partBlueprintType.GetField(
            "_SettersCache",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (localSettersCacheField is null)
        {
            Trace.TraceError("QudJP: BlueprintTemplateTranslation — _SettersCache field not found.");
            return false;
        }

        var setterType = partBlueprintType.GetNestedType("PartSetter", BindingFlags.NonPublic);
#pragma warning restore S3011
        if (setterType is null)
        {
            Trace.TraceError("QudJP: BlueprintTemplateTranslation — PartSetter type not found.");
            return false;
        }

        var localOriginalValueField = setterType.GetField("OriginalValue");
        var localParsedValueField = setterType.GetField("ParsedValue");

        if (localOriginalValueField is null || localParsedValueField is null)
        {
            Trace.TraceError("QudJP: BlueprintTemplateTranslation — PartSetter fields not found.");
            return false;
        }

        // Assign all fields atomically to prevent partial initialization.
        blueprintsField = localBlueprintsField;
        partsField = localPartsField;
        originalValueField = localOriginalValueField;
        parsedValueField = localParsedValueField;
        settersCacheField = localSettersCacheField;

        return true;
    }

    [DataContract]
    internal sealed class TemplateDictionaryDocument
    {
        [DataMember(Name = "entries")]
        public List<TemplateEntry>? Entries { get; set; }
    }

    [DataContract]
    internal sealed class TemplateEntry
    {
        [DataMember(Name = "key")]
        public string? Key { get; set; }

        [DataMember(Name = "text")]
        public string? Text { get; set; }
    }
}
