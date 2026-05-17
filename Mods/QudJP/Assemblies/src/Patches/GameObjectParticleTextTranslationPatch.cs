using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class GameObjectParticleTextTranslationPatch
{
    private const string Context = nameof(GameObjectParticleTextTranslationPatch);

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (gameObjectType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            yield break;
        }

        foreach (var method in ResolveParticleTextOverloads(gameObjectType))
        {
            yield return method;
        }
    }

    public static bool Prefix(ref string Text)
    {
        try
        {
            _ = ParticleTextSemanticPipeline.TryTranslateParticleText(ref Text);
            return true;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Prefix failed: {1}", Context, ex);
            return true;
        }
    }

    private static IEnumerable<MethodBase> ResolveParticleTextOverloads(Type gameObjectType)
    {
        var signatures = new[]
        {
            new[] { typeof(string), typeof(float), typeof(int) },
            new[] { typeof(string), typeof(bool) },
            new[] { typeof(string), typeof(float), typeof(float), typeof(char), typeof(bool) },
            new[] { typeof(string), typeof(char), typeof(bool), typeof(float), typeof(float) },
        };

        foreach (var signature in signatures)
        {
            var method = AccessTools.Method(gameObjectType, "ParticleText", signature);
            if (method is not null)
            {
                yield return method;
            }
            else
            {
                Trace.TraceError("QudJP: {0}.ParticleText({1}) target not found.", Context, string.Join(", ", Array.ConvertAll(signature, static t => t.FullName)));
            }
        }
    }
}
