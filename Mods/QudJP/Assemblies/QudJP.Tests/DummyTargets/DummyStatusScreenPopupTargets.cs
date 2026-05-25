using System.Runtime.CompilerServices;

namespace QudJP.Tests.DummyTargets;

internal static class DummyStatusScreenPopupTarget
{
    public static string MessageToSend { get; set; } = string.Empty;

    public static string PickOptionTitleToSend { get; set; } = string.Empty;

    public static string PickOptionIntroToSend { get; set; } = string.Empty;

    public static IReadOnlyList<string>? PickOptionOptionsToSend { get; set; }

    public static void Reset()
    {
        MessageToSend = string.Empty;
        PickOptionTitleToSend = string.Empty;
        PickOptionIntroToSend = string.Empty;
        PickOptionOptionsToSend = null;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void BuyStat(DummyGameObject go, string chosenStat)
    {
        _ = go;
        _ = chosenStat;
        DummyPopupShow.Show(MessageToSend);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool BuyRandomMutation(DummyGameObject go)
    {
        _ = go;
        if (!string.IsNullOrEmpty(PickOptionTitleToSend)
            || !string.IsNullOrEmpty(PickOptionIntroToSend)
            || PickOptionOptionsToSend is not null)
        {
            DummyPopupGenericTarget.PickOption(
                Title: PickOptionTitleToSend,
                Intro: PickOptionIntroToSend,
                Options: PickOptionOptionsToSend);
            return true;
        }

        DummyPopupShow.Show(MessageToSend);
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Show(DummyGameObject go)
    {
        _ = go;
        DummyPopupShow.Show(MessageToSend);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ShowMutationPopup(DummyGameObject go, DummyCharacterMutation mutation)
    {
        _ = go;
        _ = mutation;
        DummyPopupShow.Show(MessageToSend);
    }
}
