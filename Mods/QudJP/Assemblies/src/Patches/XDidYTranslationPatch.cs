using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

#pragma warning disable S3011

namespace QudJP.Patches;

[HarmonyPatch]
public static class XDidYTranslationPatch
{
    private const string XDidYMethodName = "XDidY";
    private const string XDidYToZMethodName = "XDidYToZ";
    private const string WDidXToYWithZMethodName = "WDidXToYWithZ";

    private static readonly Type? MessagingType =
        GameTypeResolver.FindType("XRL.World.Capabilities.Messaging", "Messaging");
    private static readonly Type? GameObjectType =
        GameTypeResolver.FindType("XRL.World.GameObject", "GameObject");
    private static readonly Type? TheType =
        GameTypeResolver.FindType("XRL.World.The", "The");

    private static readonly MethodInfo? HandleMessageMethod = FindHandleMessageMethod();
    private static readonly MethodInfo? ValidateMethod = AccessTools.Method("XRL.World.GameObject:Validate");
    private static readonly MethodInfo? ConsequentialColorMethod = AccessTools.Method("XRL.Messages.ColorCoding:ConsequentialColor");

    private static Action<object?, string, bool, bool>? messageDispatcherOverride;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var methodName in new[] { XDidYMethodName, XDidYToZMethodName, WDidXToYWithZMethodName })
        {
            var method = AccessTools.Method($"XRL.World.Capabilities.Messaging:{methodName}");
            if (method is null)
            {
                Trace.TraceError("QudJP: Failed to resolve Messaging.{0}. Patch will not apply.", methodName);
                continue;
            }

            yield return method;
        }
    }

    internal static void SetMessageDispatcherForTests(Action<object?, string, bool, bool>? dispatcher)
    {
        messageDispatcherOverride = dispatcher;
    }

    internal static bool PrefixXDidYForTests(
        object? Actor,
        string Verb,
        string? Extra = null,
        string? EndMark = null,
        string? SubjectOverride = null,
        string? Color = null,
        object? ColorAsGoodFor = null,
        object? ColorAsBadFor = null,
        bool UseFullNames = false,
        bool IndefiniteSubject = false,
        object? SubjectPossessedBy = null,
        object? Source = null,
        bool DescribeSubjectDirection = false,
        bool DescribeSubjectDirectionLate = false,
        bool AlwaysVisible = false,
        bool FromDialog = false,
        bool UsePopup = false,
        object? UseVisibilityOf = null)
    {
        return HandleXDidY(
            new object?[]
            {
                Actor,
                Verb,
                Extra,
                EndMark,
                SubjectOverride,
                Color,
                ColorAsGoodFor,
                ColorAsBadFor,
                UseFullNames,
                IndefiniteSubject,
                SubjectPossessedBy,
                Source,
                DescribeSubjectDirection,
                DescribeSubjectDirectionLate,
                AlwaysVisible,
                FromDialog,
                UsePopup,
                UseVisibilityOf,
            });
    }

    internal static bool PrefixXDidYToZForTests(
        object? Actor,
        string Verb,
        string? Preposition = null,
        object? Object = null,
        string? Extra = null,
        string? EndMark = null,
        string? SubjectOverride = null,
        string? Color = null,
        object? ColorAsGoodFor = null,
        object? ColorAsBadFor = null,
        bool UseFullNames = false,
        bool IndefiniteSubject = false,
        bool IndefiniteObject = false,
        bool IndefiniteObjectForOthers = false,
        bool PossessiveObject = false,
        object? SubjectPossessedBy = null,
        object? ObjectPossessedBy = null,
        object? Source = null,
        bool DescribeSubjectDirection = false,
        bool DescribeSubjectDirectionLate = false,
        bool AlwaysVisible = false,
        bool FromDialog = false,
        bool UsePopup = false,
        object? UseVisibilityOf = null)
    {
        return HandleXDidYToZ(
            new object?[]
            {
                Actor,
                Verb,
                Preposition,
                Object,
                Extra,
                EndMark,
                SubjectOverride,
                Color,
                ColorAsGoodFor,
                ColorAsBadFor,
                UseFullNames,
                IndefiniteSubject,
                IndefiniteObject,
                IndefiniteObjectForOthers,
                PossessiveObject,
                SubjectPossessedBy,
                ObjectPossessedBy,
                Source,
                DescribeSubjectDirection,
                DescribeSubjectDirectionLate,
                AlwaysVisible,
                FromDialog,
                UsePopup,
                UseVisibilityOf,
            });
    }

    internal static bool PrefixWDidXToYWithZForTests(
        object? Actor,
        string Verb,
        string? DirectPreposition,
        object? DirectObject,
        string? IndirectPreposition,
        object? IndirectObject,
        string? Extra = null,
        string? EndMark = null,
        string? SubjectOverride = null,
        string? Color = null,
        object? ColorAsGoodFor = null,
        object? ColorAsBadFor = null,
        bool UseFullNames = false,
        bool IndefiniteSubject = false,
        bool IndefiniteDirectObject = false,
        bool IndefiniteIndirectObject = false,
        bool IndefiniteDirectObjectForOthers = false,
        bool IndefiniteIndirectObjectForOthers = false,
        bool PossessiveDirectObject = false,
        bool PossessiveIndirectObject = false,
        object? SubjectPossessedBy = null,
        object? DirectObjectPossessedBy = null,
        object? IndirectObjectPossessedBy = null,
        object? Source = null,
        bool DescribeSubjectDirection = false,
        bool DescribeSubjectDirectionLate = false,
        bool AlwaysVisible = false,
        bool FromDialog = false,
        bool UsePopup = false,
        object? UseVisibilityOf = null)
    {
        return HandleWDidXToYWithZ(
            new object?[]
            {
                Actor,
                Verb,
                DirectPreposition,
                DirectObject,
                IndirectPreposition,
                IndirectObject,
                Extra,
                EndMark,
                SubjectOverride,
                Color,
                ColorAsGoodFor,
                ColorAsBadFor,
                UseFullNames,
                IndefiniteSubject,
                IndefiniteDirectObject,
                IndefiniteIndirectObject,
                IndefiniteDirectObjectForOthers,
                IndefiniteIndirectObjectForOthers,
                PossessiveDirectObject,
                PossessiveIndirectObject,
                SubjectPossessedBy,
                DirectObjectPossessedBy,
                IndirectObjectPossessedBy,
                Source,
                DescribeSubjectDirection,
                DescribeSubjectDirectionLate,
                AlwaysVisible,
                FromDialog,
                UsePopup,
                UseVisibilityOf,
            });
    }

    public static bool Prefix(MethodBase __originalMethod, object[] __args)
    {
        try
        {
            if (__originalMethod is null || __args is null)
            {
                Trace.TraceError("QudJP: XDidYTranslationPatch.Prefix received null originalMethod or args.");
                return true;
            }

            return __originalMethod.Name switch
            {
                XDidYMethodName => HandleXDidY(__args),
                XDidYToZMethodName => HandleXDidYToZ(__args),
                WDidXToYWithZMethodName => HandleWDidXToYWithZ(__args),
                _ => true,
            };
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: XDidYTranslationPatch.Prefix failed: {0}", ex);
            return true;
        }
    }

    private static bool HandleXDidY(object?[] args)
    {
        var actor = GetArg(args, 0);
        var verb = GetStringArg(args, 1);
        var extra = GetStringArg(args, 2);
        var endMark = GetStringArg(args, 3);
        var subjectOverride = GetStringArg(args, 4);
        var color = GetStringArg(args, 5);
        var colorAsGoodFor = GetArg(args, 6);
        var colorAsBadFor = GetArg(args, 7);
        var useFullNames = GetBoolArg(args, 8);
        var indefiniteSubject = GetBoolArg(args, 9);
        var subjectPossessedBy = GetArg(args, 10);
        var source = GetArg(args, 11);
        var describeSubjectDirection = GetBoolArg(args, 12);
        var describeSubjectDirectionLate = GetBoolArg(args, 13);
        var alwaysVisible = GetBoolArg(args, 14);
        var fromDialog = GetBoolArg(args, 15);
        var usePopup = GetBoolArg(args, 16);
        var useVisibilityOf = GetArg(args, 17);

        if (string.IsNullOrWhiteSpace(verb))
        {
            return true;
        }

        var verbText = verb!;
        usePopup = ShouldPromotePopupForXDidY(usePopup, fromDialog, actor, subjectPossessedBy);

        if (string.IsNullOrEmpty(subjectOverride) && IsPlayer(actor))
        {
            if (!MessageFrameTranslator.TryTranslateXDidY("あなた", verbText, extra, endMark, out var playerMessage))
            {
                return true;
            }

            DispatchTranslatedMessage(
                ResolveMessageSource(source, actor),
                playerMessage,
                color,
                colorAsGoodFor,
                colorAsBadFor,
                fromDialog,
                usePopup);
            return false;
        }

        if (!alwaysVisible && !IsVisible(useVisibilityOf ?? source ?? actor))
        {
            return false;
        }

        if (!TryBuildSubjectLabel(
                actor,
                subjectOverride,
                useFullNames,
                indefiniteSubject,
                subjectPossessedBy,
                describeSubjectDirection,
                describeSubjectDirectionLate,
                out var subjectText))
        {
            return true;
        }

        if (!MessageFrameTranslator.TryTranslateXDidY(subjectText, verbText, extra, endMark, out var translated))
        {
            return true;
        }

        DispatchTranslatedMessage(
            ResolveMessageSource(source, actor),
            translated,
            color,
            colorAsGoodFor,
            colorAsBadFor,
            fromDialog,
            usePopup);
        return false;
    }

    private static bool HandleXDidYToZ(object?[] args)
    {
        var actor = GetArg(args, 0);
        var verb = GetStringArg(args, 1);
        var preposition = GetStringArg(args, 2);
        var @object = GetArg(args, 3);
        var extra = GetStringArg(args, 4);
        var endMark = GetStringArg(args, 5);
        var subjectOverride = GetStringArg(args, 6);
        var color = GetStringArg(args, 7);
        var colorAsGoodFor = GetArg(args, 8);
        var colorAsBadFor = GetArg(args, 9);
        var useFullNames = GetBoolArg(args, 10);
        var indefiniteSubject = GetBoolArg(args, 11);
        var indefiniteObject = GetBoolArg(args, 12);
        var indefiniteObjectForOthers = GetBoolArg(args, 13);
        var possessiveObject = GetBoolArg(args, 14);
        var subjectPossessedBy = GetArg(args, 15);
        var objectPossessedBy = GetArg(args, 16);
        var source = GetArg(args, 17);
        var describeSubjectDirection = GetBoolArg(args, 18);
        var describeSubjectDirectionLate = GetBoolArg(args, 19);
        var alwaysVisible = GetBoolArg(args, 20);
        var fromDialog = GetBoolArg(args, 21);
        var usePopup = GetBoolArg(args, 22);
        var useVisibilityOf = GetArg(args, 23);

        if (string.IsNullOrWhiteSpace(verb))
        {
            return true;
        }

        var verbText = verb!;
        if (!IsValidObjectArgument(@object))
        {
            return false;
        }

        usePopup = ShouldPromotePopupForXDidYToZ(usePopup, fromDialog, actor, @object, subjectPossessedBy, objectPossessedBy);

        if (string.IsNullOrEmpty(subjectOverride) && IsPlayer(actor))
        {
            var objectText = BuildObjectLabel(
                @object,
                actor,
                subjectOverride,
                useFullNames,
                indefiniteObject,
                indefiniteObjectForOthers,
                possessiveObject);
            if (string.IsNullOrWhiteSpace(objectText))
            {
                return true;
            }

            if (!MessageFrameTranslator.TryTranslateXDidYToZ("あなた", verbText, preposition, objectText, extra, endMark, out var playerMessage))
            {
                return true;
            }

            DispatchTranslatedMessage(
                ResolveMessageSource(source, actor),
                playerMessage,
                color,
                colorAsGoodFor,
                colorAsBadFor,
                fromDialog,
                usePopup);
            return false;
        }

        if (!alwaysVisible && !IsVisible(useVisibilityOf ?? source ?? actor))
        {
            return false;
        }

        if (!TryBuildSubjectLabel(
                actor,
                subjectOverride,
                useFullNames,
                indefiniteSubject,
                subjectPossessedBy,
                describeSubjectDirection,
                describeSubjectDirectionLate,
                out var subjectText))
        {
            return true;
        }

        var objectLabel = BuildObjectLabel(
            @object,
            actor,
            subjectOverride,
            useFullNames,
            indefiniteObject,
            indefiniteObjectForOthers,
            possessiveObject);
        if (string.IsNullOrWhiteSpace(objectLabel))
        {
            return true;
        }

        if (!MessageFrameTranslator.TryTranslateXDidYToZ(
                subjectText,
                verbText,
                preposition,
                objectLabel,
                extra,
                endMark,
                out var translated))
        {
            return true;
        }

        DispatchTranslatedMessage(
            ResolveMessageSource(source, actor),
            translated,
            color,
            colorAsGoodFor,
            colorAsBadFor,
            fromDialog,
            usePopup);
        return false;
    }

    private static bool HandleWDidXToYWithZ(object?[] args)
    {
        var actor = GetArg(args, 0);
        var verb = GetStringArg(args, 1);
        var directPreposition = GetStringArg(args, 2);
        var directObject = GetArg(args, 3);
        var indirectPreposition = GetStringArg(args, 4);
        var indirectObject = GetArg(args, 5);
        var extra = GetStringArg(args, 6);
        var endMark = GetStringArg(args, 7);
        var subjectOverride = GetStringArg(args, 8);
        var color = GetStringArg(args, 9);
        var colorAsGoodFor = GetArg(args, 10);
        var colorAsBadFor = GetArg(args, 11);
        var useFullNames = GetBoolArg(args, 12);
        var indefiniteSubject = GetBoolArg(args, 13);
        var indefiniteDirectObject = GetBoolArg(args, 14);
        var indefiniteIndirectObject = GetBoolArg(args, 15);
        var indefiniteDirectObjectForOthers = GetBoolArg(args, 16);
        var indefiniteIndirectObjectForOthers = GetBoolArg(args, 17);
        var possessiveDirectObject = GetBoolArg(args, 18);
        var possessiveIndirectObject = GetBoolArg(args, 19);
        var subjectPossessedBy = GetArg(args, 20);
        var directObjectPossessedBy = GetArg(args, 21);
        var indirectObjectPossessedBy = GetArg(args, 22);
        var source = GetArg(args, 23);
        var describeSubjectDirection = GetBoolArg(args, 24);
        var describeSubjectDirectionLate = GetBoolArg(args, 25);
        var alwaysVisible = GetBoolArg(args, 26);
        var fromDialog = GetBoolArg(args, 27);
        var usePopup = GetBoolArg(args, 28);
        var useVisibilityOf = GetArg(args, 29);

        _ = directObjectPossessedBy;
        _ = indirectObjectPossessedBy;

        if (string.IsNullOrWhiteSpace(verb))
        {
            return true;
        }

        var verbText = verb!;
        if (!IsValidObjectArgument(directObject) || !IsValidObjectArgument(indirectObject))
        {
            return false;
        }

        usePopup = ShouldPromotePopupForWDidXToYWithZ(
            usePopup,
            fromDialog,
            actor,
            directObject,
            indirectObject,
            subjectPossessedBy,
            directObjectPossessedBy,
            indirectObjectPossessedBy);

        if (string.IsNullOrEmpty(subjectOverride) && IsPlayer(actor))
        {
            var directObjectText = BuildObjectLabel(
                directObject,
                actor,
                subjectOverride,
                useFullNames,
                indefiniteDirectObject,
                indefiniteDirectObjectForOthers,
                possessiveDirectObject);
            var indirectObjectText = BuildObjectLabel(
                indirectObject,
                actor,
                subjectOverride,
                useFullNames,
                indefiniteIndirectObject,
                indefiniteIndirectObjectForOthers,
                possessiveIndirectObject);

            if (string.IsNullOrWhiteSpace(directObjectText) || string.IsNullOrWhiteSpace(indirectObjectText))
            {
                return true;
            }

            if (!MessageFrameTranslator.TryTranslateWDidXToYWithZ(
                    "あなた",
                    verbText,
                    directPreposition,
                    directObjectText,
                    indirectPreposition,
                    indirectObjectText,
                    extra,
                    endMark,
                    out var playerMessage))
            {
                return true;
            }

            DispatchTranslatedMessage(
                ResolveMessageSource(source, actor),
                playerMessage,
                color,
                colorAsGoodFor,
                colorAsBadFor,
                fromDialog,
                usePopup);
            return false;
        }

        if (!alwaysVisible && !IsVisible(useVisibilityOf ?? source ?? actor))
        {
            return false;
        }

        if (!TryBuildSubjectLabel(
                actor,
                subjectOverride,
                useFullNames,
                indefiniteSubject,
                subjectPossessedBy,
                describeSubjectDirection,
                describeSubjectDirectionLate,
                out var subjectText))
        {
            return true;
        }

        var directLabel = BuildObjectLabel(
            directObject,
            actor,
            subjectOverride,
            useFullNames,
            indefiniteDirectObject,
            indefiniteDirectObjectForOthers,
            possessiveDirectObject);
        var indirectLabel = BuildObjectLabel(
            indirectObject,
            actor,
            subjectOverride,
            useFullNames,
            indefiniteIndirectObject,
            indefiniteIndirectObjectForOthers,
            possessiveIndirectObject);
        if (string.IsNullOrWhiteSpace(directLabel) || string.IsNullOrWhiteSpace(indirectLabel))
        {
            return true;
        }

        if (!MessageFrameTranslator.TryTranslateWDidXToYWithZ(
                subjectText,
                verbText,
                directPreposition,
                directLabel,
                indirectPreposition,
                indirectLabel,
                extra,
                endMark,
                out var translated))
        {
            return true;
        }

        DispatchTranslatedMessage(
            ResolveMessageSource(source, actor),
            translated,
            color,
            colorAsGoodFor,
            colorAsBadFor,
            fromDialog,
            usePopup);
        return false;
    }

    private static bool ShouldPromotePopupForXDidY(bool usePopup, bool fromDialog, object? actor, object? subjectPossessedBy)
    {
        if (usePopup || !fromDialog)
        {
            return usePopup;
        }

        return IsPlayer(subjectPossessedBy) || HolderIsPlayer(actor);
    }

    private static bool ShouldPromotePopupForXDidYToZ(
        bool usePopup,
        bool fromDialog,
        object? actor,
        object? @object,
        object? subjectPossessedBy,
        object? objectPossessedBy)
    {
        if (usePopup || !fromDialog)
        {
            return usePopup;
        }

        return IsPlayer(@object)
            || IsPlayer(subjectPossessedBy)
            || HolderIsPlayer(actor)
            || IsPlayer(objectPossessedBy)
            || HolderIsPlayer(@object);
    }

    private static bool ShouldPromotePopupForWDidXToYWithZ(
        bool usePopup,
        bool fromDialog,
        object? actor,
        object? directObject,
        object? indirectObject,
        object? subjectPossessedBy,
        object? directObjectPossessedBy,
        object? indirectObjectPossessedBy)
    {
        if (usePopup || !fromDialog)
        {
            return usePopup;
        }

        return IsPlayer(directObject)
            || IsPlayer(indirectObject)
            || IsPlayer(subjectPossessedBy)
            || HolderIsPlayer(actor)
            || IsPlayer(directObjectPossessedBy)
            || HolderIsPlayer(directObject)
            || IsPlayer(indirectObjectPossessedBy)
            || HolderIsPlayer(indirectObject);
    }

    private static bool TryBuildSubjectLabel(
        object? actor,
        string? subjectOverride,
        bool useFullNames,
        bool indefiniteSubject,
        object? subjectPossessedBy,
        bool describeSubjectDirection,
        bool describeSubjectDirectionLate,
        out string subjectText)
    {
        var ownerPrefix = GetOwnerPrefix(subjectPossessedBy ?? GetHolder(actor), useFullNames, indefiniteSubject);
        var baseLabel = TranslateSubjectBase(actor, subjectOverride, useFullNames, indefiniteSubject);
        if (string.IsNullOrWhiteSpace(baseLabel))
        {
            subjectText = string.Empty;
            return false;
        }

        subjectText = ownerPrefix + baseLabel;
        if (describeSubjectDirection && !describeSubjectDirectionLate)
        {
            subjectText += GetDirectionSuffix(subjectPossessedBy ?? actor);
        }

        return true;
    }

    private static string TranslateSubjectBase(object? actor, string? subjectOverride, bool useFullNames, bool indefiniteSubject)
    {
        if (!string.IsNullOrWhiteSpace(subjectOverride))
        {
            return TranslateDisplayFragment(subjectOverride!);
        }

        if (IsPlayer(actor))
        {
            return "あなた";
        }

        return GetEntityDisplayName(actor, capitalize: true, useFullNames, indefiniteSubject);
    }

    private static string GetOwnerPrefix(object? owner, bool useFullNames, bool indefiniteSubject)
    {
        if (owner is null)
        {
            return string.Empty;
        }

        if (IsPlayer(owner))
        {
            return "あなたの";
        }

        var label = GetEntityDisplayName(owner, capitalize: false, useFullNames, indefiniteSubject);
        return string.IsNullOrWhiteSpace(label)
            ? string.Empty
            : MakePossessiveLabel(label);
    }

    private static string GetDirectionSuffix(object? target)
    {
        if (target is null)
        {
            return string.Empty;
        }

        var player = GetPlayerObject();
        if (player is null)
        {
            return string.Empty;
        }

        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var method = player.GetType().GetMethod("DescribeDirectionToward", Flags);
        if (method is null)
        {
            return string.Empty;
        }

        if (method.Invoke(player, new[] { target }) is not string direction || string.IsNullOrWhiteSpace(direction))
        {
            return string.Empty;
        }

        return TranslateDirectionPhrase(direction);
    }

    private static string BuildObjectLabel(
        object? value,
        object? actor,
        string? subjectOverride,
        bool useFullNames,
        bool indefiniteObject,
        bool indefiniteObjectForOthers,
        bool possessive)
    {
        if (ReferenceEquals(value, actor) && string.IsNullOrEmpty(subjectOverride))
        {
            return possessive ? "自分の" : "自分";
        }

        if (IsPlayer(value))
        {
            return possessive ? "あなたの" : "あなた";
        }

        var label = GetEntityDisplayName(value, capitalize: false, useFullNames, indefiniteObject || indefiniteObjectForOthers);
        if (string.IsNullOrWhiteSpace(label))
        {
            return string.Empty;
        }

        return possessive ? MakePossessiveLabel(label) : label;
    }

    private static string GetEntityDisplayName(object? value, bool capitalize, bool useFullNames, bool indefiniteArticle)
    {
        if (value is null)
        {
            return string.Empty;
        }

        if (value is string text)
        {
            return TranslateDisplayFragment(text);
        }

        if (TryInvokeDisplayNameMethod(value, capitalize, useFullNames, indefiniteArticle, out var displayName))
        {
            return TranslateDisplayFragment(displayName);
        }

        var fallbackText = value.ToString();
        if (fallbackText is null)
        {
            Trace.TraceWarning("QudJP: XDidYTranslationPatch could not derive a display name from '{0}'.", value.GetType().FullName);
            return string.Empty;
        }

        return TranslateDisplayFragment(fallbackText);
    }

    private static bool TryInvokeDisplayNameMethod(
        object target,
        bool capitalize,
        bool useFullNames,
        bool indefiniteArticle,
        out string displayName)
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var methodName = capitalize ? "One" : "one";
        var methods = target.GetType().GetMethods(Flags);
        for (var index = 0; index < methods.Length; index++)
        {
            var method = methods[index];
            if (!string.Equals(method.Name, methodName, StringComparison.Ordinal)
                || method.ReturnType != typeof(string))
            {
                continue;
            }

            var parameters = method.GetParameters();
            if (parameters.Length != 13)
            {
                continue;
            }

            var args = new object?[]
            {
                int.MaxValue,
                null,
                null,
                false,
                false,
                false,
                false,
                false,
                !useFullNames,
                !useFullNames,
                false,
                indefiniteArticle,
                null,
            };

            displayName = method.Invoke(target, args) as string ?? string.Empty;
            return !string.IsNullOrWhiteSpace(displayName);
        }

        displayName = string.Empty;
        return false;
    }

    private static string TranslateDisplayFragment(string text)
    {
        return string.IsNullOrWhiteSpace(text)
            ? string.Empty
            : GetDisplayNameRouteTranslator.TranslatePreservingColors(text, nameof(GetDisplayNamePatch));
    }

    private static string MakePossessiveLabel(string label)
    {
        return label.EndsWith("の", StringComparison.Ordinal) ? label : label + "の";
    }

    private static string TranslateDirectionPhrase(string direction)
    {
        if (Translator.TryGetTranslation(direction, out var translated)
            && !string.Equals(translated, direction, StringComparison.Ordinal))
        {
            return translated;
        }

        return direction switch
        {
            "to the north" => "の北側",
            "to the south" => "の南側",
            "to the east" => "の東側",
            "to the west" => "の西側",
            "to the northeast" => "の北東側",
            "to the northwest" => "の北西側",
            "to the southeast" => "の南東側",
            "to the southwest" => "の南西側",
            _ => direction,
        };
    }

    private static void DispatchTranslatedMessage(
        object? source,
        string translatedMessage,
        string? color,
        object? colorAsGoodFor,
        object? colorAsBadFor,
        bool fromDialog,
        bool usePopup)
    {
        var marked = MessageFrameTranslator.MarkDirectTranslation(translatedMessage);
        var colored = ApplyMessageColor(marked, color, colorAsGoodFor, colorAsBadFor);

        if (messageDispatcherOverride is not null)
        {
            messageDispatcherOverride(source, colored, fromDialog, usePopup);
            return;
        }

        if (HandleMessageMethod is null)
        {
            Trace.TraceError("QudJP: Failed to resolve Messaging.HandleMessage(string overload).");
            return;
        }

        HandleMessageMethod.Invoke(
            null,
            new[]
            {
                ResolveMessageSource(source, null),
                (object?)colored,
                ' ',
                fromDialog,
                usePopup,
                null,
                null,
            });
    }

    private static string ApplyMessageColor(string message, string? color, object? colorAsGoodFor, object? colorAsBadFor)
    {
        var colorCode = color;
        if (string.IsNullOrEmpty(colorCode))
        {
            colorCode = ResolveConsequentialColor(colorAsGoodFor, colorAsBadFor);
        }

        return string.IsNullOrEmpty(colorCode)
            ? message
            : "{{" + colorCode + "|" + message + "}}";
    }

    private static string? ResolveConsequentialColor(object? colorAsGoodFor, object? colorAsBadFor)
    {
        return ConsequentialColorMethod?.Invoke(null, new[] { colorAsGoodFor, colorAsBadFor }) as string;
    }

    private static object? ResolveMessageSource(object? source, object? actor)
    {
        return source ?? actor ?? GetPlayerObject();
    }

    private static object? GetPlayerObject()
    {
        if (TheType is null)
        {
            return null;
        }

        const BindingFlags Flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        var property = TheType.GetProperty("Player", Flags);
        if (property is not null)
        {
            return property.GetValue(null);
        }

        var field = TheType.GetField("Player", Flags);
        return field?.GetValue(null);
    }

    private static bool IsValidObjectArgument(object? value)
    {
        if (value is null)
        {
            return false;
        }

        if (!IsGameObjectInstance(value) || ValidateMethod is null)
        {
            return true;
        }

        var invokeArgs = new[] { value };
        return ValidateMethod.Invoke(null, invokeArgs) is bool result && result;
    }

    private static bool IsPlayer(object? value)
    {
        return InvokeBooleanMember(value, "IsPlayer");
    }

    private static bool IsVisible(object? value)
    {
        if (value is string)
        {
            return true;
        }

        return InvokeBooleanMember(value, "IsVisible");
    }

    private static bool HolderIsPlayer(object? value)
    {
        return IsPlayer(GetHolder(value));
    }

    private static object? GetHolder(object? value)
    {
        if (value is null)
        {
            return null;
        }

        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var property = value.GetType().GetProperty("Holder", Flags);
        return property?.GetValue(value);
    }

    private static bool InvokeBooleanMember(object? value, string memberName)
    {
        if (value is null)
        {
            return false;
        }

        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var method = value.GetType().GetMethod(memberName, Flags, binder: null, Type.EmptyTypes, modifiers: null);
        if (method is not null && method.ReturnType == typeof(bool))
        {
            return (bool)method.Invoke(value, parameters: null)!;
        }

        var property = value.GetType().GetProperty(memberName, Flags);
        if (property is not null && property.PropertyType == typeof(bool))
        {
            return (bool)property.GetValue(value)!;
        }

        return false;
    }

    private static bool IsGameObjectInstance(object value)
    {
        return GameObjectType is not null && GameObjectType.IsInstanceOfType(value);
    }

    private static MethodInfo? FindHandleMessageMethod()
    {
        if (MessagingType is null)
        {
            return null;
        }

        const BindingFlags Flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        var methods = MessagingType.GetMethods(Flags);
        for (var index = 0; index < methods.Length; index++)
        {
            var method = methods[index];
            if (!string.Equals(method.Name, "HandleMessage", StringComparison.Ordinal))
            {
                continue;
            }

            var parameters = method.GetParameters();
            if (parameters.Length == 7 && parameters[1].ParameterType == typeof(string))
            {
                return method;
            }
        }

        return null;
    }

    private static object? GetArg(object?[] args, int index)
    {
        return index >= 0 && index < args.Length ? args[index] : null;
    }

    private static string? GetStringArg(object?[] args, int index)
    {
        return GetArg(args, index) as string;
    }

    private static bool GetBoolArg(object?[] args, int index)
    {
        return GetArg(args, index) is bool value && value;
    }
}

#pragma warning restore S3011
