using System;

namespace QudJP.Patches;

internal static class OwnerTextSetter
{
    internal static void SetTranslatedText(
        object? uiTextSkin,
        string source,
        string translated,
        string context,
        Type patchType)
    {
        var value = translated;
        if (!string.Equals(source, translated, StringComparison.Ordinal)
            && uiTextSkin is not null
            && uiTextSkin.GetType().Assembly != patchType.Assembly)
        {
            value = MessageFrameTranslator.MarkDirectTranslation(translated);
        }

        _ = UITextSkinReflectionAccessor.SetCurrentText(uiTextSkin, value, context);
    }
}
