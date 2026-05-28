using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class CharGenMenuOptionOwnerTranslationPatch
{
    private const string Context = nameof(CharGenMenuOptionOwnerTranslationPatch);

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var summaryMenuBar = ResolveMethod("XRL.CharacterBuilds.Qud.UI.QudBuildSummaryModuleWindow", "GetKeyMenuBar");
        if (summaryMenuBar is not null)
        {
            yield return summaryMenuBar;
        }

        var mutationsMenuBar = ResolveMethod("XRL.CharacterBuilds.Qud.UI.QudMutationsModuleWindow", "GetKeyMenuBar");
        if (mutationsMenuBar is not null)
        {
            yield return mutationsMenuBar;
        }

        var buildLibrarySelections = ResolveMethod("XRL.CharacterBuilds.Qud.UI.QudBuildLibraryModuleWindow", "GetSelections");
        if (buildLibrarySelections is not null)
        {
            yield return buildLibrarySelections;
        }

        var buildLibraryMenuBar = ResolveMethod("XRL.CharacterBuilds.Qud.UI.QudBuildLibraryModuleWindow", "GetKeyMenuBar");
        if (buildLibraryMenuBar is not null)
        {
            yield return buildLibraryMenuBar;
        }

        var customizePets = ResolveMethod("XRL.CharacterBuilds.Qud.UI.QudCustomizeCharacterModuleWindow", "GetPets");
        if (customizePets is not null)
        {
            yield return customizePets;
        }

        var gamemodeSelections = ResolveMethod("XRL.CharacterBuilds.Qud.UI.QudGamemodeModuleWindow", "GetSelections");
        if (gamemodeSelections is not null)
        {
            yield return gamemodeSelections;
        }

        var gamemodeMenuBar = ResolveMethod("XRL.CharacterBuilds.Qud.UI.QudGamemodeModuleWindow", "GetKeyMenuBar");
        if (gamemodeMenuBar is not null)
        {
            yield return gamemodeMenuBar;
        }

        var attributesMenuBar = ResolveMethod("XRL.CharacterBuilds.Qud.UI.QudAttributesModuleWindow", "GetKeyMenuBar");
        if (attributesMenuBar is not null)
        {
            yield return attributesMenuBar;
        }
    }

    public static void Postfix(ref IEnumerable __result, MethodBase __originalMethod)
    {
        try
        {
            if (__result is null)
            {
                return;
            }

            if (string.Equals(__originalMethod.Name, "GetSelections", StringComparison.Ordinal))
            {
                __result = TranslateChoiceTitlesAndDescriptions(__result);
                return;
            }

            __result = TranslateMenuOptions(__result);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }

    internal static IEnumerable TranslateMenuOptions(IEnumerable values)
    {
        return CharGenProducerTranslationHelpers.MaterializeTranslatedEnumerable(
            values,
            "Description",
            Context + ".MenuOption.Description",
            TranslateOwnerText);
    }

    internal static IEnumerable TranslateChoiceTitles(IEnumerable values)
    {
        return CharGenProducerTranslationHelpers.MaterializeTranslatedEnumerable(
            values,
            "Title",
            Context + ".Choice.Title",
            TranslateOwnerText);
    }

    internal static IEnumerable TranslateChoiceTitlesAndDescriptions(IEnumerable values)
    {
        var translatedTitles = TranslateChoiceTitles(values);
        return CharGenProducerTranslationHelpers.MaterializeTranslatedEnumerable(
            translatedTitles,
            "Description",
            Context + ".Choice.Description",
            TranslateOwnerText);
    }

    private static string TranslateOwnerText(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            return markedText;
        }

        var translated = CharGenProducerTranslationHelpers.TranslateText(source);
        if (!string.Equals(translated, source, StringComparison.Ordinal))
        {
            return translated;
        }

        return string.Equals(source, "[Debug] Quickstart", StringComparison.Ordinal)
            ? "[デバッグ] クイックスタート"
            : source;
    }

    private static MethodBase? ResolveMethod(string typeName, string methodName)
    {
        var type = AccessTools.TypeByName(typeName);
        if (type is null)
        {
            Trace.TraceError("QudJP: {0} target type not found: {1}.", Context, typeName);
            return null;
        }

        var method = AccessTools.Method(type, methodName, Type.EmptyTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0} target method not found: {1}.{2}().", Context, typeName, methodName);
            return null;
        }

        return method;
    }
}
