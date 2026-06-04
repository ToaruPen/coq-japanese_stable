using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class QuestsLineTranslationPatch
{
    private const string Context = nameof(QuestsLineTranslationPatch);
    private const string DictionaryFile = "ui-quests.ja.json";
    private static readonly Regex QuestTitlePrefixPattern =
        new Regex("^(?<prefix>\\[[+-]\\]\\s*)(?<title>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        return FrameworkDataElementSetDataTargetResolver.Resolve(Context, "Qud.UI.QuestsLine", "QuestsLine");
    }

    public static void Postfix(object? __instance)
    {
        try
        {
            if (__instance is null)
            {
                return;
            }

            var type = __instance.GetType();
            TranslateStaticMenuOptions(type);
            TranslateExactTextField(__instance, "titleText", "titleText", "QuestsLine.TitleText");
            TranslateGiverText(__instance, "giverText", "giverText");
            TranslateBodyText(__instance, "bodyText", "bodyText");
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: QuestsLineTranslationPatch.Postfix failed: {0}", ex);
        }
    }

    private static void TranslateStaticMenuOptions(Type instanceType)
    {
        TranslateMenuOptionCollection(GetStaticMemberValue(instanceType, "categoryExpandOptions"), "categoryExpandOptions");
        TranslateMenuOptionCollection(GetStaticMemberValue(instanceType, "categoryCollapseOptions"), "categoryCollapseOptions");
    }

    private static void TranslateMenuOptionCollection(object? maybeCollection, string routeSuffix)
    {
        if (maybeCollection is null || maybeCollection is string || maybeCollection is not IEnumerable enumerable)
        {
            return;
        }

        var index = 0;
        foreach (var item in enumerable)
        {
            if (item is null)
            {
                index++;
                continue;
            }

            var current = GetStringMemberValue(item, "Description");
            if (!string.IsNullOrEmpty(current))
            {
                var route = ObservabilityHelpers.ComposeContext(Context, "field=" + routeSuffix + "[" + index + "]");
                var translated = TranslateExactLeaf(current!, route, "QuestsLine.MenuOption");
                if (!string.Equals(translated, current, StringComparison.Ordinal))
                {
                    SetMemberValue(item, "Description", translated);
                }
            }

            index++;
        }
    }

    private static void TranslateExactTextField(object instance, string memberName, string routeSuffix, string family)
    {
        var uiTextSkin = GetMemberValue(instance, memberName);
        var current = UITextSkinReflectionAccessor.GetCurrentText(uiTextSkin, Context);
        if (string.IsNullOrEmpty(current))
        {
            return;
        }

        var route = ObservabilityHelpers.ComposeContext(Context, "field=" + routeSuffix);
        var translated = TranslateExactLeaf(current!, route, family);
        if (string.Equals(translated, current, StringComparison.Ordinal))
        {
            translated = GeneratedQuestTitleTranslator.TranslateEmbeddedPreservingColors(current!, route);
        }

        if (string.Equals(translated, current, StringComparison.Ordinal) && family == "QuestsLine.TitleText")
        {
            translated = TranslateQuestTitle(current!, route);
        }

        if (string.Equals(translated, current, StringComparison.Ordinal))
        {
            return;
        }

        OwnerTextSetter.SetTranslatedText(uiTextSkin, current!, translated, Context, typeof(QuestsLineTranslationPatch));
    }

    private static string TranslateQuestTitle(string source, string route)
    {
        var match = QuestTitlePrefixPattern.Match(source);
        if (!match.Success)
        {
            return source;
        }

        var title = match.Groups["title"].Value;
        var translatedTitle = TranslateExactLeaf(title, route, "QuestsLine.TitleText");
        if (string.Equals(translatedTitle, title, StringComparison.Ordinal)
            && (!DynamicQuestGeneratedQuestTextTranslator.TryTranslate(title, out translatedTitle)
                || string.Equals(translatedTitle, title, StringComparison.Ordinal)))
        {
            return source;
        }

        return match.Groups["prefix"].Value + translatedTitle;
    }

    private static void TranslateGiverText(object instance, string memberName, string routeSuffix)
    {
        var uiTextSkin = GetMemberValue(instance, memberName);
        var current = UITextSkinReflectionAccessor.GetCurrentText(uiTextSkin, Context);
        if (string.IsNullOrEmpty(current))
        {
            return;
        }

        var parts = current!.Split(new[] { " / " }, StringSplitOptions.None);
        var changed = false;
        for (var index = 0; index < parts.Length; index++)
        {
            var route = ObservabilityHelpers.ComposeContext(Context, "field=" + routeSuffix + "[" + index + "]");
            var translated = TranslateGiverPart(parts[index], route, index);
            if (string.Equals(translated, parts[index], StringComparison.Ordinal))
            {
                continue;
            }

            parts[index] = translated;
            changed = true;
        }

        if (!changed)
        {
            return;
        }

        var translatedText = string.Join(" / ", parts);
        OwnerTextSetter.SetTranslatedText(uiTextSkin, current!, translatedText, Context, typeof(QuestsLineTranslationPatch));
    }

    private static string TranslateGiverPart(string source, string route, int index)
    {
        var translated = TranslateExactLeaf(source, route, "QuestsLine.GiverText");
        if (!string.Equals(translated, source, StringComparison.Ordinal))
        {
            return translated;
        }

        if (index == 0)
        {
            translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(source, route);
            return string.Equals(translated, source, StringComparison.Ordinal) ? source : translated;
        }

        if (MessageLogProducerTranslationHelpers.TryTranslateZoneDisplayName(source, route, out translated)
            && !string.Equals(translated, source, StringComparison.Ordinal))
        {
            return translated;
        }

        return source;
    }

    private static void TranslateBodyText(object instance, string memberName, string routeSuffix)
    {
        var uiTextSkin = GetMemberValue(instance, memberName);
        var current = UITextSkinReflectionAccessor.GetCurrentText(uiTextSkin, Context);
        if (string.IsNullOrEmpty(current))
        {
            return;
        }

        var currentText = current!;
        var newline = currentText.Contains("\r\n") ? "\r\n" : "\n";
        var normalized = newline == "\r\n"
            ? currentText.Replace("\r\n", "\n")
            : currentText;
        var lines = normalized.Split(new[] { '\n' }, StringSplitOptions.None);
        var changed = false;
        for (var index = 0; index < lines.Length; index++)
        {
            var route = ObservabilityHelpers.ComposeContext(Context, "field=" + routeSuffix + "[" + index + "]");
            var translated = QuestLogTranslationPatch.TranslateQuestLogLine(lines[index], route);
            if (string.Equals(translated, lines[index], StringComparison.Ordinal))
            {
                continue;
            }

            lines[index] = translated;
            changed = true;
        }

        if (!changed)
        {
            return;
        }

        var translatedText = string.Join(newline, lines);
        OwnerTextSetter.SetTranslatedText(uiTextSkin, currentText, translatedText, Context, typeof(QuestsLineTranslationPatch));
    }

    private static string TranslateExactLeaf(string source, string route, string family)
    {
        var translated = ScopedDictionaryLookup.TranslateExactOrLowerAscii(source, DictionaryFile);
        if (translated is null || string.Equals(translated, source, StringComparison.Ordinal))
        {
            return source;
        }

        DynamicTextObservability.RecordTransform(route, family, source, translated);
        return translated;
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

    private static void SetMemberValue(object instance, string memberName, object? value) => UiBindingTranslationHelpers.SetMemberValue(instance, memberName, value);
}
