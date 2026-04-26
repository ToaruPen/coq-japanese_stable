#if HAS_GAME_DLL
using System.Reflection;
using HarmonyLib;

namespace QudJP.Tests.L2G;

[TestFixture]
[Category("L2G")]
public sealed class HistoricNarrativePatchPresenceTests
{
    [Test]
    public void GenerateVillageEraHistoryTranslationPatch_IsPresentInProductionAssembly()
    {
        var type = typeof(QudJPMod).Assembly.GetType("QudJP.Patches.GenerateVillageEraHistoryTranslationPatch", throwOnError: false);
        Assert.That(type, Is.Not.Null,
            "GenerateVillageEraHistoryTranslationPatch must be in the production assembly so Harmony can apply it.");
    }

    [Test]
    public void AddVillageGospelsTranslationPatch_IsPresentInProductionAssembly()
    {
        var type = typeof(QudJPMod).Assembly.GetType("QudJP.Patches.AddVillageGospelsTranslationPatch", throwOnError: false);
        Assert.That(type, Is.Not.Null,
            "AddVillageGospelsTranslationPatch must be in the production assembly so Harmony can apply it.");
    }
}
#endif
