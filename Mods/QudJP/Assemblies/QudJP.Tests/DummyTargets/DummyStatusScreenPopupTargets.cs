using System.Runtime.CompilerServices;

namespace QudJP.Tests.DummyTargets;

internal static class DummyStatusScreenPopupTarget
{
    public static string MessageToSend { get; set; } = string.Empty;

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
