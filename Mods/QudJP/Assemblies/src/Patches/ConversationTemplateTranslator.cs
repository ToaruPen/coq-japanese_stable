using System;

namespace QudJP.Patches;

public static class ConversationTemplateTranslator
{
    private const string Route = nameof(ConversationTemplateTranslator);
    private static readonly string[] DroppableConversationVariableTokens =
    {
        "=player.formalAddressTerm=",
        "=player.reflexive=",
        "=player.species=",
        "=pronouns.formalAddressTerm=",
    };

    public static string TranslateTemplate(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source;
        }

        return ColorAwareTranslationComposer.TranslatePreservingColors(
            source,
            visible => TryTranslateVisibleTemplate(visible, out var translated)
                ? translated
                : visible);
    }

    private static bool TryTranslateVisibleTemplate(string source, out string translated)
    {
        if (StringHelpers.TryGetTranslationExactOrLowerAscii(source, out translated)
            && !string.Equals(source, translated, StringComparison.Ordinal))
        {
            translated = DropConversationVariableTokens(translated);
            DynamicTextObservability.RecordTransform(Route, "ConversationTemplate.Exact", source, translated);
            return true;
        }

        translated = source;
        return false;
    }

    private static string DropConversationVariableTokens(string source)
    {
        var result = source;
        for (var index = 0; index < DroppableConversationVariableTokens.Length; index++)
        {
            result = result.Replace(DroppableConversationVariableTokens[index], string.Empty);
        }

        return result.Trim();
    }
}
