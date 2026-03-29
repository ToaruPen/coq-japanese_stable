using System;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Threading;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class EquipmentLineTranslationPatch
{
    private const string Context = nameof(EquipmentLineTranslationPatch);

    private static int _indentBodyPartsResolved;
    private static PropertyInfo? _indentBodyPartsProperty;
    private static FieldInfo? _indentBodyPartsField;

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("Qud.UI.EquipmentLine", "EquipmentLine");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: EquipmentLineTranslationPatch target type not found.");
            return null;
        }

        var frameworkDataElementType = GameTypeResolver.FindType("XRL.UI.Framework.FrameworkDataElement", "FrameworkDataElement");
        var method = frameworkDataElementType is null
            ? null
            : AccessTools.Method(targetType, "setData", new[] { frameworkDataElementType });
        if (method is null)
        {
            Trace.TraceError("QudJP: EquipmentLineTranslationPatch.setData(FrameworkDataElement) not found.");
        }

        return method;
    }

    public static void Postfix(object? __instance, object? data)
    {
        try
        {
            if (__instance is null || data is null)
            {
                return;
            }

            var bodyPart = GetMemberValue(data, "bodyPart");
            if (bodyPart is null)
            {
                return;
            }

            var cardinalDescription = InvokeStringMethod(bodyPart, "GetCardinalDescription");
            if (cardinalDescription is null) { cardinalDescription = string.Empty; }
            var paddedDescription = BuildIndentedDescription(bodyPart, cardinalDescription);
            var primaryPrefix = GetBoolMemberValue(bodyPart, "Primary") ? "{{G|*}}" : string.Empty;
            var slotSource = primaryPrefix + paddedDescription;
            var slotRoute = ObservabilityHelpers.ComposeContext(Context, "field=text");
            var translatedCardinal = TranslateVisibleText(cardinalDescription, slotRoute, "EquipmentLine.SlotName");
            var translatedSlot = primaryPrefix + ReapplyIndentation(paddedDescription, translatedCardinal);
            if (!string.Equals(translatedSlot, slotSource, StringComparison.Ordinal))
            {
                DynamicTextObservability.RecordTransform(slotRoute, "EquipmentLine.SlotText", slotSource, translatedSlot);
            }

            OwnerTextSetter.SetTranslatedText(
                GetMemberValue(__instance, "text"),
                slotSource,
                translatedSlot,
                Context,
                typeof(EquipmentLineTranslationPatch));

            var gameObject = ResolveDisplayedItem(data, bodyPart);
            var itemTarget = gameObject ?? data;
            var itemSource = GetStringMemberValue(itemTarget, "DisplayName");
            if (itemSource is null) { itemSource = "{{K|-}}"; }
            var itemRoute = ObservabilityHelpers.ComposeContext(Context, "field=itemText");
            var translatedItem = TranslateVisibleText(itemSource, itemRoute, "EquipmentLine.ItemName");
            OwnerTextSetter.SetTranslatedText(
                GetMemberValue(__instance, "itemText"),
                itemSource,
                translatedItem,
                Context,
                typeof(EquipmentLineTranslationPatch));
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: EquipmentLineTranslationPatch.Postfix failed: {0}", ex);
        }
    }

    private static object? ResolveDisplayedItem(object data, object bodyPart)
    {
        if (GetBoolMemberValue(data, "showCybernetics"))
        {
            return GetMemberValue(bodyPart, "Cybernetics");
        }

        var equipped = GetMemberValue(bodyPart, "Equipped");
        return equipped ?? GetMemberValue(bodyPart, "DefaultBehavior");
    }

    private static string BuildIndentedDescription(object bodyPart, string cardinalDescription)
    {
        var indentDepth = ShouldIndentBodyParts()
            ? GetBodyPartDepth(bodyPart)
            : 0;
        return indentDepth <= 0
            ? cardinalDescription
            : cardinalDescription.PadLeft(indentDepth + cardinalDescription.Length, ' ');
    }

    private static string ReapplyIndentation(string source, string translatedVisible)
    {
        var leadingWhitespaceLength = source.Length - source.TrimStart(' ').Length;
        return leadingWhitespaceLength <= 0
            ? translatedVisible
            : new string(' ', leadingWhitespaceLength) + translatedVisible;
    }

    private static int GetBodyPartDepth(object bodyPart)
    {
        var parentBody = GetMemberValue(bodyPart, "ParentBody");
        var depth = InvokeMethod(parentBody!, "GetPartDepth", bodyPart);
        return depth is null ? 0 : Convert.ToInt32(depth, CultureInfo.InvariantCulture);
    }

    private static bool ShouldIndentBodyParts()
    {
        if (Interlocked.CompareExchange(ref _indentBodyPartsResolved, 1, 0) == 0)
        {
            ResolveIndentBodyPartsMembers();
        }

        if (_indentBodyPartsProperty is not null && _indentBodyPartsProperty.CanRead)
        {
            var value = _indentBodyPartsProperty.GetValue(null);
            return value is bool indent && indent;
        }

        return _indentBodyPartsField?.GetValue(null) as bool? ?? false;
    }

    private static void ResolveIndentBodyPartsMembers()
    {
        var optionsType = AccessTools.TypeByName("XRL.UI.Options");
        if (optionsType is null) { optionsType = AccessTools.TypeByName("Options"); }

        if (optionsType is not null)
        {
            _indentBodyPartsProperty = AccessTools.Property(optionsType, "IndentBodyParts");
            _indentBodyPartsField = AccessTools.Field(optionsType, "IndentBodyParts");
        }
    }

    private static string TranslateVisibleText(string source, string route, string family)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source;
        }

        var translated = ColorAwareTranslationComposer.TranslatePreservingColors(
            source,
            static visible => StringHelpers.TryGetTranslationExactOrLowerAscii(visible, out var candidate)
                ? candidate
                : visible);
        if (!string.Equals(translated, source, StringComparison.Ordinal))
        {
            DynamicTextObservability.RecordTransform(route, family, source, translated);
        }

        return translated;
    }

    private static object? InvokeMethod(object instance, string methodName, params object?[]? args)
    {
        var method = AccessTools.Method(instance.GetType(), methodName);
        return method?.Invoke(instance, args);
    }

    private static string? InvokeStringMethod(object instance, string methodName)
    {
        return InvokeMethod(instance, methodName) as string;
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

    private static string? GetStringMemberValue(object? instance, string memberName)
    {
        return instance is null ? null : GetMemberValue(instance, memberName) as string;
    }

    private static bool GetBoolMemberValue(object instance, string memberName)
    {
        var value = GetMemberValue(instance, memberName);
        return value is not null && Convert.ToBoolean(value, CultureInfo.InvariantCulture);
    }
}
