using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class CharacterEffectLineTranslationPatch
{
    private const string Context = nameof(CharacterEffectLineTranslationPatch);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        return FrameworkDataElementSetDataTargetResolver.Resolve(
            Context,
            "Qud.UI.CharacterEffectLine",
            "CharacterEffectLine");
    }

    public static bool Prefix(object? __instance, object? data)
    {
        try
        {
            if (__instance is null || data is null)
            {
                return true;
            }

            var effect = GetMemberValue(data, "effect");
            if (effect is null)
            {
                return true;
            }

            SetContextData(__instance, data);

            var source = GetStringMemberValue(effect, "DisplayName");
            if (source is null)
            {
                source = string.Empty;
            }
            var translated = TranslateEffectName(source);
            OwnerTextSetter.SetTranslatedText(
                GetMemberValue(__instance, "text"),
                source,
                translated,
                Context,
                typeof(CharacterEffectLineTranslationPatch));
            return false;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: CharacterEffectLineTranslationPatch.Prefix failed: {0}", ex);
            return true;
        }
    }

    private static string TranslateEffectName(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source;
        }

        var route = ObservabilityHelpers.ComposeContext(Context, "field=text");
        var translated = ColorAwareTranslationComposer.TranslatePreservingColors(
            source,
            static visible => StringHelpers.TryGetTranslationExactOrLowerAscii(visible, out var candidate)
                ? candidate
                : visible);
        if (!string.Equals(source, translated, StringComparison.Ordinal))
        {
            DynamicTextObservability.RecordTransform(route, "CharacterStatus.EffectName", source, translated);
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
