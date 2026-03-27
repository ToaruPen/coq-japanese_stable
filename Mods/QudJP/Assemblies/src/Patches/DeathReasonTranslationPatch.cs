using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

/// <summary>
/// Observes Reason and ThirdPersonReason parameters passed to GameObject.Die().
/// Producer-owned translations may arrive pre-marked; sink-side handling only strips markers and logs unclaimed text.
/// </summary>
[HarmonyPatch]
public static class DeathReasonTranslationPatch
{
    private const string Context = nameof(DeathReasonTranslationPatch);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var type = GameTypeResolver.FindType("XRL.World.GameObject", "GameObject");
        if (type is null)
        {
            Trace.TraceError("QudJP: {0} failed to resolve GameObject type.", Context);
            return null;
        }

        var method = AccessTools.Method(type, "Die");
        if (method is null)
        {
            Trace.TraceError("QudJP: {0} failed to resolve Die method.", Context);
        }

        return method;
    }

    public static void Prefix(ref string Reason, ref string ThirdPersonReason)
    {
        try
        {
            if (!string.IsNullOrEmpty(Reason))
            {
                var translated = TranslateDeathReason(Reason);
                if (!string.Equals(translated, Reason, StringComparison.Ordinal))
                {
                    DynamicTextObservability.RecordTransform(
                        Context, "DeathReason.Reason", Reason, translated);
                    Reason = translated;
                }
            }

            if (!string.IsNullOrEmpty(ThirdPersonReason))
            {
                var translated = TranslateDeathReason(ThirdPersonReason);
                if (!string.Equals(translated, ThirdPersonReason, StringComparison.Ordinal))
                {
                    DynamicTextObservability.RecordTransform(
                        Context, "DeathReason.ThirdPerson", ThirdPersonReason, translated);
                    ThirdPersonReason = translated;
                }
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Prefix failed: {1}", Context, ex);
        }
    }

    internal static string TranslateDeathReason(string reason)
    {
        return UITextSkinTranslationPatch.TranslatePreservingColors(reason, Context);
    }
}
