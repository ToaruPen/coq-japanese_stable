using System;
using System.Diagnostics;

namespace QudJP.Patches;

internal static class ParticleTextSemanticPipeline
{
    private static readonly ParticleTextTranslator[] Translators =
    [
        JoppaZealotTranslationPatch.TryTranslateParticleText,
        SixDayZealotTranslationPatch.TryTranslateParticleText,
        ErosTeleportationTranslationPatch.TryTranslateParticleText,
        LongBladesCoreTranslationPatch.TryTranslateParticleText,
        PreacherHomilyTranslationPatch.TryTranslateParticleText,
        CanticlesChromaicParticleTextTranslationPatch.TryTranslateParticleText,
    ];

    internal static bool TryTranslateParticleText(ref string text)
    {
        for (var index = 0; index < Translators.Length; index++)
        {
            if (TryTranslateParticleTextWithFallback(Translators[index], ref text))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryTranslateParticleTextWithFallback(ParticleTextTranslator translator, ref string text)
    {
        var source = text;

        try
        {
            return translator(ref text);
        }
        catch (Exception ex)
        {
            text = source;
            Trace.TraceError(
                "QudJP: ParticleTextSemanticPipeline translator {0} failed: {1}",
                FormatTranslatorName(translator),
                ex);
            return false;
        }
    }

    private static string FormatTranslatorName(Delegate translator)
    {
        return translator.Method.DeclaringType?.FullName ?? translator.Method.Name;
    }

    private delegate bool ParticleTextTranslator(ref string text);
}
