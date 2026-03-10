using System.Diagnostics;
using System.Threading;

namespace QudJP;

public static class FontManager
{
    private static int isInitialized;

    public static void Initialize()
    {
        if (Interlocked.Exchange(ref isInitialized, 1) == 1)
        {
            return;
        }

        Trace.TraceInformation("QudJP FontManager: CJK fallback font loading is pending implementation.");
    }
}
