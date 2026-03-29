using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class BookScreenTranslationPatch
{
    private const string Context = nameof(BookScreenTranslationPatch);

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var targetType = GameTypeResolver.FindType("Qud.UI.BookScreen", "BookScreen");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: BookScreenTranslationPatch target type not found.");
            return targets;
        }

        var markovBookType = GameTypeResolver.FindType("XRL.World.Parts.MarkovBook", "MarkovBook");
        AddTargetMethod(targets, targetType, "showScreen", new[] { markovBookType, typeof(string), typeof(Action<int>), typeof(Action<int>) });
        AddTargetMethod(targets, targetType, "showScreen", new[] { typeof(string), typeof(string), typeof(Action<int>), typeof(Action<int>) });

        if (targets.Count == 0)
        {
            Trace.TraceError("QudJP: BookScreenTranslationPatch resolved zero target methods.");
        }

        return targets;
    }

    public static void Prefix(object[] __args, MethodBase __originalMethod)
    {
        try
        {
            TranslateMenuOptions(__originalMethod.DeclaringType);
            if (__args.Length == 0 || __args[0] is null)
            {
                return;
            }

            if (__args[0] is string bookId)
            {
                TranslateBookIdTitle(bookId);
                return;
            }

            TranslateBookTitle(__args[0]);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: BookScreenTranslationPatch.Prefix failed: {0}", ex);
        }
    }

    private static void AddTargetMethod(List<MethodBase> targets, Type targetType, string methodName, Type?[] parameterTypes)
    {
        if (Array.IndexOf(parameterTypes, null) >= 0)
        {
            Trace.TraceError("QudJP: BookScreenTranslationPatch failed to resolve parameter types for {0}.{1}.", targetType.FullName, methodName);
            return;
        }

        var resolvedParameterTypes = Array.ConvertAll(parameterTypes, static parameterType => parameterType!);
        var method = AccessTools.Method(targetType, methodName, resolvedParameterTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: BookScreenTranslationPatch failed to resolve {0}.{1}.", targetType.FullName, methodName);
            return;
        }

        targets.Add(method);
    }

    private static void TranslateBookIdTitle(string bookId)
    {
        if (string.IsNullOrEmpty(bookId))
        {
            return;
        }

        var bookUiType = AccessTools.TypeByName("Qud.UI.BookUI");
        if (bookUiType is null) { bookUiType = AccessTools.TypeByName("BookUI"); }
        if (bookUiType is null)
        {
            Trace.TraceError("QudJP: BookScreenTranslationPatch could not resolve BookUI for book id '{0}'.", bookId);
            return;
        }

        var books = GetStaticMemberValue(bookUiType, "Books") as IDictionary;
        if (books is null || !books.Contains(bookId))
        {
            Trace.TraceWarning("QudJP: BookScreenTranslationPatch could not find BookUI.Books entry for '{0}'.", bookId);
            return;
        }

        TranslateBookTitle(books[bookId]);
    }

    private static void TranslateBookTitle(object? book)
    {
        if (book is null)
        {
            return;
        }

        var title = GetStringMemberValue(book, "Title");
        if (title is null)
        {
            Trace.TraceError("QudJP: BookScreenTranslationPatch book title member not found on '{0}'.", book.GetType().FullName);
            return;
        }

        var route = ObservabilityHelpers.ComposeContext(Context, "field=titleText");
        var translated = TranslateVisibleText(title, route, "BookScreen.TitleText");
        if (!string.Equals(translated, title, StringComparison.Ordinal))
        {
            SetMemberValue(book, "Title", translated);
        }
    }

    private static void TranslateMenuOptions(Type? targetType)
    {
        if (targetType is null)
        {
            return;
        }

        TranslateMenuOptionsCollection(GetStaticMemberValue(targetType, "getItemMenuOptions"), "getItemMenuOptions");
        TranslateMenuOption(GetStaticMemberValue(targetType, "PREV_PAGE"), "PREV_PAGE");
        TranslateMenuOption(GetStaticMemberValue(targetType, "NEXT_PAGE"), "NEXT_PAGE");
    }

    private static void TranslateMenuOptionsCollection(object? maybeCollection, string routeSuffix)
    {
        if (maybeCollection is null || maybeCollection is string || maybeCollection is not IEnumerable enumerable)
        {
            return;
        }

        var index = 0;
        foreach (var item in enumerable)
        {
            TranslateMenuOption(item, routeSuffix + "[" + index + "]");
            index++;
        }
    }

    private static void TranslateMenuOption(object? menuOption, string routeSuffix)
    {
        if (menuOption is null)
        {
            return;
        }

        TranslateMenuOptionMember(menuOption, "Description", routeSuffix + ".Description");
        TranslateMenuOptionMember(menuOption, "KeyDescription", routeSuffix + ".KeyDescription");
    }

    private static void TranslateMenuOptionMember(object menuOption, string memberName, string routeSuffix)
    {
        var current = GetStringMemberValue(menuOption, memberName);
        if (string.IsNullOrEmpty(current))
        {
            return;
        }

        var route = ObservabilityHelpers.ComposeContext(Context, "field=" + routeSuffix);
        var translated = TranslateVisibleText(current!, route, "BookScreen.MenuOption");
        if (!string.Equals(translated, current, StringComparison.Ordinal))
        {
            SetMemberValue(menuOption, memberName, translated);
        }
    }

    private static string TranslateVisibleText(string source, string route, string family)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source;
        }

        var translated = ColorAwareTranslationComposer.TranslatePreservingColors(
            source,
            static visible => StringHelpers.TryGetTranslationExactOrLowerAscii(visible, out var candidate)
                ? candidate
                : visible);
        if (!string.Equals(translated, source, StringComparison.Ordinal))
        {
            DynamicTextObservability.RecordTransform(route, family, source, translated);
        }

        return translated;
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

    private static object? GetMemberValue(object instance, string memberName)
    {
        var type = instance.GetType();
        var property = AccessTools.Property(type, memberName);
        if (property is not null && property.CanRead)
        {
            return property.GetValue(instance);
        }

        var field = AccessTools.Field(type, memberName);
        return field?.GetValue(instance);
    }

    private static string? GetStringMemberValue(object instance, string memberName)
    {
        return GetMemberValue(instance, memberName) as string;
    }

    private static void SetMemberValue(object instance, string memberName, object? value)
    {
        var type = instance.GetType();
        var property = AccessTools.Property(type, memberName);
        if (property is not null && property.CanWrite)
        {
            property.SetValue(instance, value);
            return;
        }

        var field = AccessTools.Field(type, memberName);
        field?.SetValue(instance, value);
    }
}
