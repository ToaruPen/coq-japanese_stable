namespace QudJP.Patches;

internal static class OwnerTranslationScope
{
    internal static bool IsActive(int activeDepth)
    {
        return activeDepth > 0;
    }

    internal static void Enter(ref int activeDepth)
    {
        activeDepth++;
    }

    internal static void Exit(ref int activeDepth)
    {
        if (activeDepth > 0)
        {
            activeDepth--;
        }
    }
}
