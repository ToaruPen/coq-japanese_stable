#if HAS_GAME_DLL
using System.Reflection;
using HarmonyLib;
using HistoryKit;
using Qud.API;
using XRL.Annals;
using XRL.World.Conversations.Parts;
using XRL.World.WorldBuilders;
using XRL.World.ZoneBuilders;

namespace QudJP.Tests.L2G;

[TestFixture]
[Category("L2G")]
public sealed class HistoricNarrativeTranslationPatchesResolutionTests
{
    [Test]
    public void GenerateVillageEraHistoryPatch_ResolvesTarget()
    {
        var patchType = typeof(Translator).Assembly
            .GetType("QudJP.Patches.GenerateVillageEraHistoryTranslationPatch", throwOnError: false);
        Assert.That(patchType, Is.Not.Null, "GenerateVillageEraHistoryTranslationPatch type not found.");

        var attribute = patchType!.GetCustomAttribute<HarmonyPatch>();
        Assert.That(attribute, Is.Not.Null,
            "GenerateVillageEraHistoryTranslationPatch is missing the [HarmonyPatch] attribute.");

        var method = AccessTools.DeclaredMethod(typeof(QudHistoryFactory), nameof(QudHistoryFactory.GenerateVillageEraHistory));
        Assert.That(method, Is.Not.Null, "QudHistoryFactory.GenerateVillageEraHistory(History) not found.");
        Assert.That(method!.GetParameters().Length, Is.EqualTo(1));
        Assert.That(method.GetParameters()[0].ParameterType, Is.EqualTo(typeof(History)));
        Assert.That(method.ReturnType, Is.EqualTo(typeof(History)));
    }

    [Test]
    public void AddVillageGospelsPatch_ResolvesHistoricEntityOverload()
    {
        var patchType = typeof(Translator).Assembly
            .GetType("QudJP.Patches.AddVillageGospelsTranslationPatch", throwOnError: false);
        Assert.That(patchType, Is.Not.Null, "AddVillageGospelsTranslationPatch type not found.");

        var attribute = patchType!.GetCustomAttribute<HarmonyPatch>();
        Assert.That(attribute, Is.Not.Null,
            "AddVillageGospelsTranslationPatch is missing the [HarmonyPatch] attribute.");

        var method = AccessTools.DeclaredMethod(typeof(JournalAPI), nameof(JournalAPI.AddVillageGospels), new[] { typeof(HistoricEntity) });
        Assert.That(method, Is.Not.Null, "JournalAPI.AddVillageGospels(HistoricEntity) not found.");
        Assert.That(method!.GetParameters().Length, Is.EqualTo(1));
        Assert.That(method.GetParameters()[0].ParameterType, Is.EqualTo(typeof(HistoricEntity)));
    }

    [Test]
    public void AddVillageGospels_HasSnapshotOverload()
    {
        var method = AccessTools.DeclaredMethod(typeof(JournalAPI), nameof(JournalAPI.AddVillageGospels), new[] { typeof(HistoricEntitySnapshot) });
        Assert.That(method, Is.Not.Null, "JournalAPI.AddVillageGospels(HistoricEntitySnapshot) not found.");
    }

    // Worships and Despises are in XRL.Annals (not XRL.World.Conversations.Parts).
    // PostProcessEvent is a public static method with signature (HistoricEntity, string, string).
    [Test]
    public void Worships_HasPostProcessEvent()
    {
        var method = AccessTools.DeclaredMethod(typeof(Worships), "PostProcessEvent",
            new[] { typeof(HistoricEntity), typeof(string), typeof(string) });
        Assert.That(method, Is.Not.Null, "Worships.PostProcessEvent(HistoricEntity,string,string) not found.");
    }

    [Test]
    public void Despises_HasPostProcessEvent()
    {
        var method = AccessTools.DeclaredMethod(typeof(Despises), "PostProcessEvent",
            new[] { typeof(HistoricEntity), typeof(string), typeof(string) });
        Assert.That(method, Is.Not.Null, "Despises.PostProcessEvent(HistoricEntity,string,string) not found.");
    }

    // VillageCoda.GenerateVillageEntity is a public static method in XRL.World.ZoneBuilders.
    [Test]
    public void VillageCoda_HasGenerateVillageEntity()
    {
        var method = typeof(VillageCoda).GetMethod("GenerateVillageEntity",
            BindingFlags.Public | BindingFlags.Static);
        Assert.That(method, Is.Not.Null, "VillageCoda.GenerateVillageEntity not found.");
    }

    // EndGame is in XRL.World.Conversations.Parts; ApplyVillage is a public static method.
    [Test]
    public void EndGame_HasApplyVillage()
    {
        var method = typeof(EndGame).GetMethod("ApplyVillage",
            BindingFlags.Public | BindingFlags.Static);
        Assert.That(method, Is.Not.Null, "EndGame.ApplyVillage not found.");
    }

    // JoppaWorldBuilder.AddVillages is a public instance method in XRL.World.WorldBuilders.
    [Test]
    public void JoppaWorldBuilder_HasAddVillages()
    {
        var method = typeof(JoppaWorldBuilder).GetMethod("AddVillages",
            BindingFlags.Public | BindingFlags.Instance);
        Assert.That(method, Is.Not.Null, "JoppaWorldBuilder.AddVillages not found.");
    }
}
#endif
