using System;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class JournalLineTranslationPatch
{
    private const string Context = nameof(JournalLineTranslationPatch);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("Qud.UI.JournalLine", "JournalLine");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: JournalLineTranslationPatch target type not found.");
            return null;
        }

        var frameworkDataElementType = GameTypeResolver.FindType("XRL.UI.Framework.FrameworkDataElement", "FrameworkDataElement");
        var method = frameworkDataElementType is null
            ? null
            : AccessTools.Method(targetType, "setData", new[] { frameworkDataElementType });
        if (method is null)
        {
            Trace.TraceError("QudJP: JournalLineTranslationPatch.setData(FrameworkDataElement) not found.");
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

            var screen = GetMemberValue(data, "screen");
            var entry = GetMemberValue(data, "entry");
            var category = GetBoolMemberValue(data, "category");
            if (screen is null && !category)
            {
                return true;
            }

            SetContextData(__instance, data);
            SetMemberValue(__instance, "screen", screen);
            SetImageRenderable(__instance, GetMemberValue(data, "renderable"));

            if (category)
            {
                ApplyCategoryRow(__instance, data);
                return false;
            }

            if (entry is null)
            {
                return true;
            }

            if (GetMemberValue(entry, "Recipe") is not null)
            {
                ApplyRecipeRow(__instance, entry);
                return false;
            }

            ApplyEntryRow(__instance, data, entry);
            return false;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: JournalLineTranslationPatch.Prefix failed: {0}", ex);
            return true;
        }
    }

    private static void ApplyCategoryRow(object instance, object data)
    {
        SetPaddingLeft(GetMemberValue(instance, "layoutGroup"), 16);

        var categoryName = GetRequiredStringMemberValue(data, "categoryName");
        var prefixBuilder = new StringBuilder();
        if (!string.Equals(categoryName, GetNoEntriesText(data), StringComparison.Ordinal))
        {
            prefixBuilder.Append(GetBoolMemberValue(data, "categoryExpanded") ? "[-] " : "[+] ");
        }

        var prefix = prefixBuilder.ToString();
        var source = prefix + categoryName;
        var route = ObservabilityHelpers.ComposeContext(Context, "field=headerText");
        var translatedCategory = TranslateVisibleText(categoryName, route, "JournalLine.CategoryHeader");
        var translated = prefix + translatedCategory;

        TrySetActive(GetMemberValue(instance, "headerContainer"), active: true);
        OwnerTextSetter.SetTranslatedText(
            GetMemberValue(instance, "headerText"),
            source,
            translated,
            Context,
            typeof(JournalLineTranslationPatch));
        OwnerTextSetter.SetTranslatedText(
            GetMemberValue(instance, "text"),
            string.Empty,
            string.Empty,
            Context,
            typeof(JournalLineTranslationPatch));
    }

    private static void ApplyRecipeRow(object instance, object entry)
    {
        SetPaddingLeft(GetMemberValue(instance, "layoutGroup"), 16);
        TrySetActive(GetMemberValue(instance, "headerContainer"), active: true);

        var recipe = GetMemberValue(entry, "Recipe");
        if (recipe is null)
        {
            Trace.TraceError("QudJP: JournalLineTranslationPatch recipe entry missing Recipe member.");
            return;
        }

        var headerSource = InvokeStringMethod(recipe, "GetDisplayName") ?? string.Empty;
        var headerRoute = ObservabilityHelpers.ComposeContext(Context, "field=headerText");
        var translatedHeader = TranslateVisibleText(headerSource, headerRoute, "JournalLine.RecipeHeader");
        OwnerTextSetter.SetTranslatedText(
            GetMemberValue(instance, "headerText"),
            headerSource,
            translatedHeader,
            Context,
            typeof(JournalLineTranslationPatch));

        var ingredients = InvokeStringMethod(recipe, "GetIngredients") ?? string.Empty;
        var description = InvokeStringMethod(recipe, "GetDescription") ?? string.Empty;
        var source = BuildRecipeBody(ingredients, description);
        var route = ObservabilityHelpers.ComposeContext(Context, "field=text");
        var translated = TranslateRecipeBody(ingredients, description, route);
        OwnerTextSetter.SetTranslatedText(
            GetMemberValue(instance, "text"),
            source,
            translated,
            Context,
            typeof(JournalLineTranslationPatch));
    }

    private static void ApplyEntryRow(object instance, object data, object entry)
    {
        TrySetActive(GetMemberValue(instance, "headerContainer"), active: false);
        SetPaddingLeft(GetMemberValue(instance, "layoutGroup"), 48);

        var source = BuildEntrySource(entry);
        var route = ObservabilityHelpers.ComposeContext(Context, "field=text");
        var translated = TranslateEntrySource(entry, source, route);
        translated = MaybeClipText(translated, data);

        OwnerTextSetter.SetTranslatedText(
            GetMemberValue(instance, "text"),
            source,
            translated,
            Context,
            typeof(JournalLineTranslationPatch));
    }

    private static string BuildRecipeBody(string ingredients, string description)
    {
        var builder = new StringBuilder();
        builder.Append("{{K|Ingredients:}} ");
        builder.AppendLine(ingredients);

        var lines = description.Split(new[] { '\n' }, StringSplitOptions.None);
        for (var index = 0; index < lines.Length; index++)
        {
            if (index > 0)
            {
                builder.Append('\n');
            }

            if (index == 0)
            {
                builder.Append("{{K|Effects:}}     {{K|/}} {{y|");
            }
            else
            {
                builder.Append("             {{K|/}} {{y|");
            }

            builder.Append(lines[index]);
            builder.Append("}}");
        }

        builder.Append(" \n \n");
        return builder.ToString();
    }

    private static string TranslateRecipeBody(string ingredients, string description, string route)
    {
        var builder = new StringBuilder();
        builder.Append("{{K|");
        builder.Append(Translator.Translate("Ingredients:"));
        builder.Append("}} ");
        builder.AppendLine(TranslateVisibleText(ingredients, route, "JournalLine.RecipeBody"));

        var lines = description.Split(new[] { '\n' }, StringSplitOptions.None);
        for (var index = 0; index < lines.Length; index++)
        {
            if (index > 0)
            {
                builder.Append('\n');
            }

            if (index == 0)
            {
                builder.Append("{{K|");
                builder.Append(Translator.Translate("Effects:"));
                builder.Append("}}     {{K|/}} {{y|");
            }
            else
            {
                builder.Append("             {{K|/}} {{y|");
            }

            builder.Append(TranslateVisibleText(lines[index], route, "JournalLine.RecipeBody"));
            builder.Append("}}");
        }

        builder.Append(" \n \n");
        return builder.ToString();
    }

    private static string BuildEntrySource(object entry)
    {
        var builder = new StringBuilder();
        if (GetBoolMemberValue(entry, "Tracked"))
        {
            builder.Append("[X] ");
        }
        else if (HasMember(entry, "Tracked"))
        {
            builder.Append("[ ] ");
        }

        if (HasMember(entry, "Tradable"))
        {
            builder.Append(GetBoolMemberValue(entry, "Tradable") ? "{{G|$}} " : "{{K|$}} ");
        }

        var tombPropaganda = InvokeBoolMethod(entry, "Has", "sultanTombPropaganda");
        if (tombPropaganda)
        {
            builder.Append("{{w|[tomb engraving] ");
        }

        builder.Append(GetDisplayTextOrEmpty(entry));
        if (tombPropaganda)
        {
            builder.Append("}}");
        }

        return builder.ToString();
    }

    private static string TranslateEntrySource(object entry, string source, string route)
    {
        var builder = new StringBuilder();
        if (GetBoolMemberValue(entry, "Tracked"))
        {
            builder.Append("[X] ");
        }
        else if (HasMember(entry, "Tracked"))
        {
            builder.Append("[ ] ");
        }

        if (HasMember(entry, "Tradable"))
        {
            builder.Append(GetBoolMemberValue(entry, "Tradable") ? "{{G|$}} " : "{{K|$}} ");
        }

        var tombPropaganda = InvokeBoolMethod(entry, "Has", "sultanTombPropaganda");
        if (tombPropaganda)
        {
            var translatedMarker = TranslateVisibleText("tomb engraving", route, "JournalLine.EntryText");
            builder.Append("{{w|[");
            builder.Append(translatedMarker);
            builder.Append("] ");
        }

        var displayText = GetDisplayTextOrEmpty(entry);
        string translatedDisplayText;
        if (HasMember(entry, "Tracked"))
        {
            if (!JournalTextTranslator.TryTranslateMapNoteEntry(entry, displayText, route, out translatedDisplayText))
            {
                translatedDisplayText = displayText;
            }
        }
        else if (!JournalTextTranslator.TryTranslateBaseEntry(entry, displayText, route, out translatedDisplayText))
        {
            translatedDisplayText = TranslateVisibleText(displayText, route, "JournalLine.EntryText");
        }

        builder.Append(translatedDisplayText);
        if (tombPropaganda)
        {
            builder.Append("}}");
        }

        // Record at the entry-row level: the translator uses its own families internally.
        var translated = builder.ToString();
        if (!string.Equals(translated, source, StringComparison.Ordinal))
        {
            DynamicTextObservability.RecordTransform(route, "JournalLine.EntryText", source, translated);
        }

        return translated;
    }

    private static string MaybeClipText(string source, object data)
    {
        var screen = GetMemberValue(data, "screen");
        if (!ShouldClipForSmallMedia(screen))
        {
            return source;
        }

        var stringFormatType = ResolveType("Qud.UI.StringFormat", "StringFormat");
        var clipMethod = stringFormatType is null ? null : AccessTools.Method(stringFormatType, "ClipText", new[] { typeof(string), typeof(int) });
        return clipMethod?.Invoke(null, new object[] { source, 45 }) as string ?? source;
    }

    private static bool ShouldClipForSmallMedia(object? screen)
    {
        var mediaType = ResolveType("XRL.UI.Media", "Media");
        var sizeClassValue = mediaType is null ? null : GetStaticMemberValue(mediaType, "sizeClass");
        if (!string.Equals(sizeClassValue?.ToString(), "Small", StringComparison.Ordinal))
        {
            return false;
        }

        return GetIntMemberValue(screen, "CurrentCategory") != 2;
    }

    private static string GetNoEntriesText(object data)
    {
        var screen = GetMemberValue(data, "screen");
        var screenType = screen?.GetType();
        if (screenType is null)
        {
            return string.Empty;
        }

        return GetStaticMemberValue(screenType, "NO_ENTRIES_TEXT") as string ?? string.Empty;
    }

    private static void SetImageRenderable(object instance, object? renderable)
    {
        TrySetActive(GetMemberValue(instance, "imageContainer"), renderable is not null);
        if (renderable is not null)
        {
            _ = AccessTools.Method(GetMemberValue(instance, "image")?.GetType(), "FromRenderable")?.Invoke(GetMemberValue(instance, "image"), new[] { renderable });
        }
    }

    private static string TranslateVisibleText(string source, string route, string family) => UiBindingTranslationHelpers.TranslateVisibleText(source, route, family);

    private static string GetRequiredStringMemberValue(object instance, string memberName)
    {
        var value = GetStringMemberValue(instance, memberName);
        if (value is not null)
        {
            return value;
        }

        Trace.TraceWarning("QudJP: {0} missing string member '{1}' on '{2}'. Falling back to empty string.", Context, memberName, instance.GetType().FullName);
        return string.Empty;
    }

    private static string GetDisplayTextOrEmpty(object entry)
    {
        var value = InvokeStringMethod(entry, "GetDisplayText");
        if (value is not null)
        {
            return value;
        }

        Trace.TraceWarning("QudJP: {0} GetDisplayText() returned null on '{1}'. Falling back to empty string.", Context, entry.GetType().FullName);
        return string.Empty;
    }

    private static Type? ResolveType(string fullName, string simpleName)
    {
        var type = AccessTools.TypeByName(fullName);
        if (type is not null)
        {
            return type;
        }

        Trace.TraceWarning("QudJP: {0} failed to resolve type '{1}'. Trying simple name '{2}'.", Context, fullName, simpleName);
        return AccessTools.TypeByName(simpleName);
    }

    private static void SetContextData(object instance, object data)
    {
        var context = GetMemberValue(instance, "context");
        if (context is not null)
        {
            SetMemberValue(context, "data", data);
        }
    }

    private static void SetPaddingLeft(object? layoutGroup, int value)
    {
        if (layoutGroup is null)
        {
            return;
        }

        var padding = GetMemberValue(layoutGroup, "padding");
        if (padding is null)
        {
            return;
        }

        SetMemberValue(padding, "left", value);
    }

    private static void TrySetActive(object? target, bool active)
    {
        if (target is null)
        {
            return;
        }

        if (AccessTools.Method(target.GetType(), "SetActive", new[] { typeof(bool) }) is MethodInfo method)
        {
            _ = method.Invoke(target, new object[] { active });
            return;
        }

        SetMemberValue(target, "activeSelf", active);
        SetMemberValue(target, "Active", active);
    }

    private static bool HasMember(object instance, string memberName)
    {
        var type = instance.GetType();
        return AccessTools.Field(type, memberName) is not null || AccessTools.Property(type, memberName) is not null;
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

    private static object? GetMemberValue(object instance, string memberName) => UiBindingTranslationHelpers.GetMemberValue(instance, memberName);

    private static string? GetStringMemberValue(object instance, string memberName) => UiBindingTranslationHelpers.GetStringMemberValue(instance, memberName);

    private static bool GetBoolMemberValue(object? instance, string memberName)
    {
        if (instance is null)
        {
            return false;
        }

        var value = GetMemberValue(instance, memberName);
        return value as bool? ?? false;
    }

    private static int GetIntMemberValue(object? instance, string memberName)
    {
        if (instance is null)
        {
            return 0;
        }

        var value = GetMemberValue(instance, memberName);
        return value is int intValue ? intValue : 0;
    }

    private static string? InvokeStringMethod(object instance, string methodName, params object?[]? args)
    {
        return AccessTools.Method(instance.GetType(), methodName)?.Invoke(instance, args) as string;
    }

    private static bool InvokeBoolMethod(object instance, string methodName, params object?[]? args)
    {
        return AccessTools.Method(instance.GetType(), methodName)?.Invoke(instance, args) as bool? ?? false;
    }

    private static void SetMemberValue(object instance, string memberName, object? value) => UiBindingTranslationHelpers.SetMemberValue(instance, memberName, value);
}
