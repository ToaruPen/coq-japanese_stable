using System;

namespace QudJP.Patches;

internal static class MemorialInscriptionIntroTranslator
{
    internal static bool TryTranslate(string? source, out string translated)
    {
        if (string.IsNullOrEmpty(source))
        {
            if (source is null)
            {
                translated = string.Empty;
                return false;
            }

            translated = source;
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return false;
        }

        var original = source!;
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(original);
        var translatedCore = stripped switch
        {
            "Here Lies" => "ここに眠る",
            "Rest in Peace" => "安らかに眠れ",
            "Here Rests" => "ここに憩う",
            "Here Lies the Body of" => "ここにその身を横たえる",
            "Here Rests the Body of" => "ここにその身を休める",
            "In Memory of" => "追憶",
            "In Loving Memory of" => "愛しき追憶",
            "Here Lie the Remains of" => "ここに遺骸眠る",
            "Here Rests in the Light of Friends" => "友らの光の中に眠る",
            "Rest with Friends" => "友らとともに眠れ",
            "Under this Reef Lies" => "この礁の下に眠る",
            "Dream by the Light of Our Freehold" => "われらの自由保有地の光に夢見よ",
            "Here Rests in the Light of Gjaus" => "ジャウスの光の中に眠る",
            "Here Sheltered under Gjaus is" => "ジャウスの庇護の下、ここに眠る",
            "Rest in the Light of Gjaus" => "ジャウスの光の中で眠れ",
            "Dream in Peace" => "安らかに夢見よ",
            "Dream in the Light of Gjaus" => "ジャウスの光の中に夢見よ",
            _ => null,
        };
        if (translatedCore is null)
        {
            translated = original;
            return false;
        }

        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translatedCore,
            spans,
            stripped.Length,
            original);
        return true;
    }
}
