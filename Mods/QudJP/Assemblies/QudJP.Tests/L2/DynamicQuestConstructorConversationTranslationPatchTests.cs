using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class DynamicQuestConstructorConversationTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
        DummyDynamicQuestConstructorConversationTarget.Reset();
    }

    [Test]
    public void AddQuestConversationToGiver_TranslatesOnlyConstructorSafePrompts_WhenPatched()
    {
        WithPatchedConstructorMethods(() =>
        {
            DummyDynamicQuestConstructorConversationTarget.FindSpecificItem();
            DummyDynamicQuestConstructorConversationTarget.FindSpecificSite();
            DummyDynamicQuestConstructorConversationTarget.InteractWithObject();

            Assert.Multiple(() =>
            {
                Assert.That(
                    DummyDynamicQuestConstructorConversationTarget.Nodes,
                    Does.Contain("かつて私の同胞は炉の世話に日々を費やしていた。塩の聖性を知ってから、われらは習わしを改め、新たな儀式を作った。残念ながら、やるべき用事がある。Mehmetがわれらの{{|遺物}}を失った。{{|錆の井戸}}の近くに{{|遺物}}があると知った。料理のために、それが必要だ。*it*を探し出し、われらのもとへ返してくれるか？*it*を取り戻してくれれば、あなたの助力には代価を支払う。{{|錆の井戸}}に運ばれたと聞いている。引き受けてくれるか？あなたの奉仕には報いる。"));
                Assert.That(
                    DummyDynamicQuestConstructorConversationTarget.Nodes,
                    Does.Contain("先日、旅人たちがわれらの村に来た。彼らはパンを分け合っている間、{{|隠された文書庫}}という興味深い場所について話した。冒険者よ、われらは記録を調べ、民に忘れられていた近くの場所、{{|隠された文書庫}}を見つけた。聖所：忍耐。それを探し出してくれないか？それを見つけてくれれば、あなたの労には報酬を出す。{{|六日のスティルト}}の北、4から6パラサング離れたどこかにあると聞いている。それはわれらの村の交易の見通しにとって大きな恩恵となる。あなたの助力には代価を支払う。この場所にはどんな秘密が隠されているのか？"));
                Assert.That(
                    DummyDynamicQuestConstructorConversationTarget.Nodes,
                    Does.Contain("{{|塩の祠}}にある{{|聖なる器}}のことを聞いたことはあるか、冒険者よ? それはわれらにとって神聖な祠だ. われらはしばしば巡礼してitを祈るし、patienceについて思索する. あなたも同じことをしてくれれば、われらの誉れとなる. 引き受けてくれるか? あなたの奉仕には報いる."));
                Assert.That(
                    DummyDynamicQuestConstructorConversationTarget.Nodes,
                    Does.Contain("*慎重にあたりを見回す*\n\n近くへ、友よ。私の{{|計画}}はもうすぐ成就する。残る手順はあと一つだ。{{|塩の祠}}へ行き、そこの{{|聖なる器}}を祈るしてくれる者が必要だ。いや、理由は話せない。引き受けてくれるか。あなたの奉仕には報いる。\n\n忍耐にかけて、このことは誰にも話すな。"));
                Assert.That(
                    DummyDynamicQuestConstructorConversationTarget.Nodes,
                    Does.Contain("The rest of this generated introduction has *itemName* placeholders."));
                Assert.That(HitCount(), Is.GreaterThanOrEqualTo(35));
            });
        });

        var outsideOwner = DummyHistoricStringExpander.ExpandString("Will you?");
        Assert.That(outsideOwner, Is.EqualTo("Will you?"));
    }

    [Test]
    public void AddQuestConversationToGiver_StripsDirectMarkerWithoutObservabilityHit_WhenPatched()
    {
        WithPatchedConstructorMethods(() =>
        {
            DummyDynamicQuestConstructorConversationTarget.ItemPrompt =
                MessageFrameTranslator.DirectTranslationMarker + "Will you?";
            DummyDynamicQuestConstructorConversationTarget.ItemReward = "Unknown reward text";
            DummyDynamicQuestConstructorConversationTarget.ItemSacredIntro = "Unknown sacred intro";
            DummyDynamicQuestConstructorConversationTarget.ItemAfterLearning = "Unknown after learning";
            DummyDynamicQuestConstructorConversationTarget.ItemMisfortune = "Unknown misfortune";
            DummyDynamicQuestConstructorConversationTarget.ItemTask = "Unknown task text";
            DummyDynamicQuestConstructorConversationTarget.ItemRumor = "Unknown rumor text";
            DummyDynamicQuestConstructorConversationTarget.ItemNeed = "Unknown need text";
            DummyDynamicQuestConstructorConversationTarget.ItemLostOur = "Unknown lost text";
            DummyDynamicQuestConstructorConversationTarget.ItemRecoverPrompt = "Unknown recover text";
            DummyDynamicQuestConstructorConversationTarget.ItemIfYouRetrieveIt = "Unknown retrieve text";
            DummyDynamicQuestConstructorConversationTarget.ItemTakenTo = "Unknown taken-to text";

            DummyDynamicQuestConstructorConversationTarget.FindSpecificItem();

            Assert.Multiple(() =>
            {
                Assert.That(
                    DummyDynamicQuestConstructorConversationTarget.Nodes,
                    Does.Contain("Unknown sacred intro. Unknown after learning. Unknown misfortune, Unknown task text. Unknown lost text. Unknown rumor text. Unknown need text. Unknown recover text Unknown retrieve text. Unknown taken-to text. Will you? Unknown reward text."));
                Assert.That(HitCount(), Is.Zero);
            });
        });
    }

    private static void WithPatchedConstructorMethods(Action action)
    {
        var harmonyId = "qudjp.tests.dynamic-quest-constructor-conversation." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            var transpiler = new HarmonyMethod(RequireMethod(
                typeof(DynamicQuestConstructorConversationTranslationPatch),
                nameof(DynamicQuestConstructorConversationTranslationPatch.Transpiler),
                typeof(IEnumerable<CodeInstruction>)));
            harmony.Patch(
                original: RequireMethod(
                    typeof(DummyDynamicQuestConstructorConversationTarget),
                    nameof(DummyDynamicQuestConstructorConversationTarget.FindSpecificItem)),
                transpiler: transpiler);
            harmony.Patch(
                original: RequireMethod(
                    typeof(DummyDynamicQuestConstructorConversationTarget),
                    nameof(DummyDynamicQuestConstructorConversationTarget.FindSpecificSite)),
                transpiler: transpiler);
            harmony.Patch(
                original: RequireMethod(
                    typeof(DummyDynamicQuestConstructorConversationTarget),
                    nameof(DummyDynamicQuestConstructorConversationTarget.InteractWithObject)),
                transpiler: transpiler);
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static int HitCount()
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(DynamicQuestConstructorConversationTranslationPatch),
            nameof(DynamicQuestConstructorConversationTranslationPatch) + ".ExpandString");
    }

    private static MethodInfo RequireMethod(Type type, string name, params Type[] parameterTypes)
    {
        var method = AccessTools.Method(type, name, parameterTypes);
        Assert.That(method, Is.Not.Null, $"{type.FullName}.{name} not found");
        return method!;
    }
}

