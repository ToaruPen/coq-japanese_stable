using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

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

    public static bool Prefix(object __instance, ref string Text, object[] __args)
    {
        try
        {
            if (!ParticleTextSemanticPipeline.TryTranslateParticleText(ref Text))
            {
                return true;
            }

            return !CombatJuiceFloatingTextRenderer.TryRender(__instance, Text, __args);
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

internal static class CombatJuiceFloatingTextRenderer
{
    private const char DefaultColor = 'W';
    private static readonly Type[] FloatingTextSignature =
    [
        typeof(object),
        typeof(string),
        typeof(Color),
        typeof(float),
        typeof(float),
        typeof(float),
        typeof(bool),
        typeof(object),
    ];

    private static CombatJuiceRenderer? rendererForTests;

    internal static void SetRendererForTests(CombatJuiceRenderer? renderer)
    {
        rendererForTests = renderer;
    }

    internal static bool TryRender(object? gameObject, string text, object[]? args)
    {
        if (gameObject is null || string.IsNullOrEmpty(text))
        {
            return false;
        }

        if (!ShouldRenderWhenTranslated(gameObject, args))
        {
            return false;
        }

        var cell = TryGetCurrentCell(gameObject);
        if (cell is null)
        {
            return false;
        }

        var colorCode = TryGetColorCodeFromArgs(args);
        var visibleText = StripLeadingColorCode(text, ref colorCode);
        if (string.IsNullOrEmpty(visibleText))
        {
            return false;
        }

        var color = ResolveQudColor(colorCode);
        var duration = 1.5f;
        if (TryGetFloatArg(args, 3, out var durationArg))
        {
            duration = durationArg;
        }

        var floatLength = 24f;
        if (TryGetFloatArg(args, 4, out var floatLengthArg))
        {
            floatLength = floatLengthArg;
        }

        return rendererForTests?.Invoke(cell, visibleText, color, duration, floatLength, 1f, true, gameObject)
            ?? TryInvokeCombatJuice(cell, visibleText, color, duration, floatLength, gameObject);
    }

    private static bool ShouldRenderWhenTranslated(object gameObject, object[]? args)
    {
        if (!TryGetIgnoreVisibility(args, out var ignoreVisibility) || ignoreVisibility)
        {
            return true;
        }

        var isVisible = AccessTools.Method(gameObject.GetType(), "IsVisible", Type.EmptyTypes);
        if (isVisible is null || isVisible.ReturnType != typeof(bool))
        {
            return true;
        }

        try
        {
            return isVisible.Invoke(gameObject, Array.Empty<object>()) is true;
        }
        catch (TargetInvocationException)
        {
            return false;
        }
    }

    private static bool TryGetIgnoreVisibility(object[]? args, out bool ignoreVisibility)
    {
        ignoreVisibility = false;
        if (args is null)
        {
            return false;
        }

        return TryGetBoolArg(args, 1, out ignoreVisibility)
            || TryGetBoolArg(args, 2, out ignoreVisibility)
            || TryGetBoolArg(args, 4, out ignoreVisibility);
    }

    private static bool TryGetBoolArg(object[] args, int index, out bool value)
    {
        if (index < args.Length && args[index] is bool boolValue)
        {
            value = boolValue;
            return true;
        }

        value = false;
        return false;
    }

    private static object? TryGetCurrentCell(object gameObject)
    {
        var getCurrentCell = AccessTools.Method(gameObject.GetType(), "GetCurrentCell", Type.EmptyTypes);
        if (getCurrentCell is null)
        {
            return null;
        }

        try
        {
            return getCurrentCell.Invoke(gameObject, Array.Empty<object>());
        }
        catch (TargetInvocationException)
        {
            return null;
        }
    }

    private static char TryGetColorCodeFromArgs(object[]? args)
    {
        if (args is null)
        {
            return DefaultColor;
        }

        return args.FirstOrDefault(static arg => arg is char and not ' ') is char colorCode
            ? colorCode
            : DefaultColor;
    }

    private static string StripLeadingColorCode(string text, ref char colorCode)
    {
        if (text.Length >= 2 && text[0] == '&')
        {
            colorCode = text[1];
            return text.Substring(2);
        }

        return text;
    }

    private static bool TryGetFloatArg(object[]? args, int index, out float value)
    {
        if (args is not null && index < args.Length && args[index] is float arg)
        {
            value = arg;
            return true;
        }

        value = 0f;
        return false;
    }

    private static bool TryInvokeCombatJuice(
        object cell,
        string text,
        Color color,
        float duration,
        float floatLength,
        object gameObject)
    {
        var combatJuiceType = AccessTools.TypeByName("CombatJuice");
        var floatingText = combatJuiceType?.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(static method => method.Name == "floatingText" && method.GetParameters().Length == FloatingTextSignature.Length);
        if (floatingText is null)
        {
            return false;
        }

        try
        {
            floatingText.Invoke(null, [cell, text, color, duration, floatLength, 1f, true, gameObject]);
            return true;
        }
        catch (TargetInvocationException ex)
        {
            Trace.TraceWarning("QudJP: CombatJuice floating text render failed: {0}", ex.InnerException ?? ex);
            return false;
        }
        catch (ArgumentException ex)
        {
            Trace.TraceWarning("QudJP: CombatJuice floating text render argument mismatch: {0}", ex);
            return false;
        }
    }

    private static Color ResolveQudColor(char colorCode)
    {
        var colorUtilityType = AccessTools.TypeByName("ConsoleLib.Console.ColorUtility");
        var colorMap = AccessTools.Field(colorUtilityType, "ColorMap")?.GetValue(null);
        if (TryReadColorMap(colorMap, colorCode, out var color))
        {
            return color;
        }

        return colorCode switch
        {
            'W' => new Color { r = 1f, g = 1f, b = 0f, a = 1f },
            'Y' => new Color { r = 1f, g = 1f, b = 1f, a = 1f },
            'R' => new Color { r = 1f, g = 0f, b = 0f, a = 1f },
            'G' => new Color { r = 0f, g = 1f, b = 0f, a = 1f },
            'B' => new Color { r = 0f, g = 0f, b = 1f, a = 1f },
            _ => new Color { r = 1f, g = 1f, b = 1f, a = 1f },
        };
    }

    private static bool TryReadColorMap(object? colorMap, char colorCode, out Color color)
    {
        if (colorMap is IDictionary dictionary && dictionary.Contains(colorCode) && dictionary[colorCode] is Color mappedColor)
        {
            color = mappedColor;
            return true;
        }

        color = default;
        return false;
    }

    internal delegate bool CombatJuiceRenderer(
        object cell,
        string text,
        Color color,
        float duration,
        float floatLength,
        float scale,
        bool ignoreVisibility,
        object gameObject);
}
