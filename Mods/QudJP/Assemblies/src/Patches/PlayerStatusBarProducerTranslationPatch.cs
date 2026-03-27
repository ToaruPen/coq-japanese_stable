using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class PlayerStatusBarProducerTranslationPatch
{
    private const string Context = nameof(PlayerStatusBarProducerTranslationPatch);

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = GameTypeResolver.FindType("Qud.UI.PlayerStatusBar", "PlayerStatusBar");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            yield break;
        }

        var coreType = AccessTools.TypeByName("XRL.Core.XRLCore");
        var beginEndTurn = coreType is not null
            ? AccessTools.Method(targetType, "BeginEndTurn", new[] { coreType })
            : null;
        var update = AccessTools.Method(targetType, "Update", Type.EmptyTypes);

        if (beginEndTurn is not null)
        {
            yield return beginEndTurn;
        }
        else
        {
            Trace.TraceWarning("QudJP: {0} failed to resolve PlayerStatusBar.BeginEndTurn(XRLCore).", Context);
        }

        if (update is not null)
        {
            yield return update;
        }
        else
        {
            Trace.TraceWarning("QudJP: {0} failed to resolve PlayerStatusBar.Update().", Context);
        }
    }

    public static void Postfix(object __instance, MethodBase __originalMethod)
    {
        try
        {
            if (string.Equals(__originalMethod.Name, "BeginEndTurn", StringComparison.Ordinal))
            {
                TranslatePlayerStringData(__instance);
                return;
            }

            if (string.Equals(__originalMethod.Name, "Update", StringComparison.Ordinal))
            {
                TranslateXpBar(__instance);
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }

    private static void TranslatePlayerStringData(object instance)
    {
        var field = AccessTools.Field(instance.GetType(), "playerStringData");
        if (field?.GetValue(instance) is not IDictionary dictionary)
        {
            return;
        }

        var keys = new List<object>();
        foreach (var key in dictionary.Keys.Cast<object>().Where(static key => key is not null))
        {
            keys.Add(key);
        }

        for (var index = 0; index < keys.Count; index++)
        {
            var key = keys[index];
            if (dictionary[key] is not string source || string.IsNullOrEmpty(source))
            {
                continue;
            }

            var fieldName = key.ToString();
            if (string.IsNullOrEmpty(fieldName))
            {
                continue;
            }

            var translated = PlayerStatusBarProducerTranslationHelpers.TranslateStringDataValue(
                fieldName,
                source,
                $"{Context}.{fieldName}");
            if (!string.Equals(translated, source, StringComparison.Ordinal))
            {
                dictionary[key] = translated;
            }
        }
    }

    private static void TranslateXpBar(object instance)
    {
        var xpBarField = AccessTools.Field(instance.GetType(), "XPBar");
        var xpBar = xpBarField?.GetValue(instance);
        if (xpBar is null)
        {
            return;
        }

        var textField = AccessTools.Field(xpBar.GetType(), "text");
        var textObject = textField?.GetValue(xpBar);
        var current = UITextSkinReflectionAccessor.GetCurrentText(textObject, Context);
        if (string.IsNullOrEmpty(current))
        {
            return;
        }

        var currentText = current!;
        var translated = PlayerStatusBarProducerTranslationHelpers.TranslateXpBarText(
            currentText,
            $"{Context}.XPBar");
        if (!string.Equals(translated, currentText, StringComparison.Ordinal))
        {
            _ = UITextSkinReflectionAccessor.SetCurrentText(textObject, translated, Context);
        }
    }
}
