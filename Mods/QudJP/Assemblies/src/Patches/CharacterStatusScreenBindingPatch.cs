using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class CharacterStatusScreenBindingPatch
{
    private const string Context = nameof(CharacterStatusScreenBindingPatch);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("Qud.UI.CharacterStatusScreen", "CharacterStatusScreen");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: CharacterStatusScreenBindingPatch target type not found.");
            return null;
        }

        var method = AccessTools.Method(targetType, "UpdateViewFromData", Type.EmptyTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: CharacterStatusScreenBindingPatch.UpdateViewFromData not found.");
        }

        return method;
    }

    public static bool Prefix(object? __instance)
    {
        try
        {
            if (__instance is null)
            {
                return true;
            }

            var instanceType = __instance.GetType();
            var go = GetMemberValue(__instance, "GO");
            if (go is null)
            {
                return true;
            }

            TrySetComputePower(instanceType, go);
            TryResolveMutationTerms(__instance, go);
            UpdateDirectTextFields(__instance, go);
            UpdatePointTexts(__instance, go);
            TryPopulateControllers(__instance, instanceType, go);
            TryUpdatePlayerIcon(__instance, go);
            return false;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: CharacterStatusScreenBindingPatch.Prefix failed: {0}", ex);
            return true;
        }
    }

    private static void TrySetComputePower(Type instanceType, object go)
    {
        try
        {
            SetComputePower(instanceType, go);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("QudJP: CharacterStatusScreenBindingPatch.SetComputePower skipped: {0}", ex.Message);
        }
    }

    private static void TryResolveMutationTerms(object instance, object go)
    {
        try
        {
            ResolveMutationTerms(instance, go);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("QudJP: CharacterStatusScreenBindingPatch.ResolveMutationTerms skipped: {0}", ex.Message);
        }
    }

    private static void TryPopulateControllers(object instance, Type instanceType, object go)
    {
        try
        {
            PopulateControllers(instance, instanceType, go);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("QudJP: CharacterStatusScreenBindingPatch.PopulateControllers skipped: {0}", ex.Message);
        }
    }

    private static void TryUpdatePlayerIcon(object instance, object go)
    {
        try
        {
            UpdatePlayerIcon(instance, go);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("QudJP: CharacterStatusScreenBindingPatch.UpdatePlayerIcon skipped: {0}", ex.Message);
        }
    }

    private static void UpdateDirectTextFields(object instance, object go)
    {
        var mutationTermSource = GetStringMemberValue(instance, "mutationsTerm");
        if (mutationTermSource is null)
        {
            mutationTermSource = string.Empty;
        }
        var translatedMutationTerm = TranslateUiText(mutationTermSource, "field=mutationTermText");
        var mutationTermText = ToUpperExceptFormatting(translatedMutationTerm);
        OwnerTextSetter.SetTranslatedText(
            GetMemberValue(instance, "mutationTermText"),
            mutationTermSource,
            mutationTermText,
            Context,
            typeof(CharacterStatusScreenBindingPatch));

        var nameSource = GetStringMemberValue(go, "DisplayName");
        if (nameSource is null)
        {
            nameSource = string.Empty;
        }
        var translatedName = TranslateUiText(nameSource, "field=nameText");
        OwnerTextSetter.SetTranslatedText(
            GetMemberValue(instance, "nameText"),
            nameSource,
            translatedName,
            Context,
            typeof(CharacterStatusScreenBindingPatch));

        var genotype = InvokeStringMethod(go, "GetGenotype");
        if (genotype is null)
        {
            genotype = string.Empty;
        }

        var subtype = InvokeStringMethod(go, "GetSubtype");
        if (subtype is null)
        {
            subtype = string.Empty;
        }

        var classSource = genotype + " " + subtype;
        var translatedClass = TranslateUiText(classSource, "field=classText");
        OwnerTextSetter.SetTranslatedText(
            GetMemberValue(instance, "classText"),
            classSource,
            translatedClass,
            Context,
            typeof(CharacterStatusScreenBindingPatch));

        var levelSource = BuildLevelSummary(go);
        var translatedLevel = TranslateUiText(levelSource, "field=levelText");
        OwnerTextSetter.SetTranslatedText(
            GetMemberValue(instance, "levelText"),
            levelSource,
            translatedLevel,
            Context,
            typeof(CharacterStatusScreenBindingPatch));
    }

    private static void UpdatePointTexts(object instance, object go)
    {
        var ap = GetGameStatValue(go, "AP");
        var attributePointsSource = string.Format(
            CultureInfo.InvariantCulture,
            "Attribute Points: {0}{1}}}}}",
            ap > 0 ? "{{G|" : "{{K|",
            ap);
        _ = UITextSkinReflectionAccessor.SetCurrentText(
            GetMemberValue(instance, "attributePointsText"),
            attributePointsSource,
            Context);

        var mp = GetGameStatValue(go, "MP");
        var mutationTermCapital = GetStringMemberValue(instance, "mutationTermCapital");
        if (mutationTermCapital is null)
        {
            mutationTermCapital = "Mutation";
        }

        string mutationPointsSource;
        if (IsCompactStatusLayout())
        {
            mutationPointsSource = string.Format(CultureInfo.InvariantCulture, "MP: {0}{1}}}}}", mp > 0 ? "{{G|" : "{{K|", mp);
        }
        else
        {
            mutationPointsSource = string.Format(
                CultureInfo.InvariantCulture,
                "{0} Points: {1}{2}}}}}",
                mutationTermCapital,
                mp > 0 ? "{{G|" : "{{K|",
                mp);
        }
        _ = UITextSkinReflectionAccessor.SetCurrentText(
            GetMemberValue(instance, "mutationPointsText"),
            mutationPointsSource,
            Context);
    }

    private static void PopulateControllers(object instance, Type instanceType, object go)
    {
        var stats = GetStaticEnumerable(instanceType, "stats");
        var mutations = GetStaticEnumerable(instanceType, "mutations");
        var effects = GetStaticEnumerable(instanceType, "effects");

        PopulateAttributeController(
            GetMemberValue(instance, "primaryAttributesController"),
            ResolveType("Qud.UI.CharacterAttributeLineData", "CharacterAttributeLineData"),
            GetStaticStringArray(instanceType, "PrimaryAttributes"),
            stats,
            go,
            categoryName: "primary",
            includeComputePower: false);
        var secondaryAttributes = GetStaticStringArray(instanceType, "SecondaryAttributes");
        if (GetStaticIntValue(instanceType, "CP") > 0)
        {
            secondaryAttributes = GetStaticStringArray(instanceType, "SecondaryAttributesWithCP");
        }

        PopulateAttributeController(
            GetMemberValue(instance, "secondaryAttributesController"),
            ResolveType("Qud.UI.CharacterAttributeLineData", "CharacterAttributeLineData"),
            secondaryAttributes,
            stats,
            go,
            categoryName: "secondary",
            includeComputePower: true);
        PopulateAttributeController(
            GetMemberValue(instance, "resistanceAttributesController"),
            ResolveType("Qud.UI.CharacterAttributeLineData", "CharacterAttributeLineData"),
            GetStaticStringArray(instanceType, "ResistanceAttributes"),
            stats,
            go,
            categoryName: "resistance",
            includeComputePower: false);
        PopulateSimpleController(
            GetMemberValue(instance, "mutationsController"),
            ResolveType("Qud.UI.CharacterMutationLineData", "CharacterMutationLineData"),
            "mutation",
            mutations);
        PopulateSimpleController(
            GetMemberValue(instance, "effectsController"),
            ResolveType("Qud.UI.CharacterEffectLineData", "CharacterEffectLineData"),
            "effect",
            effects);
    }

    private static void PopulateAttributeController(
        object? controller,
        Type? lineDataType,
        string[] attributeNames,
        IEnumerable<object?> stats,
        object go,
        string categoryName,
        bool includeComputePower)
    {
        if (controller is null || lineDataType is null)
        {
            return;
        }

        var list = CreateGenericList(lineDataType);
        var add = list.GetType().GetMethod("Add")!;
        foreach (var attributeName in attributeNames)
        {
            var statistic = FindStatByName(stats, attributeName);
            if (statistic is null && (!includeComputePower || !string.Equals(attributeName, "CP", StringComparison.Ordinal)))
            {
                continue;
            }

            var lineData = Activator.CreateInstance(lineDataType)!;
            SetMemberValue(lineData, "category", ParseEnumMember(lineData, "category", categoryName));
            SetMemberValue(lineData, "go", go);
            SetMemberValue(lineData, "data", statistic);
            SetMemberValue(lineData, "stat", attributeName);
            add.Invoke(list, new[] { lineData });
        }

        InvokeBeforeShow(controller, list);
    }

    private static void PopulateSimpleController(
        object? controller,
        Type? lineDataType,
        string memberName,
        IEnumerable<object?> items)
    {
        if (controller is null || lineDataType is null)
        {
            return;
        }

        var list = CreateGenericList(lineDataType);
        var add = list.GetType().GetMethod("Add")!;
        foreach (var item in items)
        {
            if (item is null)
            {
                continue;
            }

            var lineData = Activator.CreateInstance(lineDataType)!;
            SetMemberValue(lineData, memberName, item);
            add.Invoke(list, new[] { lineData });
        }

        InvokeBeforeShow(controller, list);
    }

    private static void InvokeBeforeShow(object controller, object selections)
    {
        var method = FindBeforeShowMethod(controller.GetType(), selections.GetType());
        method?.Invoke(controller, method.GetParameters().Length == 1
            ? new[] { selections }
            : new object?[] { null, selections });
    }

    private static MethodInfo? FindBeforeShowMethod(Type controllerType, Type selectionType)
    {
        foreach (var method in controllerType.GetMethods(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!string.Equals(method.Name, "BeforeShow", StringComparison.Ordinal))
            {
                continue;
            }

            var parameters = method.GetParameters();
            if (parameters.Length == 1 && parameters[0].ParameterType.IsAssignableFrom(selectionType))
            {
                return method;
            }

            if (parameters.Length == 2 && parameters[1].ParameterType.IsAssignableFrom(selectionType))
            {
                return method;
            }
        }

        return null;
    }

    private static object CreateGenericList(Type elementType)
    {
        return Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!;
    }

    private static object? ParseEnumMember(object instance, string memberName, string value)
    {
        var field = AccessTools.Field(instance.GetType(), memberName);
        if (field is not null && field.FieldType.IsEnum)
        {
            return Enum.Parse(field.FieldType, value, ignoreCase: false);
        }

        var property = AccessTools.Property(instance.GetType(), memberName);
        if (property is null || !property.PropertyType.IsEnum)
        {
            return null;
        }

        return Enum.Parse(property.PropertyType, value, ignoreCase: false);
    }

    private static object? FindStatByName(IEnumerable<object?> stats, string name)
    {
        foreach (var stat in stats)
        {
            if (stat is not null
                && string.Equals(GetStringMemberValue(stat, "Name"), name, StringComparison.Ordinal))
            {
                return stat;
            }
        }

        return null;
    }

    private static IEnumerable<object?> GetStaticEnumerable(Type type, string fieldName)
    {
        var value = AccessTools.Field(type, fieldName)?.GetValue(null);
        if (value is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                yield return item;
            }
        }
    }

    private static void UpdatePlayerIcon(object instance, object go)
    {
        var playerIcon = GetMemberValue(instance, "playerIcon");
        if (playerIcon is null)
        {
            return;
        }

        var renderable = InvokeMethod(go, "RenderForUI", "StatusScreen,Character");
        if (renderable is null)
        {
            return;
        }

        AccessTools.Method(playerIcon.GetType(), "FromRenderable")?.Invoke(playerIcon, new[] { renderable });
    }

    private static void SetComputePower(Type instanceType, object go)
    {
        var cpField = AccessTools.Field(instanceType, "CP");
        if (cpField?.FieldType != typeof(int))
        {
            return;
        }

        var eventType = AccessTools.TypeByName("XRL.World.GetAvailableComputePowerEvent");
        if (eventType is null)
        {
            eventType = AccessTools.TypeByName("GetAvailableComputePowerEvent");
        }
        var method = eventType is null ? null : AccessTools.Method(eventType, "GetFor");
        var value = method?.Invoke(null, new[] { go });
        if (value is not null)
        {
            cpField.SetValue(null, Convert.ToInt32(value, CultureInfo.InvariantCulture));
        }
    }

    private static void ResolveMutationTerms(object instance, object go)
    {
        var mutationTerm = GetStringMemberValue(instance, "mutationTerm");
        if (mutationTerm is null)
        {
            mutationTerm = "Mutation";
        }

        var mutationColor = GetStringMemberValue(instance, "mutationColor");
        if (mutationColor is null)
        {
            mutationColor = "C";
        }

        var eventType = AccessTools.TypeByName("XRL.World.GetMutationTermEvent");
        if (eventType is null)
        {
            eventType = AccessTools.TypeByName("GetMutationTermEvent");
        }
        var method = eventType is null ? null : AccessTools.Method(eventType, "GetFor");
        if (method is not null)
        {
            var args = new object?[] { go, mutationTerm, mutationColor };
            _ = method.Invoke(null, args);
            mutationTerm = args[1] as string ?? mutationTerm;
            mutationColor = args[2] as string ?? mutationColor;
        }

        SetMemberValue(instance, "mutationTerm", mutationTerm);
        SetMemberValue(instance, "mutationColor", mutationColor);
        SetMemberValue(instance, "mutationsTerm", MakeTitleCase(Pluralize(mutationTerm)));
        SetMemberValue(instance, "mutationTermCapital", MakeTitleCase(mutationTerm));
    }

    private static string TranslateUiText(string source, string fieldDetail)
    {
        var route = ObservabilityHelpers.ComposeContext(Context, fieldDetail);
        return ColorAwareTranslationComposer.TranslatePreservingColors(source, visible =>
        {
            if (CharacterStatusScreenTextTranslator.TryTranslateUiText(visible, route, out var candidate))
            {
                return candidate;
            }

            return StringHelpers.TryGetTranslationExactOrLowerAscii(visible, out candidate)
                ? candidate
                : visible;
        });
    }

    private static string BuildLevelSummary(object go)
    {
        var level = GetIntMemberValue(go, "Level");
        var hpCurrent = GetGameStatValue(go, "Hitpoints");
        var hitpoints = InvokeMethod(go, "GetStat", "Hitpoints");
        var hpMax = hitpoints is null ? 0 : GetIntMemberValue(hitpoints, "BaseValue");
        var xpCurrent = GetGameStatValue(go, "XP");
        var xpMax = GetNextLevelXp(go);
        var weight = GetMemberValue(go, "Weight");
        if (weight is null)
        {
            weight = 0;
        }
        return string.Format(
            CultureInfo.InvariantCulture,
            "Level: {0} ¯ HP: {1}/{2} ¯ XP: {3}/{4} ¯ Weight: {5}#",
            level,
            hpCurrent,
            hpMax,
            xpCurrent,
            xpMax,
            weight);
    }

    private static int GetNextLevelXp(object go)
    {
        var level = GetGameStatValue(go, "Level");
        var levelerType = AccessTools.TypeByName("XRL.World.Capabilities.Leveler");
        if (levelerType is null)
        {
            levelerType = AccessTools.TypeByName("Leveler");
        }
        var method = levelerType is null ? null : AccessTools.Method(levelerType, "GetXPForLevel");
        var value = method?.Invoke(null, new object[] { level + 1 });
        return value is null ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private static int GetGameStatValue(object go, string statName)
    {
        var value = InvokeMethod(go, "GetStatValue", statName);
        if (value is null)
        {
            value = InvokeMethod(go, "Stat", statName);
        }
        return value is null ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private static bool IsCompactStatusLayout()
    {
        var mediaType = AccessTools.TypeByName("XRL.UI.Media");
        if (mediaType is null)
        {
            mediaType = AccessTools.TypeByName("Media");
        }
        var sizeClassValue = mediaType is null ? null : GetStaticMemberValue(mediaType, "sizeClass");
        if (sizeClassValue is null)
        {
            return false;
        }

        var enumType = sizeClassValue.GetType();
        if (!enumType.IsEnum || !Enum.IsDefined(enumType, "Medium"))
        {
            return false;
        }

        var medium = Enum.Parse(enumType, "Medium", ignoreCase: false);
        return Comparer.DefaultInvariant.Compare(sizeClassValue, medium) < 0;
    }

    private static string ToUpperExceptFormatting(string source)
    {
        var colorUtilityType = AccessTools.TypeByName("ConsoleLib.Console.ColorUtility");
        if (colorUtilityType is null)
        {
            colorUtilityType = AccessTools.TypeByName("ColorUtility");
        }
        var method = colorUtilityType is null ? null : AccessTools.Method(colorUtilityType, "ToUpperExceptFormatting", new[] { typeof(string) });
        return method?.Invoke(null, new object[] { source }) as string ?? source.ToUpperInvariant();
    }

    private static string MakeTitleCase(string source)
    {
        var grammarType = AccessTools.TypeByName("XRL.Language.Grammar");
        if (grammarType is null)
        {
            grammarType = AccessTools.TypeByName("Grammar");
        }
        var method = grammarType is null ? null : AccessTools.Method(grammarType, "MakeTitleCase", new[] { typeof(string) });
        return method?.Invoke(null, new object[] { source }) as string ?? source;
    }

    private static string Pluralize(string source)
    {
        var grammarType = AccessTools.TypeByName("XRL.Language.Grammar");
        if (grammarType is null)
        {
            grammarType = AccessTools.TypeByName("Grammar");
        }
        var method = grammarType is null ? null : AccessTools.Method(grammarType, "Pluralize", new[] { typeof(string) });
        return method?.Invoke(null, new object[] { source }) as string ?? (source + "s");
    }

    private static Type? ResolveType(string fullName, string fallbackName)
    {
        var resolved = AccessTools.TypeByName(fullName);
        if (resolved is not null)
        {
            return resolved;
        }

        return AccessTools.TypeByName(fallbackName);
    }

    private static string[] GetStaticStringArray(Type type, string fieldName)
    {
        return AccessTools.Field(type, fieldName)?.GetValue(null) as string[] ?? Array.Empty<string>();
    }

    private static int GetStaticIntValue(Type type, string fieldName)
    {
        var value = AccessTools.Field(type, fieldName)?.GetValue(null);
        return value is null ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private static object? GetStaticMemberValue(Type type, string memberName)
    {
        var property = AccessTools.Property(type, memberName);
        if (property is not null && property.CanRead)
        {
            return property.GetValue(null);
        }

        var field = AccessTools.Field(type, memberName);
        return field?.GetValue(null);
    }

    private static object? GetMemberValue(object instance, string memberName)
    {
        var type = instance.GetType();
        var property = AccessTools.Property(type, memberName);
        if (property is not null && property.CanRead)
        {
            return property.GetValue(instance);
        }

        var field = AccessTools.Field(type, memberName);
        return field?.GetValue(instance);
    }

    private static string? GetStringMemberValue(object instance, string memberName)
    {
        return GetMemberValue(instance, memberName) as string;
    }

    private static int GetIntMemberValue(object instance, string memberName)
    {
        var value = GetMemberValue(instance, memberName);
        return value is null ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private static string? InvokeStringMethod(object instance, string methodName, params object?[]? args)
    {
        return InvokeMethod(instance, methodName, args) as string;
    }

    private static object? InvokeMethod(object instance, string methodName, params object?[]? args)
    {
        return AccessTools.Method(instance.GetType(), methodName)?.Invoke(instance, args);
    }

    private static void SetMemberValue(object instance, string memberName, object? value)
    {
        var type = instance.GetType();
        var property = AccessTools.Property(type, memberName);
        if (property is not null && property.CanWrite)
        {
            property.SetValue(instance, value);
            return;
        }

        var field = AccessTools.Field(type, memberName);
        field?.SetValue(instance, value);
    }
}
