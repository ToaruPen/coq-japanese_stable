using System;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class AbilityManagerLineTranslationPatch
{
    private const string Context = nameof(AbilityManagerLineTranslationPatch);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        return FrameworkDataElementSetDataTargetResolver.Resolve(
            Context,
            "Qud.UI.AbilityManagerLine",
            "AbilityManagerLine");
    }

    public static void Prefix(object? __instance)
    {
        try
        {
            if (__instance is null)
            {
                return;
            }

            TranslateStaticMenuOptions(__instance.GetType());
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: AbilityManagerLineTranslationPatch.Prefix failed: {0}", ex);
        }
    }

    public static void Postfix(object? __instance, object? data)
    {
        try
        {
            if (__instance is null || data is null)
            {
                return;
            }

            if (GetMemberValue(data, "category") is not null)
            {
                ApplyCategoryRow(__instance, data);
                return;
            }

            var ability = GetMemberValue(data, "ability");
            if (ability is null)
            {
                return;
            }

            LogAbilityRowProbe(data, ability);
            ApplyAbilityRow(__instance, data, ability);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: AbilityManagerLineTranslationPatch.Postfix failed: {0}", ex);
        }
    }

    private static void ApplyCategoryRow(object instance, object data)
    {
        SetContextData(instance, data);
        var collapsed = GetBoolMemberValue(data, "collapsed");
        var category = GetRequiredStringMemberValue(data, "category");
        var source = "[" + (collapsed ? "+" : "-") + "] " + category;
        var route = ObservabilityHelpers.ComposeContext(Context, "field=text");
        var translated = "[" + (collapsed ? "+" : "-") + "] " + TranslateVisibleText(category, route, "AbilityManagerLine.CategoryText");

        OwnerTextSetter.SetTranslatedText(
            GetMemberValue(instance, "text"),
            source,
            translated,
            Context,
            typeof(AbilityManagerLineTranslationPatch));
    }

    private static void ApplyAbilityRow(object instance, object data, object ability)
    {
        SetContextData(instance, data);
        var source = BuildAbilityText(data, ability, translated: false);
        var translated = BuildAbilityText(data, ability, translated: true);

        OwnerTextSetter.SetTranslatedText(
            GetMemberValue(instance, "text"),
            source,
            translated,
            Context,
            typeof(AbilityManagerLineTranslationPatch));
    }

    private static string BuildAbilityText(object data, object ability, bool translated)
    {
        var builder = new StringBuilder();
        var displayNameSource = GetRequiredStringMemberValue(ability, "DisplayName");
        var route = ObservabilityHelpers.ComposeContext(Context, "field=text");
        var displayName = translated
            ? TranslateVisibleText(displayNameSource, route, "AbilityManagerLine.AbilityText")
            : displayNameSource;

        var quickKey = GetMemberValue(data, "quickKey")?.ToString();
        if (quickKey is null)
        {
            quickKey = string.Empty;
        }
        var hotkeyDescription = ResolveHotkeyDescription(data, ability);
        var enabled = GetBoolMemberValue(ability, "Enabled");
        var isAttack = GetBoolMemberValue(ability, "IsAttack");
        var isRealityDistortionBased = GetBoolMemberValue(ability, "IsRealityDistortionBased");
        var realityIsWeak = GetBoolMemberValue(data, "realityIsWeak");
        var cooldown = GetIntMemberValue(ability, "Cooldown");
        var cooldownRounds = GetIntMemberValue(ability, "CooldownRounds");
        var toggleable = GetBoolMemberValue(ability, "Toggleable");
        var toggleState = GetBoolMemberValue(ability, "ToggleState");

        var attackLabel = translated ? TranslateFragment("attack") : "attack";
        var disabledLabel = translated ? TranslateFragment("disabled") : "disabled";
        var tetheredLabel = translated ? TranslateFragment("astrally tethered") : "astrally tethered";
        var cooldownLabel = translated ? TranslateFragment("turn cooldown") : "turn cooldown";
        var toggledOnLabel = translated ? TranslateFragment("Toggled on") : "Toggled on";
        var toggledOffLabel = translated ? TranslateFragment("Toggled off") : "Toggled off";

        if (!enabled)
        {
            builder.Append("{{K|");
            builder.Append(quickKey);
            builder.Append(") ");
            builder.Append(displayName);
            if (isAttack)
            {
                builder.Append(" [");
                builder.Append(attackLabel);
                builder.Append(']');
            }

            builder.Append(" [");
            builder.Append(disabledLabel);
            builder.Append("]}}");
        }
        else if (cooldown <= 0)
        {
            if (isRealityDistortionBased && !realityIsWeak)
            {
                builder.Append("{{K|");
                builder.Append(quickKey);
                builder.Append(") ");
                builder.Append(displayName);
                if (isAttack)
                {
                    builder.Append(" [");
                    builder.Append(attackLabel);
                    builder.Append(']');
                }

                builder.Append(" [");
                builder.Append(tetheredLabel);
                builder.Append("]}}");
            }
            else
            {
                builder.Append(quickKey);
                builder.Append(") ");
                builder.Append(displayName);
                if (isAttack)
                {
                    builder.Append(" [{{W|");
                    builder.Append(attackLabel);
                    builder.Append("}}]");
                }
            }
        }
        else if (isRealityDistortionBased && !realityIsWeak)
        {
            builder.Append("{{K|");
            builder.Append(quickKey);
            builder.Append("}}) ");
            builder.Append(displayName);
            builder.Append(" [{{C|");
            builder.Append(cooldownRounds);
            builder.Append("}} ");
            builder.Append(cooldownLabel);
            builder.Append(", ");
            builder.Append(tetheredLabel);
            builder.Append(']');
        }
        else
        {
            builder.Append("{{K|");
            builder.Append(quickKey);
            builder.Append("}}) ");
            builder.Append(displayName);
            builder.Append(" [{{C|");
            builder.Append(cooldownRounds);
            builder.Append("}} ");
            builder.Append(cooldownLabel);
            builder.Append(']');
        }

        if (toggleable)
        {
            builder.Append(" {{K|[{{");
            builder.Append(toggleState ? "g" : "y");
            builder.Append('|');
            builder.Append(toggleState ? toggledOnLabel : toggledOffLabel);
            builder.Append("}}]}}");
        }

        if (!string.IsNullOrEmpty(hotkeyDescription))
        {
            builder.Append(" {{Y|<{{w|");
            builder.Append(hotkeyDescription);
            builder.Append("}}>}}");
        }

        return builder.ToString();
    }

    private static string TranslateFragment(string source)
    {
        var translated = Translator.Translate(source);
        return string.Equals(translated, source, StringComparison.Ordinal) ? source : translated;
    }

    private static string? ResolveHotkeyDescription(object data, object ability)
    {
        var hotkeyDescription = GetStringMemberValue(data, "hotkeyDescription");
        var displayForHotkey = GetStringMemberValue(ability, "DisplayForHotkey");
        var command = GetStringMemberValue(ability, "Command");
        string? commandHotkey = null;
        string? slotHotkey = null;
        string? resolved = null;
        var source = "none";

        if (IsUsableHotkeyDescription(hotkeyDescription))
        {
            resolved = hotkeyDescription;
            source = "line";
        }
        else if (IsUsableHotkeyDescription(displayForHotkey))
        {
            resolved = displayForHotkey;
            source = "display";
        }
        else
        {
            if (!string.IsNullOrEmpty(command))
            {
                commandHotkey = GetCommandInputDescription(command!);
                if (IsUsableHotkeyDescription(commandHotkey))
                {
                    resolved = commandHotkey;
                    source = "command";
                }
            }

            if (resolved is null)
            {
                slotHotkey = ResolveAbilityBarSlotHotkey(data, ability, command);
                if (IsUsableHotkeyDescription(slotHotkey))
                {
                    resolved = slotHotkey;
                    source = "slot";
                }
            }
        }

        LogHotkeyProbe(data, ability, hotkeyDescription, displayForHotkey, command, commandHotkey, slotHotkey, resolved, source);
        return resolved;
    }

    private static string? GetCommandInputDescription(string command)
    {
        var controlManagerType = GameTypeResolver.FindType("ControlManager", "ControlManager");
        var method = controlManagerType is null ? null : AccessTools.Method(controlManagerType, "getCommandInputDescription");
        if (method is null)
        {
            return null;
        }

        var parameters = method.GetParameters();
        if (parameters.Length == 0)
        {
            return null;
        }

        var args = new object?[parameters.Length];
        args[0] = command;
        for (var index = 1; index < parameters.Length; index++)
        {
            args[index] = BuildDefaultArgument(parameters[index]);
        }

        try
        {
            return method.Invoke(null, args) is string value && IsUsableHotkeyDescription(value)
                ? value
                : null;
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("QudJP: {0} failed to resolve command input description for '{1}': {2}", Context, command, ex.Message);
            return null;
        }
    }

    private static string? ResolveAbilityBarSlotHotkey(object data, object ability, string? command)
    {
        var slotNumber = ResolveOrderedAbilitySlotNumber(ability, command);
        if (slotNumber is null)
        {
            slotNumber = ResolveQuickKeySlotNumber(data);
        }

        if (slotNumber is null)
        {
            return null;
        }

        var slotHotkey = GetCommandInputDescription("CmdAbility" + slotNumber.Value);
        return IsUsableHotkeyDescription(slotHotkey)
            ? slotHotkey
            : GetFallbackAbilityBarSlotHotkey(slotNumber.Value);
    }

    private static string? GetFallbackAbilityBarSlotHotkey(int slotNumber)
    {
        if (slotNumber is < 1 or > 10)
        {
            return null;
        }

        return slotNumber == 10
            ? "0"
            : slotNumber.ToString(CultureInfo.InvariantCulture);
    }

    private static int? ResolveOrderedAbilitySlotNumber(object ability, string? command)
    {
        try
        {
            var theType = AccessTools.TypeByName("XRL.The");
            if (theType is null)
            {
                theType = AccessTools.TypeByName("The");
            }

            var player = theType is null ? null : AccessTools.Property(theType, "Player")?.GetValue(null);
            var activatedAbilities = player is null ? null : GetMemberValue(player, "ActivatedAbilities");
            var orderedAbilities = activatedAbilities is null
                ? null
                : AccessTools.Method(activatedAbilities.GetType(), "GetAbilityListOrderedByPreference", Type.EmptyTypes)
                    ?.Invoke(activatedAbilities, Array.Empty<object>());
            if (orderedAbilities is not IEnumerable enumerable)
            {
                return null;
            }

            var slotNumber = 1;
            foreach (var entry in enumerable)
            {
                if (entry is not null && AbilityMatches(entry, ability, command))
                {
                    return slotNumber is >= 1 and <= 10 ? slotNumber : null;
                }

                slotNumber++;
            }
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("QudJP: {0} failed to resolve ordered ability slot: {1}", Context, ex.Message);
        }

        return null;
    }

    private static bool AbilityMatches(object candidate, object ability, string? command)
    {
        if (ReferenceEquals(candidate, ability))
        {
            return true;
        }

        if (string.IsNullOrEmpty(command))
        {
            return false;
        }

        return string.Equals(GetStringMemberValue(candidate, "Command"), command, StringComparison.Ordinal);
    }

    private static void LogAbilityRowProbe(object data, object ability)
    {
        RuntimeDiagnostics.LogVerboseProbe(() =>
            "[QudJP] AbilityManagerLineRowProbe/v1: dataType="
            + ProbeValue(data.GetType().FullName)
            + " abilityType="
            + ProbeValue(ability.GetType().FullName)
            + " display="
            + ProbeValue(GetStringMemberValue(ability, "DisplayName"))
            + " quickKey="
            + ProbeValue(GetMemberValue(data, "quickKey")?.ToString())
            + " command="
            + ProbeValue(GetStringMemberValue(ability, "Command"))
            + " lineHotkey="
            + ProbeValue(GetStringMemberValue(data, "hotkeyDescription"))
            + " displayForHotkey="
            + ProbeValue(GetStringMemberValue(ability, "DisplayForHotkey")));
    }

    private static int? ResolveQuickKeySlotNumber(object data)
    {
        var quickKeyText = GetMemberValue(data, "quickKey")?.ToString();
        if (string.IsNullOrEmpty(quickKeyText) || quickKeyText!.Length != 1)
        {
            return null;
        }

        var quickKey = char.ToLowerInvariant(quickKeyText[0]);
        return quickKey is >= 'a' and <= 'j' ? quickKey - 'a' + 1 : null;
    }

    private static void LogHotkeyProbe(
        object data,
        object ability,
        string? lineHotkey,
        string? displayForHotkey,
        string? command,
        string? commandHotkey,
        string? slotHotkey,
        string? resolved,
        string source)
    {
        RuntimeDiagnostics.LogVerboseProbe(() =>
            "[QudJP] AbilityManagerLineHotkeyProbe/v1: display="
            + ProbeValue(GetStringMemberValue(ability, "DisplayName"))
            + " quickKey="
            + ProbeValue(GetMemberValue(data, "quickKey")?.ToString())
            + " command="
            + ProbeValue(command)
            + " lineHotkey="
            + ProbeValue(lineHotkey)
            + " displayForHotkey="
            + ProbeValue(displayForHotkey)
            + " commandHotkey="
            + ProbeValue(commandHotkey)
            + " slotHotkey="
            + ProbeValue(slotHotkey)
            + " resolved="
            + ProbeValue(resolved)
            + " source="
            + ProbeValue(source));
    }

    private static string ProbeValue(string? value)
    {
        if (value is null)
        {
            return "'<null>'";
        }

        return "'" + value.Replace("\\", "\\\\").Replace("\r", "\\r").Replace("\n", "\\n").Replace("'", "\\'") + "'";
    }

    private static object? BuildDefaultArgument(ParameterInfo parameter)
    {
        if (parameter.HasDefaultValue)
        {
            var defaultValue = parameter.DefaultValue;
            if (parameter.ParameterType.IsEnum && defaultValue is int enumValue)
            {
                return Enum.ToObject(parameter.ParameterType, enumValue);
            }

            return defaultValue;
        }

        if (parameter.ParameterType == typeof(bool))
        {
            return false;
        }

        if (parameter.ParameterType.IsEnum)
        {
            return Enum.ToObject(parameter.ParameterType, 0);
        }

        return parameter.ParameterType.IsValueType ? Activator.CreateInstance(parameter.ParameterType) : null;
    }

    private static bool IsUsableHotkeyDescription(string? hotkeyDescription)
    {
        if (string.IsNullOrEmpty(hotkeyDescription))
        {
            return false;
        }

        return !string.Equals(hotkeyDescription, "NEEDUICONTEXT", StringComparison.Ordinal)
            && hotkeyDescription!.IndexOf("<nothing bound", StringComparison.Ordinal) < 0;
    }

    private static void TranslateStaticMenuOptions(Type instanceType)
    {
        TranslateMenuOption(GetStaticMemberValue(instanceType, "MOVE_DOWN"), "MOVE_DOWN");
        TranslateMenuOption(GetStaticMemberValue(instanceType, "MOVE_UP"), "MOVE_UP");
        TranslateMenuOption(GetStaticMemberValue(instanceType, "BIND_KEY"), "BIND_KEY");
        TranslateMenuOption(GetStaticMemberValue(instanceType, "UNBIND_KEY"), "UNBIND_KEY");
    }

    private static void TranslateMenuOption(object? menuOption, string routeSuffix)
    {
        if (menuOption is null)
        {
            return;
        }

        var current = GetStringMemberValue(menuOption, "Description");
        if (string.IsNullOrEmpty(current))
        {
            return;
        }

        var route = ObservabilityHelpers.ComposeContext(Context, "field=" + routeSuffix);
        var translated = TranslateVisibleText(current!, route, "AbilityManagerLine.MenuOption");
        if (!string.Equals(translated, current, StringComparison.Ordinal))
        {
            SetMemberValue(menuOption, "Description", translated);
        }
    }

    private static string TranslateVisibleText(string source, string route, string family)
    {
        if (string.Equals(family, "AbilityManagerLine.AbilityText", StringComparison.Ordinal))
        {
            var activatedAbilityName = ActivatedAbilityNameTranslator.TranslatePreservingColors(source, route, family);
            if (!string.Equals(activatedAbilityName, source, StringComparison.Ordinal))
            {
                return activatedAbilityName;
            }
        }

        return UiBindingTranslationHelpers.TranslateVisibleText(source, route, family);
    }

    private static string GetRequiredStringMemberValue(object instance, string memberName)
    {
        var value = GetStringMemberValue(instance, memberName);
        if (value is not null)
        {
            return value;
        }

        Trace.TraceWarning("QudJP: {0} missing string member '{1}' on '{2}'. Falling back to empty string.", Context, memberName, instance.GetType().FullName);
        return string.Empty;
    }

    private static void SetContextData(object instance, object data)
    {
        var context = GetMemberValue(instance, "context");
        if (context is not null)
        {
            SetMemberValue(context, "data", data);
        }
    }

    private static object? GetStaticMemberValue(Type type, string memberName)
    {
        var property = AccessTools.Property(type, memberName);
        if (property is not null && property.CanRead)
        {
            return property.GetValue(null);
        }

        var field = AccessTools.Field(type, memberName);
        return field?.GetValue(null);
    }

    private static object? GetMemberValue(object instance, string memberName) => UiBindingTranslationHelpers.GetMemberValue(instance, memberName);

    private static string? GetStringMemberValue(object instance, string memberName) => UiBindingTranslationHelpers.GetStringMemberValue(instance, memberName);

    private static bool GetBoolMemberValue(object instance, string memberName)
    {
        return GetMemberValue(instance, memberName) as bool? ?? false;
    }

    private static int GetIntMemberValue(object instance, string memberName)
    {
        var value = GetMemberValue(instance, memberName);
        return value is int intValue ? intValue : 0;
    }

    private static void SetMemberValue(object instance, string memberName, object? value) => UiBindingTranslationHelpers.SetMemberValue(instance, memberName, value);
}
