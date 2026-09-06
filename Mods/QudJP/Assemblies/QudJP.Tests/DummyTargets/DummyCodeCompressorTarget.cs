using System.Collections.Generic;
using System.Runtime.CompilerServices;
using XRL.CharacterBuilds;

namespace QudJP.Tests.DummyTargets;

internal static class DummyCodeCompressorTarget
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void loadCode(
        string code,
        List<AbstractEmbarkBuilderModule> modules,
        bool silent)
    {
        DummyPopupShow.ShowAsync(code).GetAwaiter().GetResult();
    }
}
