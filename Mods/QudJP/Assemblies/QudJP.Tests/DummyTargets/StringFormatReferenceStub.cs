#if !HAS_GAME_DLL
namespace XRL.UI;

internal static class StringFormat
{
    public static string ClipText(string source, int maxWidth)
    {
        return source.Length <= maxWidth ? source : source.Substring(0, maxWidth);
    }
}
#endif