internal static class DummyDynamicQuestConstructorConversationTarget
{
    public static string ItemPrompt { get; set; } = "Will you?";

    public static string SitePrompt { get; set; } = "Would you be willing to locate it?";

    public static string ObjectPrompt { get; set; } = "Would you";

    public static string ItemReward { get; set; } = "We will reward your services";

    public static string SiteReward { get; set; } = "We will pay you for your assistance";

    public static string ObjectReward { get; set; } = "I will reward your services";

    public static string ItemSacredIntro { get; set; } = "Come, close! My kin used to spend our days *Activity*";

    public static string ItemAfterLearning { get; set; } = "But after learning the *sanctityOfSacredThing*, we changed our ways and composed new rituals";

    public static string ItemMisfortune { get; set; } = "Unfortunately";

    public static string ItemTask { get; set; } = "I have a errand that needs doing";

    public static string ItemRumor { get; set; } = "Recently I learned that there's *itemName.an* nearby in *itemLocation*";

    public static string ItemNeed { get; set; } = "*NeedsItemFor*, I must have *it*";

    public static string ItemLostOur { get; set; } = "*name* lost our *itemName*";

    public static string ItemRecoverPrompt { get; set; } = "Would you seek *it* out and return *it* to us?";

    public static string ItemIfYouRetrieveIt { get; set; } = "Fetch *it* and I'll pay you for your assistance";

    public static string ItemTakenTo { get; set; } = "We hear *it* *has* been taken to *deliveryTarget*";

    public static string SiteBoon { get; set; } = "It would be a great gift to the trade prospects of our village";

    public static string SiteTreasures { get; set; } = "What secrets might this place hide";

    public static string SiteIfYouFindIt { get; set; } = "If you locate it for us, we will compensate you for your labor";

