using System;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class EquipmentLineTranslationPatch
{
    private const string Context = nameof(EquipmentLineTranslationPatch);

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

    public static bool Prefix(object? __instance, object? data)
    {
        try
        {
            if (__instance is null || data is null)
            {
                return true;
            }

            var bodyPart = GetMemberValue(data, "bodyPart");
            if (bodyPart is null)
            {
                return true;
            }

            SetMemberValue(data, "line", __instance);
            SetContextData(__instance, data);
            SetMemberValue(__instance, "tooltipCompareGo", null);

            var showCybernetics = GetBoolMemberValue(data, "showCybernetics");
            object? tooltipGo;
            if (showCybernetics)
            {
                tooltipGo = GetMemberValue(bodyPart, "Cybernetics");
            }
            else
            {
                tooltipGo = GetMemberValue(bodyPart, "Equipped");
                if (tooltipGo is null) { tooltipGo = GetMemberValue(bodyPart, "DefaultBehavior"); }
            }
            SetMemberValue(__instance, "tooltipGo", tooltipGo);

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

            object? gameObject;
            if (showCybernetics)
            {
                gameObject = GetMemberValue(bodyPart, "Cybernetics");
            }
            else
            {
                gameObject = GetMemberValue(bodyPart, "Equipped");
                if (gameObject is null) { gameObject = GetMemberValue(bodyPart, "DefaultBehavior"); }
            }
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

            if (gameObject is not null)
            {
                var renderable = InvokeMethod(gameObject, "RenderForUI", "Equipment");
                TryInvokeMethod(GetMemberValue(__instance, "icon"), "FromRenderable", renderable);
            }

            return false;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: EquipmentLineTranslationPatch.Prefix failed: {0}", ex);
            return true;
        }
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
        var optionsType = AccessTools.TypeByName("XRL.UI.Options");
        if (optionsType is null) { optionsType = AccessTools.TypeByName("Options"); }
        var property = optionsType is null ? null : AccessTools.Property(optionsType, "IndentBodyParts");
        if (property is not null && property.CanRead)
        {
            var value = property.GetValue(null);
            return value is bool indent && indent;
        }

        var field = optionsType is null ? null : AccessTools.Field(optionsType, "IndentBodyParts");
        return field?.GetValue(null) as bool? ?? false;
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

    private static void SetContextData(object instance, object data)
    {
        var context = GetMemberValue(instance, "context");
        if (context is not null)
        {
            SetMemberValue(context, "data", data);
        }
    }

    private static void TryInvokeMethod(object? target, string methodName, object? arg)
    {
        if (target is null)
        {
            return;
        }

        var argType = arg?.GetType();
        if (argType is null) { argType = typeof(object); }
        var method = AccessTools.Method(target.GetType(), methodName, new[] { argType });
        if (method is null) { method = AccessTools.Method(target.GetType(), methodName); }
        _ = method?.Invoke(target, arg is null ? null : new[] { arg });
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
