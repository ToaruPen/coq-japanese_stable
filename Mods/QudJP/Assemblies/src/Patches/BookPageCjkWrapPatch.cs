using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using QudJP.UI;
#if HAS_GAME_DLL
using XRL.UI;
#endif

namespace QudJP.Patches;

[HarmonyPatch]
public static class BookPageCjkWrapPatch
{
#if !HAS_GAME_DLL
    private const string TargetTypeName = "XRL.UI.BookPage";
#endif
    private const int BookPageColumns = 47;
    private const int BookPageMaxWrappedLines = 5000;
    private const int BookPreferredBreakSearchColumns = 2;

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        try
        {
            var targetType = ResolveTargetType();
            if (targetType is null)
            {
                Trace.TraceError("QudJP: Failed to resolve XRL.UI.BookPage. Book page CJK wrap patch will not apply.");
                return null;
            }

            var constructor = AccessTools.Constructor(targetType, new[] { typeof(string), typeof(string) })
                ?? targetType.GetConstructor(new[] { typeof(string), typeof(string) });
            if (constructor is not null)
            {
                return constructor;
            }

            Trace.TraceError("QudJP: Failed to resolve BookPage(string,string). Book page CJK wrap patch will not apply.");
            return null;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: BookPageCjkWrapPatch.TargetMethod failed: {0}", ex);
            return null;
        }
    }

    private static Type? ResolveTargetType()
    {
#if HAS_GAME_DLL
        return typeof(BookPage);
#else
        return AccessTools.TypeByName(TargetTypeName);
#endif
    }

    public static void Prefix(ref string Data)
    {
        try
        {
            Data = WrapForBookPage(Data);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: BookPageCjkWrapPatch.Prefix failed: {0}", ex);
        }
    }

    internal static string WrapForBookPageForTests(string data)
    {
        return WrapForBookPage(data);
    }

    private static string WrapForBookPage(string data)
    {
        if (LooksAlreadyBookFormatted(data))
        {
            return data;
        }

        return JapaneseBlockWrap.TryWrapForCjkBlock(
                data,
                BookPageColumns,
                BookPageMaxWrappedLines,
                out var wrapped,
                BookPreferredBreakSearchColumns,
                reopenFormattingAfterLineBreak: false)
            ? wrapped
            : data;
    }

    private static bool LooksAlreadyBookFormatted(string data)
    {
        return data.Contains("&y&y&y")
            || data.Contains("\n&y\n");
    }
}
