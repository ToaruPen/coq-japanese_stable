using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class SifrahPureOwnerPopupTranslationPatch
{
    private const string Context = nameof(SifrahPureOwnerPopupTranslationPatch);

    private static readonly Regex BaetylOfferingPattern = new(
        "^You have no usable options to employ for performing an offering to (?<target>.+), giving you no chance of doing so well\\. You can remedy this situation by improving your Intelligence or by obtaining items useful in such a ritual\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex FormalWaterRitualPattern = new(
        "^You have no usable options to employ for performing the formal water ritual with (?<target>.+), giving you no chance of doing so well\\. You can remedy this situation by improving your Ego or by obtaining items useful in such a ritual\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex HagglingPattern = new(
        "^You have no usable options to employ for haggling with (?<target>.+), giving you no chance of success\\. You can remedy this situation by improving your Ego and social skills, or by obtaining items useful in social situations\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ItemModdingPattern = new(
        "^You have no usable options to employ for modding (?<target>.+), giving you no chance of success\\. You can remedy this situation by improving your Intelligence and tinkering skills, or by obtaining items useful for tinkering\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ItemNamingPattern = new(
        "^You have no usable options to employ for ritually naming (?<target>.+), giving you no chance of performing well\\. You can remedy this situation by improving your Ego, Willpower, and esoteric skills, or by obtaining items useful in ritual\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ReverseEngineeringPattern = new(
        "^You have no usable options to employ for reverse engineering (?<target>.+), giving you no chance of success\\. You can remedy this situation by improving your Intelligence and tinkering skills, or by obtaining items useful for tinkering\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ReverseEngineeringEarlyExitPattern = new(
        "^Exiting will still disassemble (?<target>.+), and will result in an attempt at reverse engineering as matters stand\\. Do you still want to exit\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AttributeSacrificePattern = new(
        "^Your (?<value>.+) is too depleted to do that\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex InvokeHigherBeingPattern = new(
        "^You have blasphemed against (?<value>.+)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SocialSecretPattern = new(
        "^You do not have any (?<more>more )?secrets (?<target>.+?) (?<verb>is|are) interested in\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex TinkeringBitPattern = new(
        "^You do not have any (?<more>more )?(?<value>.+)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex TinkeringChargeWithGenerationPattern = new(
        "^You do not have any energy cells with (?<amount>.+) charge available, and your electrical generation capacity is unable to meet the demand\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex TinkeringChargePattern = new(
        "^You do not have any energy cells with (?<amount>.+) charge available\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex TinkeringComputePowerPattern = new(
        "^You do not have (?<amount>.+) (?<unit>unit|units) of compute power available on the local lattice\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex TinkeringLiquidPattern = new(
        "^You do not have any (?<more> ?more)?(?<value>.+)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SifrahChosenCorrectPattern = new(
        "^You have already chosen the correct option for (?<value>.+)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SifrahEliminatedPattern = new(
        "^You have already eliminated (?<value>.+) as a possibility\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SifrahDisabledPattern = new(
        "^Choosing (?<value>.+) is disabled for this turn\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex[] CandidatePatterns =
    [
        BaetylOfferingPattern,
        FormalWaterRitualPattern,
        HagglingPattern,
        ItemModdingPattern,
        ItemNamingPattern,
        ReverseEngineeringPattern,
        ReverseEngineeringEarlyExitPattern,
        TinkeringChargeWithGenerationPattern,
        TinkeringChargePattern,
        TinkeringComputePowerPattern,
        SifrahChosenCorrectPattern,
        SifrahEliminatedPattern,
        SifrahDisabledPattern,
    ];

    [ThreadStatic]
    private static int activeDepth;

    [ThreadStatic]
    private static string? activeDeclaringType;

    [ThreadStatic]
    private static string? activeMemberName;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (gameObjectType is null)
        {
            Trace.TraceError("QudJP: {0} failed to resolve GameObject.", Context);
            yield break;
        }

        var intType = typeof(int);
        var tinkerDataType = AccessTools.TypeByName("XRL.World.Tinkering.TinkerData");
        var sifrahGameType = AccessTools.TypeByName("XRL.SifrahGame");
        var sifrahSlotType = AccessTools.TypeByName("XRL.SifrahSlot");
        if (tinkerDataType is null || sifrahGameType is null || sifrahSlotType is null)
        {
            Trace.TraceError("QudJP: {0} failed to resolve a Sifrah support type.", Context);
            yield break;
        }

        foreach (var target in new[]
                 {
                     new TargetConstructor("XRL.World.BaetylOfferingSifrah", [gameObjectType, intType, intType]),
                     new TargetConstructor("XRL.World.FormalWaterRitualSifrah", [gameObjectType]),
                     new TargetConstructor("XRL.World.HagglingSifrah", [gameObjectType]),
                     new TargetConstructor("XRL.World.ItemModdingSifrah", [gameObjectType, intType, intType, intType]),
                     new TargetConstructor("XRL.World.ItemNamingSifrah", [gameObjectType, intType, intType]),
                     new TargetConstructor(
                         "XRL.World.ReverseEngineeringSifrah",
                         [
                             gameObjectType,
                             intType,
                             intType,
                             intType,
                             tinkerDataType,
                         ]),
                 })
        {
            if (Array.Exists(target.ParameterTypes, static parameterType => parameterType is null))
            {
                Trace.TraceError("QudJP: {0} failed to resolve a constructor parameter for {1}.", Context, target.TypeName);
                continue;
            }

            var type = AccessTools.TypeByName(target.TypeName);
            if (type is null)
            {
                Trace.TraceError("QudJP: {0} failed to resolve {1}.", Context, target.TypeName);
                continue;
            }

            var constructor = AccessTools.Constructor(type, target.ParameterTypes);
            if (constructor is not null)
            {
                yield return constructor;
            }
            else
            {
                Trace.TraceError("QudJP: {0} failed to resolve {1} constructor.", Context, target.TypeName);
            }
        }

        foreach (var target in new[]
                 {
                     new TargetMethod("XRL.World.ReverseEngineeringSifrah", "CheckEarlyExit", [gameObjectType]),
                     new TargetMethod(
                         "XRL.World.RitualSifrahTokenAttributeSacrifice",
                         "CheckTokenUse",
                         SifrahTokenCheckParameterTypes(sifrahGameType, sifrahSlotType, gameObjectType)),
                     new TargetMethod(
                         "XRL.World.RitualSifrahTokenInvokeHigherBeing",
                         "CheckTokenUse",
                         SifrahTokenCheckParameterTypes(sifrahGameType, sifrahSlotType, gameObjectType)),
                     new TargetMethod(
                         "XRL.World.SocialSifrahTokenSecret",
                         "CheckTokenUse",
                         SifrahTokenCheckParameterTypes(sifrahGameType, sifrahSlotType, gameObjectType)),
                     new TargetMethod(
                         "XRL.World.TinkeringSifrahTokenBit",
                         "CheckTokenUse",
                         SifrahTokenCheckParameterTypes(sifrahGameType, sifrahSlotType, gameObjectType)),
                     new TargetMethod(
                         "XRL.World.TinkeringSifrahTokenCharge",
                         "CheckTokenUse",
                         SifrahTokenCheckParameterTypes(sifrahGameType, sifrahSlotType, gameObjectType)),
                     new TargetMethod(
                         "XRL.World.TinkeringSifrahTokenComputePower",
                         "CheckTokenUse",
                         SifrahTokenCheckParameterTypes(sifrahGameType, sifrahSlotType, gameObjectType)),
                     new TargetMethod(
                         "XRL.World.TinkeringSifrahTokenLiquid",
                         "CheckTokenUse",
                         SifrahTokenCheckParameterTypes(sifrahGameType, sifrahSlotType, gameObjectType)),
                     new TargetMethod("XRL.SifrahGame", "MakeMoveForSlot", [intType, gameObjectType]),
                 })
        {
            if (Array.Exists(target.ParameterTypes, static parameterType => parameterType is null))
            {
                Trace.TraceError("QudJP: {0} failed to resolve a method parameter for {1}.{2}.", Context, target.TypeName, target.MethodName);
                continue;
            }

            var type = AccessTools.TypeByName(target.TypeName);
            if (type is null)
            {
                Trace.TraceError("QudJP: {0} failed to resolve {1}.", Context, target.TypeName);
                continue;
            }

            var method = AccessTools.Method(type, target.MethodName, target.ParameterTypes);
            if (method is not null)
            {
                yield return method;
            }
            else
            {
                Trace.TraceError("QudJP: {0} failed to resolve {1}.{2}.", Context, target.TypeName, target.MethodName);
            }
        }
    }

    internal static void Prefix(MethodBase __originalMethod, out OwnerContextState? __state)
    {
        try
        {
            __state = new OwnerContextState(activeDeclaringType, activeMemberName);
            activeDepth++;
            activeDeclaringType = __originalMethod.DeclaringType?.FullName;
            activeMemberName = __originalMethod.Name;
        }
        catch (Exception ex)
        {
            __state = null;
            Trace.TraceError("QudJP: {0}.Prefix failed: {1}", Context, ex);
        }
    }

    internal static Exception? Finalizer(Exception? __exception, OwnerContextState? __state)
    {
        try
        {
            if (activeDepth > 0)
            {
                activeDepth--;
            }

            if (__state is not null)
            {
                activeDeclaringType = __state.PreviousDeclaringType;
                activeMemberName = __state.PreviousMemberName;
            }

            if (activeDepth == 0)
            {
                activeDeclaringType = null;
                activeMemberName = null;
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Finalizer failed: {1}", Context, ex);
        }

        return __exception;
    }

    internal static bool TryTranslatePopupMessage(string source, string route, string family, out string translated)
    {
        if (activeDepth <= 0 || string.IsNullOrEmpty(source))
        {
            translated = source;
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return true;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        return TryTranslate(
                   BaetylOfferingPattern,
                   source,
                   stripped,
                   spans,
                   route,
                   family,
                   "BaetylOffering",
                   target => target + "に捧げ物をするために使用できる選択肢がなく、うまく行う見込みがない。知性を高めるか、そのような儀式に役立つアイテムを入手すれば、この状況を改善できる。",
                   out translated)
               || TryTranslate(
                   FormalWaterRitualPattern,
                   source,
                   stripped,
                   spans,
                   route,
                   family,
                   "FormalWaterRitual",
                   target => target + "との正式な水の儀式に使用できる選択肢がなく、うまく行う見込みがない。エゴを高めるか、そのような儀式に役立つアイテムを入手すれば、この状況を改善できる。",
                   out translated)
               || TryTranslate(
                   HagglingPattern,
                   source,
                   stripped,
                   spans,
                   route,
                   family,
                   "Haggling",
                   target => target + "と値段交渉するために使用できる選択肢がなく、成功する見込みがない。エゴや社交スキルを高めるか、社交的な状況に役立つアイテムを入手すれば、この状況を改善できる。",
                   out translated)
               || TryTranslate(
                   ItemModdingPattern,
                   source,
                   stripped,
                   spans,
                   route,
                   family,
                   "ItemModding",
                   target => target + "を改造するために使用できる選択肢がなく、成功する見込みがない。知性や工作スキルを高めるか、工作に役立つアイテムを入手すれば、この状況を改善できる。",
                   out translated)
               || TryTranslate(
                   ItemNamingPattern,
                   source,
                   stripped,
                   spans,
                   route,
                   family,
                   "ItemNaming",
                   target => target + "に儀式的に名付けるために使用できる選択肢がなく、うまく行う見込みがない。エゴ、意志力、秘教系スキルを高めるか、儀式に役立つアイテムを入手すれば、この状況を改善できる。",
                   out translated)
               || IsActiveOwner("XRL.World.ReverseEngineeringSifrah", ".ctor", "ReverseEngineeringSifrah")
               && TryTranslate(
                   ReverseEngineeringPattern,
                   source,
                   stripped,
                   spans,
                   route,
                   family,
                   "ReverseEngineering",
                   target => target + "をリバースエンジニアリングするために使用できる選択肢がなく、成功する見込みがない。知性や工作スキルを高めるか、工作に役立つアイテムを入手すれば、この状況を改善できる。",
                   out translated)
               || IsActiveOwner("XRL.World.ReverseEngineeringSifrah", "CheckEarlyExit", "ReverseEngineeringCheckEarlyExit")
               && TryTranslate(
                   ReverseEngineeringEarlyExitPattern,
                   source,
                   stripped,
                   spans,
                   route,
                   family,
                   "ReverseEngineeringEarlyExit",
                   target => "終了しても" + target + "は分解され、現状のままリバースエンジニアリングを試みることになる。それでも終了する？",
                   out translated)
               || IsActiveOwner("XRL.World.RitualSifrahTokenAttributeSacrifice", "CheckTokenUse", "RitualAttributeSacrificeCheckTokenUse")
               && TryTranslate(
                   AttributeSacrificePattern,
                   source,
                   stripped,
                   spans,
                   route,
                   family,
                   "AttributeSacrifice",
                   value => value + "が消耗しすぎているため、それはできない。",
                   out translated)
               || IsActiveOwner("XRL.World.RitualSifrahTokenInvokeHigherBeing", "CheckTokenUse", "RitualInvokeHigherBeingCheckTokenUse")
               && TryTranslate(
                   InvokeHigherBeingPattern,
                   source,
                   stripped,
                   spans,
                   route,
                   family,
                   "InvokeHigherBeing",
                   value => value + "に冒涜を働いた。",
                   out translated)
               || IsActiveOwner("XRL.World.SocialSifrahTokenSecret", "CheckTokenUse", "SocialSecretCheckTokenUse")
               && TryTranslate(
                   SocialSecretPattern,
                   source,
                   stripped,
                   spans,
                   route,
                   family,
                   "SocialSecret",
                   target => target + "が興味を持つ秘密を持っていない。",
                   out translated)
               || IsActiveOwner("XRL.World.TinkeringSifrahTokenCharge", "CheckTokenUse", "TinkeringChargeCheckTokenUse")
               && TryTranslate(
                   TinkeringChargeWithGenerationPattern,
                   source,
                   stripped,
                   spans,
                   route,
                   family,
                   "TinkeringChargeWithGeneration",
                   amount => amount + "チャージのあるエネルギーセルを持っておらず、発電能力でも需要を満たせない。",
                   out translated)
               || IsActiveOwner("XRL.World.TinkeringSifrahTokenCharge", "CheckTokenUse", "TinkeringChargeCheckTokenUse")
               && TryTranslate(
                   TinkeringChargePattern,
                   source,
                   stripped,
                   spans,
                   route,
                   family,
                   "TinkeringCharge",
                   amount => amount + "チャージのあるエネルギーセルを持っていない。",
                   out translated)
               || IsActiveOwner("XRL.World.TinkeringSifrahTokenComputePower", "CheckTokenUse", "TinkeringComputePowerCheckTokenUse")
               && TryTranslate(
                   TinkeringComputePowerPattern,
                   source,
                   stripped,
                   spans,
                   route,
                   family,
                   "TinkeringComputePower",
                   amount => "ローカル格子上に利用可能な計算能力が" + amount + "ユニットない。",
                   out translated)
               || IsActiveOwner("XRL.World.TinkeringSifrahTokenBit", "CheckTokenUse", "TinkeringBitCheckTokenUse")
               && TryTranslate(
                   TinkeringBitPattern,
                   source,
                   stripped,
                   spans,
                   route,
                   family,
                   "TinkeringBit",
                   value => value + "を持っていない。",
                   out translated)
               || IsActiveOwner("XRL.World.TinkeringSifrahTokenLiquid", "CheckTokenUse", "TinkeringLiquidCheckTokenUse")
               && TryTranslate(
                   TinkeringLiquidPattern,
                   source,
                   stripped,
                   spans,
                   route,
                   family,
                   "TinkeringLiquid",
                   value => value + "を持っていない。",
                   out translated)
               || IsActiveOwner("XRL.SifrahGame", "MakeMoveForSlot", "SifrahGameMakeMoveForSlot")
               && TryTranslate(
                   SifrahChosenCorrectPattern,
                   source,
                   stripped,
                   spans,
                   route,
                   family,
                   "MakeMoveForSlotChosenCorrect",
                   value => value + "にはすでに正しい選択肢を選んでいる。",
                   out translated)
               || IsActiveOwner("XRL.SifrahGame", "MakeMoveForSlot", "SifrahGameMakeMoveForSlot")
               && TryTranslate(
                   SifrahEliminatedPattern,
                   source,
                   stripped,
                   spans,
                   route,
                   family,
                   "MakeMoveForSlotEliminated",
                   value => value + "はすでに可能性から除外している。",
                   out translated)
               || IsActiveOwner("XRL.SifrahGame", "MakeMoveForSlot", "SifrahGameMakeMoveForSlot")
               && TryTranslate(
                   SifrahDisabledPattern,
                   source,
                   stripped,
                   spans,
                   route,
                   family,
                   "MakeMoveForSlotDisabled",
                   value => value + "を選ぶことはこのターン無効化されている。",
                   out translated);
    }

    internal static bool TryGetPureOwnerBatchPopupCandidateText(string source, out string candidateText)
    {
        if (string.IsNullOrEmpty(source))
        {
            candidateText = source;
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            source = markedText;
        }

        candidateText = source;
        var (stripped, _) = ColorAwareTranslationComposer.Strip(source);
        return Array.Exists(CandidatePatterns, pattern => pattern.IsMatch(stripped));
    }

    private static bool TryTranslate(
        Regex pattern,
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        string detail,
        Func<string, string> translate,
        out string translated)
    {
        _ = family;

        var match = pattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        Group group;
        if (match.Groups["target"].Success)
        {
            group = match.Groups["target"];
        }
        else if (match.Groups["value"].Success)
        {
            group = match.Groups["value"];
        }
        else if (match.Groups["amount"].Success)
        {
            group = match.Groups["amount"];
        }
        else
        {
            translated = source;
            return false;
        }

        var target = ColorAwareTranslationComposer.MarkupAwareRestoreCapture(group.Value, spans, group).Trim();
        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translate(target),
            spans,
            stripped.Length,
            source);
        DynamicTextObservability.RecordTransform(route, "Popup.ProducerText." + Context + "." + detail, source, translated);
        return true;
    }

    internal sealed class OwnerContextState
    {
        internal OwnerContextState(string? previousDeclaringType, string? previousMemberName)
        {
            PreviousDeclaringType = previousDeclaringType;
            PreviousMemberName = previousMemberName;
        }

        internal string? PreviousDeclaringType { get; }

        internal string? PreviousMemberName { get; }
    }

    private sealed class TargetConstructor
    {
        internal TargetConstructor(string typeName, Type[] parameterTypes)
        {
            TypeName = typeName;
            ParameterTypes = parameterTypes;
        }

        internal string TypeName { get; }

        internal Type[] ParameterTypes { get; }
    }

    private sealed class TargetMethod
    {
        internal TargetMethod(string typeName, string methodName, Type[] parameterTypes)
        {
            TypeName = typeName;
            MethodName = methodName;
            ParameterTypes = parameterTypes;
        }

        internal string TypeName { get; }

        internal string MethodName { get; }

        internal Type[] ParameterTypes { get; }
    }

    private static bool IsActiveOwner(string realType, string realMember, string dummyMember)
    {
        return string.Equals(activeDeclaringType, realType, StringComparison.Ordinal)
               && string.Equals(activeMemberName, realMember, StringComparison.Ordinal)
               || string.Equals(activeDeclaringType, "QudJP.Tests.DummyTargets.DummySifrahPureOwnerPopupProducerTarget", StringComparison.Ordinal)
               && string.Equals(activeMemberName, dummyMember, StringComparison.Ordinal);
    }

    private static Type[] SifrahTokenCheckParameterTypes(Type sifrahGameType, Type sifrahSlotType, Type gameObjectType)
    {
        return
        [
            sifrahGameType,
            sifrahSlotType,
            gameObjectType,
        ];
    }
}
