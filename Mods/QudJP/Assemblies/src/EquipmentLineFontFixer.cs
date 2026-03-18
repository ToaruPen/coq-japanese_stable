#if HAS_TMP
using TMPro;
using UnityEngine;
#endif

namespace QudJP;

internal static class EquipmentLineFontFixer
{
#if HAS_TMP
    internal static int TryApplyPrimaryFontToEquipmentLine(object? equipmentLineInstance)
    {
        if (equipmentLineInstance is not Component component)
        {
            return 0;
        }

        var texts = component.GetComponentsInChildren<TextMeshProUGUI>(includeInactive: true);
        var applied = 0;
        for (var index = 0; index < texts.Length; index++)
        {
            FontManager.ForcePrimaryFont(texts[index]);
            applied++;
        }

        return applied;
    }
#endif
}
