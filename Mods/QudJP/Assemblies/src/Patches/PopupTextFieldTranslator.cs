using System;
using HarmonyLib;

namespace QudJP.Patches;

internal static class PopupTextFieldTranslator
{
    internal static bool TryTranslateTextField(
        object? item,
        Func<string, string> translate,
        bool translateNullAsEmpty = false)
    {
        if (item is null)
        {
            return false;
        }

        var textField = AccessTools.Field(item.GetType(), "text");
        if (textField is null || textField.FieldType != typeof(string))
        {
            return false;
        }

        var originalText = textField.GetValue(item) as string;
        string sourceText;
        if (originalText is null)
        {
            if (!translateNullAsEmpty)
            {
                return false;
            }

            sourceText = string.Empty;
        }
        else if (originalText.Length == 0)
        {
            return false;
        }
        else
        {
            sourceText = originalText;
        }

        var translated = translate(sourceText);
        var currentText = textField.GetValue(item) as string;
        if (string.Equals(currentText, translated, StringComparison.Ordinal))
        {
            return false;
        }

        textField.SetValue(item, translated);
        return true;
    }
}
