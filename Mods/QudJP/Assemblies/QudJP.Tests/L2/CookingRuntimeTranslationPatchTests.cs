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
        "You gorge on the succulent meat. It's tastier than usual.\n\n{{W|+5 hit points for the rest of the day}}",
        "瑞々しい肉を貪った。いつもよりおいしい。\n\n{{W|一日中、HP+5}}")]
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
    [TestCase("You gained the mutation {{w|Bilge Sphincter}}!", "変異{{w|ビルジスフィンクター}}を得た！")]
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
    [TestCase("{{G|snapjaw}}&y reflects 5 damage back at {{R|glowfish}}&y.", "{{G|snapjaw}}は5ダメージを{{R|glowfish}}へ反射した。")]
    [TestCase("Fate intervenes and you deal no damage to {{R|glowfish}}&y.", "運命が介入し、あなたは{{R|glowfish}}にダメージを与えられなかった。")]
    [TestCase("Your phase remains stable.", "あなたの位相は安定したままだ。")]
    public void CookingQueuedMessage_TranslatesRuntimeMessages_WhenOwnerPatched(string source, string expected)
    {
        AssertQueuedMessage(source, null, expected);
    }

    [Test]
    public void CookingQueuedMessage_StripsPatternControlHeader_ForReflectedDamage()
    {
        AssertQueuedMessage(
            "\u0002reflect\u001F9\u001F18\u001F\u0003The 石英のヒヒ reflects 1 damage back at you.",
            null,
            "石英のヒヒは1ダメージをあなたへ反射した。");
    }

    [TestCase("You phase out.", "あなたは位相が外れた。")]
    [TestCase("It phases out.", "それは位相が外れた。")]
    [TestCase("They phase out.", "それらは位相が外れた。")]
    [TestCase("He phases out.", "彼は位相が外れた。")]
    [TestCase("She phases out.", "彼女は位相が外れた。")]
    [TestCase("You perform an act of nimble violence.", "あなたは俊敏な暴力行為を行った。")]
    [TestCase("It performs an act of brutal violence.", "それは残忍な暴力行為を行った。")]
    [TestCase("Your wounds heal significantly.", "あなたの傷が大きく癒えた。")]
    [TestCase("His wounds heal a bit.", "彼の傷が少し癒えた。")]
    [TestCase("Her wounds heal a bit.", "彼女の傷が少し癒えた。")]
    [TestCase("Their muscles bulge.", "それらの筋肉が膨れ上がった。")]
    [TestCase("Plants burgeon around them!", "それらの周囲に植物が芽吹いた！")]
    [TestCase("Plants burgeon around him!", "彼の周囲に植物が芽吹いた！")]
    [TestCase("Plants burgeon around her!", "彼女の周囲に植物が芽吹いた！")]
    [TestCase("He intimidates everyone around him.", "彼は周囲の全員を威圧した。")]
    [TestCase("She intimidates everyone around her.", "彼女は周囲の全員を威圧した。")]
    [TestCase("You stop bleeding.", "あなたは出血が止まった。")]
    [TestCase("You teleport.", "あなたはテレポートした。")]
    [TestCase("It teleports.", "それはテレポートした。")]
    [TestCase("They teleport.", "それらはテレポートした。")]
    [TestCase("You teleport all creatures surrounding you.", "あなたは周囲のすべてのクリーチャーをテレポートさせた。")]
    [TestCase("It teleports all creatures surrounding it.", "それは周囲のすべてのクリーチャーをテレポートさせた。")]
    [TestCase("They teleport all creatures surrounding them.", "それらは周囲のすべてのクリーチャーをテレポートさせた。")]
    [TestCase("He teleports all creatures surrounding him.", "彼は周囲のすべてのクリーチャーをテレポートさせた。")]
    [TestCase("She teleports all creatures surrounding her.", "彼女は周囲のすべてのクリーチャーをテレポートさせた。")]
    [TestCase("You don't thirst.", "あなたは喉が渇かなくなった。")]
    [TestCase("It don't thirst.", "それは喉が渇かなくなった。")]
    [TestCase("They don't thirst.", "それらは喉が渇かなくなった。")]
    [TestCase("You don't thirst for the next 12 hours.", "あなたは次の12時間喉が渇かなくなった。")]
    public void ProceduralCookingTrigger_TranslatesResolvedNotifications_WhenOwnerPatched(string source, string expected)
    {
        AssertQueuedMessage(nameof(DummyCookingRuntimeTarget.Trigger), source, null, expected);
    }

    [Test]
    public void CookingQueuedMessage_TranslatesModBlinkEscapeFateIntervenes_WhenOwnerPatched()
    {
        AssertQueuedMessage(
            nameof(DummyCookingRuntimeTarget.CheckBlinkEscape),
            "Fate intervenes and you deal no damage to {{R|glowfish}}&y.",
            "r",
            "運命が介入し、あなたは{{R|glowfish}}にダメージを与えられなかった。");
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
    public void ProceduralCookingTrigger_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage("You phase out.", null, Capitalize: false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("You phase out."));
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
        AssertQueuedMessage(
            nameof(DummyCookingRuntimeTarget.Trigger),
            MessageFrameTranslator.MarkDirectTranslation("You phase out."),
            null,
            "You phase out.");
    }

    [Test]
    public void CookingQueuedMessage_PreservesMessageColor_WhenOwnerPatched()
    {
        AssertQueuedMessage("You reflect 3 damage back at {{R|snapjaw}}&y.", "G", "3ダメージを{{R|snapjaw}}へ反射した。");
    }

    [Test]
    public void CookingRuntime_LeavesEmptyMessagesUnchanged_WhenOwnerPatched()
    {
        AssertPopupMessage(string.Empty, string.Empty);
        AssertQueuedMessage(string.Empty, null, string.Empty);
        AssertQueuedMessage(nameof(DummyCookingRuntimeTarget.Trigger), string.Empty, null, string.Empty);
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
        AssertQueuedMessage(
            nameof(DummyCookingRuntimeTarget.FireQueuedEffect),
            source,
            color,
            expected);
    }

    private static void AssertQueuedMessage(string ownerMethodName, string source, string? color, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyCookingRuntimeTarget), ownerMethodName, OwnerParameterTypes(ownerMethodName)));

            var target = new DummyCookingRuntimeTarget
            {
                MessageToSend = source,
                ColorToSend = color,
            };

            InvokeQueuedOwnerMethod(ownerMethodName, target);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
            Assert.That(DummyMessageQueue.LastColor, Is.EqualTo(color));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static Type[] OwnerParameterTypes(string ownerMethodName)
    {
        return ownerMethodName switch
        {
            nameof(DummyCookingRuntimeTarget.FireQueuedEffect) => new[] { typeof(DummyGameEvent) },
            _ => Type.EmptyTypes,
        };
    }

    private static void InvokeQueuedOwnerMethod(string ownerMethodName, DummyCookingRuntimeTarget target)
    {
        switch (ownerMethodName)
        {
            case nameof(DummyCookingRuntimeTarget.FireQueuedEffect):
                _ = target.FireQueuedEffect(new DummyGameEvent());
                break;
            case nameof(DummyCookingRuntimeTarget.CheckBlinkEscape):
                _ = target.CheckBlinkEscape();
                break;
            case nameof(DummyCookingRuntimeTarget.Trigger):
                target.Trigger();
                break;
            default:
                throw new InvalidOperationException($"Unsupported cooking runtime owner method: {ownerMethodName}");
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
