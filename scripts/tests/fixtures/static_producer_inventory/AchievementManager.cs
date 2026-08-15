public static class AchievementManager
{
    public sealed class AchievementWishes
    {
        public static void DebugUnlockAnyAchievement()
        {
            AddPlayerMessage("Unlocked Achievement: " + achievementId);
            AddPlayerMessage("All Achievements Already Unlocked");
        }
    }
}
