using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class MissileWeaponShowPickerTranslationPatch
{
    private const string Context = nameof(MissileWeaponShowPickerTranslationPatch);

    private static readonly HashSet<string> TranslatableLiterals = new(StringComparer.Ordinal);

    private static readonly MethodInfo TranslateLiteralMethod =
        AccessTools.Method(typeof(MissileWeaponShowPickerTranslationPatch), nameof(TranslateLiteral))
        ?? throw new InvalidOperationException("TranslateLiteral method not found.");

    private static readonly MethodInfo TranslateRenderedMethod =
        AccessTools.Method(typeof(MissileWeaponShowPickerTranslationPatch), nameof(TranslateRendered))
        ?? throw new InvalidOperationException("TranslateRendered method not found.");

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var missileWeaponType = GameTypeResolver.FindType("XRL.World.Parts.MissileWeapon", "MissileWeapon");
        var allowVisType = GameTypeResolver.FindType("XRL.World.AllowVis", "AllowVis");
        var gameObjectType = GameTypeResolver.FindType("XRL.World.GameObject", "GameObject");
        var fireType = GameTypeResolver.FindType("XRL.World.Parts.FireType", "FireType");
        if (missileWeaponType is null || allowVisType is null || gameObjectType is null || fireType is null)
        {
            Trace.TraceError("QudJP: {0} target parameter types not found.", Context);
            return targets;
        }

        var method = AccessTools.Method(
            missileWeaponType,
            "ShowPicker",
            new[]
            {
                typeof(int),
                typeof(int),
                typeof(bool),
                allowVisType,
                typeof(int),
                typeof(bool),
                gameObjectType,
                fireType.MakeByRefType(),
                typeof(int),
            });
        if (method is not null)
        {
            targets.Add(method);
        }
        else
        {
            Trace.TraceError("QudJP: {0}.MissileWeapon.ShowPicker target not found.", Context);
        }

        return targets;
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        try
        {
            return LegacyGamepadPromptTranspilerHelpers.Apply(
                instructions,
                TranslatableLiterals,
                TranslateLiteralMethod,
                TranslateRenderedMethod,
                Context);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Transpiler failed: {1}", Context, ex);
            return instructions;
        }
    }

    public static string TranslateLiteral(string source)
    {
        return source;
    }

    public static string TranslateRendered(string source)
    {
        try
        {
            return LegacyGamepadPromptTranslationHelpers.TranslateMissileWeaponShowPickerRendered(source);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.TranslateRendered failed: {1}", Context, ex);
            return source;
        }
    }
}
