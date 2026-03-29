using System;
using System.Collections;
using System.Diagnostics;
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
        var targetType = GameTypeResolver.FindType("Qud.UI.AbilityManagerLine", "AbilityManagerLine");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: AbilityManagerLineTranslationPatch target type not found.");
            return null;
        }

        var frameworkDataElementType = GameTypeResolver.FindType("XRL.UI.Framework.FrameworkDataElement", "FrameworkDataElement");
        var method = frameworkDataElementType is null
            ? null
            : AccessTools.Method(targetType, "setData", new[] { frameworkDataElementType });
        if (method is null)
        {
            Trace.TraceError("QudJP: AbilityManagerLineTranslationPatch.setData(FrameworkDataElement) not found.");
        }

        return method;
    }

    public static bool Prefix(object? __instance, object? data)
    {
        try
        {
            if (__instance is null || data is null)
            {
                return true;
            }

            TranslateStaticMenuOptions(__instance.GetType());

            if (GetMemberValue(data, "category") is not null)
            {
                ApplyCategoryRow(__instance, data);
                return false;
            }

            var ability = GetMemberValue(data, "ability");
            if (ability is null)
            {
                return true;
            }

            ApplyAbilityRow(__instance, data, ability);
            return false;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: AbilityManagerLineTranslationPatch.Prefix failed: {0}", ex);
            return true;
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
        var icon = GetMemberValue(instance, "icon");
        if (icon is not null)
        {
            TrySetActive(GetMemberValue(icon, "gameObject"), active: false);
        }
    }

    private static void ApplyAbilityRow(object instance, object data, object ability)
    {
        SetContextData(instance, data);
        var icon = GetMemberValue(instance, "icon");
        if (icon is not null)
        {
            TrySetActive(GetMemberValue(icon, "gameObject"), active: true);
        }

        var uiTile = AccessTools.Method(ability.GetType(), "GetUITile")?.Invoke(ability, null);
        if (icon is not null && uiTile is not null)
        {
            _ = AccessTools.Method(icon.GetType(), "FromRenderable")?.Invoke(icon, new[] { uiTile });
        }

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

        var quickKey = GetMemberValue(data, "quickKey")?.ToString() ?? string.Empty;
        var hotkeyDescription = GetStringMemberValue(data, "hotkeyDescription");
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

    private static string TranslateVisibleText(string source, string route, string family) => UiBindingTranslationHelpers.TranslateVisibleText(source, route, family);

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

    private static void TrySetActive(object? target, bool active)
    {
        if (target is null)
        {
            return;
        }

        if (AccessTools.Method(target.GetType(), "SetActive", new[] { typeof(bool) }) is MethodInfo method)
        {
            _ = method.Invoke(target, new object[] { active });
            return;
        }

        SetMemberValue(target, "activeSelf", active);
        SetMemberValue(target, "Active", active);
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
