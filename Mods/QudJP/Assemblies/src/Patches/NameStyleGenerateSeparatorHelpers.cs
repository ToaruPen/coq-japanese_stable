namespace QudJP.Patches;

internal static class NameStyleGenerateSeparatorHelpers
{
    internal const char JapaneseNameSeparator = '・';

    internal static char TranslateSeparator(char source)
    {
        return source is '-' or ' '
            ? JapaneseNameSeparator
            : source;
    }
}