    public static string SiteDirections { get; set; } = "We hear it's located somewhere between *min* and *max* parasangs *direction* of *landmark*";

    public static string SiteTravelersCame { get; set; } = "Traveling wanderers came to our village the other day";

    public static string SiteSpokeOfPlace { get; set; } = "While *GuestActivity*, they spoke of a fascinating place, *site*";

    public static string SiteRecordsIntro { get; set; } = "Adventurer, we've been poring over our records and we learned of a nearby location forgotten to our people, *siteInitLower*";

    public static string SiteShrine { get; set; } = "A shrine to ";

    public static string ObjectIntro { get; set; } = "Adventurer, have you heard of the *itemName* at *deliveryTarget*";

    public static string ObjectHoly { get; set; } = "*It* is a sacred shrine to us";

    public static string ObjectWillInteract { get; set; } = "Often we make pilgrimages to *verb* *it* and contemplate *sacredThing*";

    public static string ObjectHonor { get; set; } = "It would honor us if you would do the same";

    public static string StrangeIntro { get; set; } = "*looks around suspiciously*";

    public static string StrangeComeClose { get; set; } = "Come, close!";

    public static string StrangePlan { get; set; } = "My *plan* is nearly complete. Only one more thing must be done";

    public static string StrangeGoTo { get; set; } = "I need someone to go to *deliveryTarget* and *verb* the *itemName* there";

    public static string StrangeTellNoOne { get; set; } = "By *sacredThing*, tell no one of this";

    public static List<string> Nodes { get; } = [];

