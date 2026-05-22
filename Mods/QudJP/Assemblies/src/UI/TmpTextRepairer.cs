#if HAS_TMP
using System;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
#endif

namespace QudJP;

internal static class TmpTextRepairer
{
#if HAS_TMP
    private const string ReplacementObjectName = "QudJPReplacementText";

    internal static int TryRepairInvisibleTexts(object? componentInstance)
    {
        if (componentInstance is not Component component)
        {
            return 0;
        }

        var texts = component.GetComponentsInChildren<TextMeshProUGUI>(includeInactive: true);
        var repaired = 0;
        for (var index = 0; index < texts.Length; index++)
        {
            if (TryRepairInvisibleText(texts[index]))
            {
                repaired++;
            }
        }

        return repaired;
    }

    internal static bool CanAttemptRepairForTests(
        bool enabled,
        bool activeInHierarchy,
        string? text,
        string objectName)
    {
        return enabled
            && activeInHierarchy
            && !string.IsNullOrEmpty(text)
            && !string.Equals(objectName, ReplacementObjectName, StringComparison.Ordinal);
    }

    internal static string BuildRepairLog(string probeName, int repairedCount)
    {
        return "[QudJP] " + probeName + ": repaired=" + repairedCount.ToString(CultureInfo.InvariantCulture);
    }

    private static bool TryRepairInvisibleText(TextMeshProUGUI text)
    {
        if (!CanAttemptRepair(text))
        {
            return false;
        }

        var currentText = text.text;
        if (string.IsNullOrEmpty(currentText))
        {
            return false;
        }

        text.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
        if (text.textInfo.characterCount > 0)
        {
            return false;
        }

        _ = FontManager.TryWarmPrimaryFontCharactersForUi(currentText);
        if (text.font is not null)
        {
            text.fontSharedMaterial = text.font.material;
        }

        text.havePropertiesChanged = true;
        text.SetAllDirty();
        text.text = currentText;
        text.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
        if (text.textInfo.characterCount > 0)
        {
            return true;
        }

        FontManager.ForcePrimaryFont(text);
        text.havePropertiesChanged = true;
        text.SetAllDirty();
        text.text = currentText;
        text.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
        return text.textInfo.characterCount > 0;
    }

    private static bool CanAttemptRepair(TextMeshProUGUI text)
    {
        return CanAttemptRepairForTests(
            text.enabled,
            text.gameObject.activeInHierarchy,
            text.text,
            text.gameObject.name);
    }

#endif
}
