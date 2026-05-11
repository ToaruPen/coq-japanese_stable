using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class CookingRuntimeTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DummyPopupShow.Reset();
        DummyMessageQueue.Reset();
    }

    [TestCase(
        "You eat the meal. It's tastier than usual.\n\n{{W|+5 hit points for the rest of the day}}",
        "食事を食べた。いつもよりおいしい。\n\n{{W|一日中、HP+5}}")]
    [TestCase(
        "You eat the meal. It's tastier than usual.\n\n{{W|+1 MA for the rest of the day}}",
        "食事を食べた。いつもよりおいしい。\n\n{{W|一日中、MA+1}}")]
    [TestCase(
        "You eat the meal. It's tastier than usual.\n\n{{W|+5% XP gained for the rest of the day}}",
        "食事を食べた。いつもよりおいしい。\n\n{{W|一日中、獲得XP+5%}}")]
    [TestCase(
        "You eat the meal. It's tastier than usual.\n\n{{W|1 Strength for the rest of the day}}",
        "食事を食べた。いつもよりおいしい。\n\n{{W|一日中、筋力+1}}")]
    public void BasicCookingPopup_TranslatesRuntimeWellFedMessages_WhenOwnerPatched(string source, string expected)
    {
        AssertPopupMessage(source, expected);
    }

    [TestCase("You feel an uncomfortable pressure across the length of your body.", "全身に不快な圧迫感を覚える。")]
    [TestCase("You gained the mutation {{C|Crystallinity}}!", "変異{{C|結晶性}}を得た！")]
    [TestCase("Feelers rip through your scalp and shudder with curiosity.", "触角が頭皮を裂いて生え、好奇心に震えた。")]
    [TestCase("Your genome has already undergone this transformation.", "あなたのゲノムはすでにこの変化を経ている。")]
    [TestCase("You bounce.", "あなたは跳ねた。")]
    [TestCase("True kin cannot digest this meal.", "トゥルーキンはこの食事を消化できない。")]
    [TestCase("Only mutants can digest this meal.", "この食事を消化できるのはミュータントだけだ。")]
    [TestCase("Tam shares the recipe for {{W|Glowfish Stew}}!", "Tamが{{W|Glowfish Stew}}のレシピを教えてくれた！")]
    [TestCase(
        "You start to metabolize the meal, gaining the following effect for the rest of the day:\n\n{{W|+1 to hit}}",
        "食事の代謝が始まり、一日中次の効果を得る:\n\n{{W|命中+1}}")]
    public void SpecialCookingPopup_TranslatesRuntimeMessages_WhenOwnerPatched(string source, string expected)
    {
        AssertPopupMessage(source, expected);
    }

    [TestCase("You reflect 3 damage back at {{R|snapjaw}}&y.", "3ダメージを{{R|snapjaw}}へ反射した。")]
    [TestCase("{{G|snapjaw}}&y reflects 4 damage back at you.", "{{G|snapjaw}}は4ダメージをあなたへ反射した。")]
    [TestCase("Fate intervenes and you deal no damage to {{R|glowfish}}&y.", "運命が介入し、あなたは{{R|glowfish}}にダメージを与えられなかった。")]
    [TestCase("Your phase remains stable.", "あなたの位相は安定したままだ。")]
    public void CookingQueuedMessage_TranslatesRuntimeMessages_WhenOwnerPatched(string source, string expected)
    {
        AssertQueuedMessage(source, null, expected);
    }

    [Test]
    public void CookingPopup_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);

            DummyPopupShow.Show("You bounce.");

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("You bounce."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void CookingRuntime_DoesNotRetranslateDirectMarkedMessages_WhenOwnerPatched()
    {
        AssertPopupMessage(MessageFrameTranslator.MarkDirectTranslation("You bounce."), "You bounce.");
        AssertQueuedMessage(MessageFrameTranslator.MarkDirectTranslation("Your phase remains stable."), null, "Your phase remains stable.");
    }

    [Test]
    public void CookingRuntime_LeavesEmptyMessagesUnchanged_WhenOwnerPatched()
    {
        AssertPopupMessage(string.Empty, string.Empty);
        AssertQueuedMessage(string.Empty, null, string.Empty);
    }

    private static void AssertPopupMessage(string source, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyCookingRuntimeTarget), nameof(DummyCookingRuntimeTarget.ApplyPopupEffect), typeof(DummyGameObject)));

            var target = new DummyCookingRuntimeTarget
            {
                PopupMessageToSend = source,
            };

            target.ApplyPopupEffect(new DummyGameObject());

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertQueuedMessage(string source, string? color, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyCookingRuntimeTarget), nameof(DummyCookingRuntimeTarget.FireQueuedEffect), typeof(DummyGameEvent)));

            var target = new DummyCookingRuntimeTarget
            {
                MessageToSend = source,
                ColorToSend = color,
            };

            _ = target.FireQueuedEffect(new DummyGameEvent());

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void PatchPopupShow(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(
                typeof(DummyPopupShow),
                nameof(DummyPopupShow.Show),
                typeof(string),
                typeof(string),
                typeof(string),
                typeof(bool),
                typeof(bool),
                typeof(bool),
                typeof(bool)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));
    }

    private static void PatchQueue(Harmony harmony)
    {
        var original = RequireMethod(typeof(DummyMessageQueue), nameof(DummyMessageQueue.AddPlayerMessage), typeof(string), typeof(string), typeof(bool));
        PatchQueuePrefix(harmony, original, nameof(MessageQueueTranslationPatch.PrefixPhysicsEnterCellPassBy));
        PatchQueuePrefix(harmony, original, nameof(MessageQueueTranslationPatch.PrefixZoneManagerSetActiveZone));
        PatchQueuePrefix(harmony, original, nameof(MessageQueueTranslationPatch.PrefixCombatAndLog));
        PatchQueuePrefix(harmony, original, nameof(MessageQueueTranslationPatch.PrefixMessageLog));
    }

    private static void PatchQueuePrefix(Harmony harmony, MethodInfo original, string prefixName)
    {
        harmony.Patch(
            original: original,
            prefix: new HarmonyMethod(RequireMethod(
                typeof(MessageQueueTranslationPatch),
                prefixName,
                typeof(string).MakeByRefType(),
                typeof(string),
                typeof(bool))));
    }

    private static void PatchOwner(Harmony harmony, MethodInfo original)
    {
        harmony.Patch(
            original: original,
            prefix: new HarmonyMethod(RequireMethod(typeof(CookingRuntimeTranslationPatch), nameof(CookingRuntimeTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(typeof(CookingRuntimeTranslationPatch), nameof(CookingRuntimeTranslationPatch.Finalizer), typeof(Exception))));
    }

    private static MethodInfo RequireMethod(Type type, string name, params Type[] parameters)
    {
        if (parameters.Length == 0)
        {
            var methodByName = type.GetMethod(
                name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
            Assert.That(methodByName, Is.Not.Null, $"{type.FullName}.{name} not found");
            return methodByName!;
        }

        var method = type.GetMethod(
            name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance,
            binder: null,
            types: parameters,
            modifiers: null);
        Assert.That(method, Is.Not.Null, $"{type.FullName}.{name} not found");
        return method!;
    }

    private static string CreateHarmonyId()
    {
        return $"qudjp.tests.{Guid.NewGuid():N}";
    }
}
