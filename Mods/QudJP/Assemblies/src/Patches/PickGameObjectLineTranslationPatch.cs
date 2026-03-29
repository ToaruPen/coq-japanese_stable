using System;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class PickGameObjectLineTranslationPatch
{
    private const string Context = nameof(PickGameObjectLineTranslationPatch);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("Qud.UI.PickGameObjectLine", "PickGameObjectLine");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: PickGameObjectLineTranslationPatch target type not found.");
            return null;
        }

        var frameworkDataElementType = GameTypeResolver.FindType("XRL.UI.Framework.FrameworkDataElement", "FrameworkDataElement");
        var method = frameworkDataElementType is null
            ? null
            : AccessTools.Method(targetType, "setData", new[] { frameworkDataElementType });
        if (method is null)
        {
            Trace.TraceError("QudJP: PickGameObjectLineTranslationPatch.setData(FrameworkDataElement) not found.");
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

            if (GetMemberValue(data, "style")?.ToString() == "Interact")
            {
                OwnerTextSetter.SetTranslatedText(
                    GetMemberValue(__instance, "check"),
                    string.Empty,
                    string.Empty,
                    Context,
                    typeof(PickGameObjectLineTranslationPatch));
            }

            SetContextData(__instance, data);
            TranslateStaticMenuOptions(__instance.GetType());

            var go = GetMemberValue(data, "go");
            if (go is null && GetMemberValue(data, "category") is null)
            {
                return true;
            }

            if (go is null)
            {
                ApplyCategoryRow(__instance, data);
                return false;
            }

            ApplyItemRow(__instance, data, go);
            return false;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: PickGameObjectLineTranslationPatch.Prefix failed: {0}", ex);
            return true;
        }
    }

    private static void ApplyCategoryRow(object instance, object data)
    {
        var category = GetRequiredStringMemberValue(data, "category");
        var source = "[" + (GetBoolMemberValue(data, "collapsed") ? "+" : "-") + "] {{K|" + category + "}}";
        var route = ObservabilityHelpers.ComposeContext(Context, "field=text");
        var translatedCategory = TranslateVisibleText(category, route, "PickGameObjectLine.CategoryText");
        var translated = "[" + (GetBoolMemberValue(data, "collapsed") ? "+" : "-") + "] {{K|" + translatedCategory + "}}";

        OwnerTextSetter.SetTranslatedText(
            GetMemberValue(instance, "text"),
            source,
            translated,
            Context,
            typeof(PickGameObjectLineTranslationPatch));
        var icon = GetMemberValue(instance, "icon");
        if (icon is not null)
        {
            TrySetActive(GetMemberValue(icon, "gameObject"), active: false);
        }

        TrySetActive(GetMemberValue(instance, "iconSpacer"), active: false);
        OwnerTextSetter.SetTranslatedText(
            GetMemberValue(instance, "rightFloatText"),
            string.Empty,
            string.Empty,
            Context,
            typeof(PickGameObjectLineTranslationPatch));
        ApplyHotkeyText(instance, data);
    }

    private static void ApplyItemRow(object instance, object data, object go)
    {
        var icon = GetMemberValue(instance, "icon");
        if (icon is not null)
        {
            TrySetActive(GetMemberValue(icon, "gameObject"), active: true);
        }

        TrySetActive(GetMemberValue(instance, "iconSpacer"), active: true);
        var renderable = AccessTools.Method(go.GetType(), "RenderForUI", Type.EmptyTypes)?.Invoke(go, null);
        if (icon is not null && renderable is not null)
        {
            _ = AccessTools.Method(icon.GetType(), "FromRenderable")?.Invoke(icon, new[] { renderable });
        }

        var source = BuildItemText(go);
        var route = ObservabilityHelpers.ComposeContext(Context, "field=text");
        var translated = TranslateItemText(go, route);
        OwnerTextSetter.SetTranslatedText(
            GetMemberValue(instance, "text"),
            source,
            translated,
            Context,
            typeof(PickGameObjectLineTranslationPatch));

        var weightValue = AccessTools.Method(go.GetType(), "GetWeight")?.Invoke(go, null);
        var weightText = "{{K|" + Convert.ToString(weightValue, CultureInfo.InvariantCulture) + "#}}";
        OwnerTextSetter.SetTranslatedText(
            GetMemberValue(instance, "rightFloatText"),
            weightText,
            weightText,
            Context,
            typeof(PickGameObjectLineTranslationPatch));
        ApplyHotkeyText(instance, data);
    }

    private static string BuildItemText(object go)
    {
        var builder = new StringBuilder();
        builder.Append(GetRequiredStringMemberValue(go, "DisplayName"));
        if (ShouldNotePlayerOwned() && GetBoolMemberValue(go, "OwnedByPlayer"))
        {
            builder.Append(" {{G|[owned by you]}}");
        }

        if (ShouldShowContext())
        {
            var context = AccessTools.Method(go.GetType(), "GetListDisplayContext")?.Invoke(go, new object?[] { null }) as string ?? string.Empty;
            builder.Append(" [");
            builder.Append(context);
            builder.Append(']');
        }

        return builder.ToString();
    }

    private static string TranslateItemText(object go, string route)
    {
        var builder = new StringBuilder();
        var displayName = GetRequiredStringMemberValue(go, "DisplayName");
        builder.Append(TranslateVisibleText(displayName, route, "PickGameObjectLine.ItemText"));
        if (ShouldNotePlayerOwned() && GetBoolMemberValue(go, "OwnedByPlayer"))
        {
            builder.Append(" {{G|[");
            builder.Append(TranslateFragment("owned by you"));
            builder.Append("]}}");
        }

        if (ShouldShowContext())
        {
            var context = AccessTools.Method(go.GetType(), "GetListDisplayContext")?.Invoke(go, new object?[] { null }) as string ?? string.Empty;
            builder.Append(" [");
            builder.Append(TranslateVisibleText(context, route, "PickGameObjectLine.ItemText"));
            builder.Append(']');
        }
        return builder.ToString();
    }

    private static void ApplyHotkeyText(object instance, object data)
    {
        var indent = GetBoolMemberValue(data, "indent") ? "   " : string.Empty;
        var hotkeyDescription = GetStringMemberValue(data, "hotkeyDescription");
        var text = string.IsNullOrEmpty(hotkeyDescription)
            ? indent + " "
            : indent + "{{Y|{{w|" + hotkeyDescription + "}}}} ";
        OwnerTextSetter.SetTranslatedText(
            GetMemberValue(instance, "hotkey"),
            text,
            text,
            Context,
            typeof(PickGameObjectLineTranslationPatch));
    }

    private static void TranslateStaticMenuOptions(Type instanceType)
    {
        TranslateMenuOptionCollection(GetStaticMemberValue(instanceType, "categoryExpandOptions"), "categoryExpandOptions");
        TranslateMenuOptionCollection(GetStaticMemberValue(instanceType, "categoryCollapseOptions"), "categoryCollapseOptions");
        TranslateMenuOptionCollection(GetStaticMemberValue(instanceType, "itemOptions"), "itemOptions");
    }

    private static void TranslateMenuOptionCollection(object? maybeCollection, string routeSuffix)
    {
        if (maybeCollection is null || maybeCollection is string || maybeCollection is not IEnumerable enumerable)
        {
            return;
        }

        var index = 0;
        foreach (var item in enumerable)
        {
            if (item is null)
            {
                index++;
                continue;
            }

            var current = GetStringMemberValue(item, "Description");
            if (!string.IsNullOrEmpty(current))
            {
                var route = ObservabilityHelpers.ComposeContext(Context, "field=" + routeSuffix + "[" + index + "]");
                var translated = TranslateVisibleText(current!, route, "PickGameObjectLine.MenuOption");
                if (!string.Equals(translated, current, StringComparison.Ordinal))
                {
                    SetMemberValue(item, "Description", translated);
                }
            }

            index++;
        }
    }

    private static string TranslateFragment(string source)
    {
        var translated = Translator.Translate(source);
        return string.Equals(translated, source, StringComparison.Ordinal) ? source : translated;
    }

    private static bool ShouldNotePlayerOwned()
    {
        var screenType = ResolvePickGameObjectScreenType();
        return GetStaticMemberValue(screenType, "NotePlayerOwned") as bool? ?? false;
    }

    private static bool ShouldShowContext()
    {
        var screenType = ResolvePickGameObjectScreenType();
        return GetStaticMemberValue(screenType, "ShowContext") as bool? ?? false;
    }

    private static string TranslateVisibleText(string source, string route, string family) => UiBindingTranslationHelpers.TranslateVisibleText(source, route, family);

    private static Type? ResolvePickGameObjectScreenType()
    {
        var screenType = AccessTools.TypeByName("Qud.UI.PickGameObjectScreen");
        if (screenType is not null)
        {
            return screenType;
        }

        Trace.TraceWarning("QudJP: {0} failed to resolve Qud.UI.PickGameObjectScreen. Trying simple name.", Context);
        return AccessTools.TypeByName("PickGameObjectScreen");
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

    private static object? GetStaticMemberValue(Type? type, string memberName)
    {
        if (type is null)
        {
            return null;
        }

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

    private static void SetMemberValue(object instance, string memberName, object? value) => UiBindingTranslationHelpers.SetMemberValue(instance, memberName, value);
}
