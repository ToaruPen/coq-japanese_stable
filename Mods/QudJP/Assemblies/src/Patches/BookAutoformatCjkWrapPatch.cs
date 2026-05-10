using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using QudJP.UI;

namespace QudJP.Patches;

[HarmonyPatch]
public static class BookAutoformatCjkWrapPatch
{
    private const string TargetTypeName = "XRL.UI.BookUI";
    private const int BookScreenColumns = 51;
    private const int BookMaxWrappedLines = 5000;
    private const int BookPreferredBreakSearchColumns = 2;

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        try
        {
            var targetType = AccessTools.TypeByName(TargetTypeName);
            if (targetType is null)
            {
                Trace.TraceError("QudJP: Failed to resolve XRL.UI.BookUI. Book autoformat CJK wrap patch will not apply.");
                return null;
            }

            var method = AccessTools.Method(
                targetType,
                "AutoformatPages",
                new[]
                {
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(int),
                    typeof(int),
                    typeof(int),
                    typeof(int),
                });
            if (method is not null)
            {
                return method;
            }

            Trace.TraceError("QudJP: Failed to resolve BookUI.AutoformatPages(string,string,string,int,int,int,int). Book autoformat CJK wrap patch will not apply.");
            return null;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: BookAutoformatCjkWrapPatch.TargetMethod failed: {0}", ex);
            return null;
        }
    }

    public static void Prefix(ref string Text, int LeftMargin = 2, int RightMargin = 2, int TopMargin = 2, int BottomMargin = 2)
    {
        try
        {
            Text = WrapForAutoformat(Text, LeftMargin, RightMargin, TopMargin, BottomMargin);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: BookAutoformatCjkWrapPatch.Prefix failed: {0}", ex);
        }
    }

    internal static string WrapForAutoformatForTests(string text, int leftMargin, int rightMargin, int topMargin, int bottomMargin)
    {
        return WrapForAutoformat(text, leftMargin, rightMargin, topMargin, bottomMargin);
    }

    private static string WrapForAutoformat(string text, int leftMargin, int rightMargin, int topMargin, int bottomMargin)
    {
        _ = topMargin;
        _ = bottomMargin;

        var width = BookScreenColumns - leftMargin - rightMargin;
        return JapaneseBlockWrap.TryWrapForCjkBlock(
                text,
                width,
                BookMaxWrappedLines,
                out var wrapped,
                BookPreferredBreakSearchColumns,
                reopenFormattingAfterLineBreak: false)
            ? wrapped
            : text;
    }
}