    public static void Reset()
    {
        ItemPrompt = "Will you?";
        SitePrompt = "Would you be willing to locate it?";
        ObjectPrompt = "Would you";
        ItemReward = "We will reward your services";
        SiteReward = "We will pay you for your assistance";
        ObjectReward = "I will reward your services";
        ItemSacredIntro = "Come, close! My kin used to spend our days *Activity*";
        ItemAfterLearning = "But after learning the *sanctityOfSacredThing*, we changed our ways and composed new rituals";
        ItemMisfortune = "Unfortunately";
        ItemTask = "I have a errand that needs doing";
        ItemRumor = "Recently I learned that there's *itemName.an* nearby in *itemLocation*";
        ItemNeed = "*NeedsItemFor*, I must have *it*";
        ItemLostOur = "*name* lost our *itemName*";
        ItemRecoverPrompt = "Would you seek *it* out and return *it* to us?";
        ItemIfYouRetrieveIt = "Fetch *it* and I'll pay you for your assistance";
        ItemTakenTo = "We hear *it* *has* been taken to *deliveryTarget*";
        SiteBoon = "It would be a great gift to the trade prospects of our village";
        SiteTreasures = "What secrets might this place hide";
        SiteIfYouFindIt = "If you locate it for us, we will compensate you for your labor";
        SiteDirections = "We hear it's located somewhere between *min* and *max* parasangs *direction* of *landmark*";
        SiteTravelersCame = "Traveling wanderers came to our village the other day";
        SiteSpokeOfPlace = "While *GuestActivity*, they spoke of a fascinating place, *site*";
        SiteRecordsIntro = "Adventurer, we've been poring over our records and we learned of a nearby location forgotten to our people, *siteInitLower*";
        SiteShrine = "A shrine to ";
        ObjectIntro = "Adventurer, have you heard of the *itemName* at *deliveryTarget*";
        ObjectHoly = "*It* is a sacred shrine to us";
        ObjectWillInteract = "Often we make pilgrimages to *verb* *it* and contemplate *sacredThing*";
        ObjectHonor = "It would honor us if you would do the same";
        StrangeIntro = "*looks around suspiciously*";
        StrangeComeClose = "Come, close!";
        StrangePlan = "My *plan* is nearly complete. Only one more thing must be done";
        StrangeGoTo = "I need someone to go to *deliveryTarget* and *verb* the *itemName* there";
        StrangeTellNoOne = "By *sacredThing*, tell no one of this";
        Nodes.Clear();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void FindSpecificItem()
    {
        AddNode(DummyHistoricStringExpander.ExpandString(ItemSacredIntro)
                .Replace("*Activity*", "tending hearths") + ". "
            + DummyHistoricStringExpander.ExpandString(ItemAfterLearning)
                .Replace("*sanctityOfSacredThing*", "the sanctity of salt") + ". "
            + DummyHistoricStringExpander.ExpandString(ItemMisfortune) + ", "
            + DummyHistoricStringExpander.ExpandString(ItemTask) + ". "
            + DummyHistoricStringExpander.ExpandString(ItemLostOur)
                .Replace("*name*", "Mehmet")
                .Replace("*itemName*", "{{|relic}}") + ". "
            + DummyHistoricStringExpander.ExpandString(ItemRumor)
                .Replace("*itemLocation*", "{{|the rust wells}}")
                .Replace("*itemName.an*", "{{|a relic}}") + ". "
            + DummyHistoricStringExpander.ExpandString(ItemNeed)
                .Replace("*NeedsItemFor*", "cooking")
                .Replace("*it*", "it") + ". "
            + DummyHistoricStringExpander.ExpandString(ItemRecoverPrompt) + " "
            + DummyHistoricStringExpander.ExpandString(ItemIfYouRetrieveIt) + ". "
            + DummyHistoricStringExpander.ExpandString(ItemTakenTo)
                .Replace("*deliveryTarget*", "{{|the rust wells}}")
                .Replace("*it*", "it")
                .Replace("*has*", "has") + ". "
            + DummyHistoricStringExpander.ExpandString(ItemPrompt) + " "
            + DummyHistoricStringExpander.ExpandString(ItemReward) + ".");
        AddNode(DummyHistoricStringExpander.ExpandString("The rest of this generated introduction has *itemName* placeholders."));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void FindSpecificSite()
    {
        AddNode(DummyHistoricStringExpander.ExpandString(SiteTravelersCame) + ". "
            + DummyHistoricStringExpander.ExpandString(SiteSpokeOfPlace)
                .Replace("*GuestActivity*", "breaking bread")
                .Replace("*site*", "{{|the hidden archive}}") + ". "
            + DummyHistoricStringExpander.ExpandString(SiteRecordsIntro)
                .Replace("*siteInitLower*", "{{|the hidden archive}}") + ". "
            + DummyHistoricStringExpander.ExpandString(SiteShrine) + "patience. "
            + DummyHistoricStringExpander.ExpandString(SitePrompt) + " "
            + DummyHistoricStringExpander.ExpandString(SiteIfYouFindIt) + ". "
            + DummyHistoricStringExpander.ExpandString(SiteDirections)
                .Replace("*landmark*", "{{|the six day stilt}}")
                .Replace("*min*", "4")
                .Replace("*max*", "6")
                .Replace("*direction*", "north") + ". "
            + DummyHistoricStringExpander.ExpandString(SiteBoon) + ". "
            + DummyHistoricStringExpander.ExpandString(SiteReward) + ". "
            + DummyHistoricStringExpander.ExpandString(SiteTreasures) + "?");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void InteractWithObject()
    {
        AddNode(DummyHistoricStringExpander.ExpandString(ObjectIntro)
                .Replace("*itemName*", "{{|sacred vessel}}")
                .Replace("*deliveryTarget*", "{{|the salt shrine}}") + "? "
            + DummyHistoricStringExpander.ExpandString(ObjectHoly)
                .Replace("*It*", "It") + ". "
            + DummyHistoricStringExpander.ExpandString(ObjectWillInteract)
                .Replace("*verb*", "pray at")
                .Replace("*it*", "it")
                .Replace("*sacredThing*", "patience") + ". "
            + DummyHistoricStringExpander.ExpandString(ObjectHonor) + ". "
            + DummyHistoricStringExpander.ExpandString(ObjectPrompt) + "? "
            + DummyHistoricStringExpander.ExpandString(ObjectReward) + ".");
        AddNode(DummyHistoricStringExpander.ExpandString(StrangeIntro) + "\n\n"
            + DummyHistoricStringExpander.ExpandString(StrangeComeClose) + " "
            + DummyHistoricStringExpander.ExpandString(StrangePlan)
                .Replace("*plan*", "{{|scheme}}") + ". "
            + DummyHistoricStringExpander.ExpandString(StrangeGoTo)
                .Replace("*deliveryTarget*", "{{|the salt shrine}}")
                .Replace("*verb*", "pray at")
                .Replace("*itemName*", "{{|sacred vessel}}") + ". "
            + DummyHistoricStringExpander.ExpandString("No, I cannot tell you why") + ". "
            + DummyHistoricStringExpander.ExpandString(ObjectPrompt) + " "
            + DummyHistoricStringExpander.ExpandString(ObjectReward) + ".\n\n"
            + DummyHistoricStringExpander.ExpandString(StrangeTellNoOne)
                .Replace("*sacredThing*", "patience") + ".");
    }

    private static void AddNode(string text)
    {
        Nodes.Add(text);
    }
}
