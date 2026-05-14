using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class PsychicGlimmerTranslationPatchTests
{
    private const string WatchedSource =
        "{{K|You are being watched.\n\nIt's a familiar feeling. When someone has watched you in the past, when it's light that's betrayed your presence, you made a friend of the darkness. You pulled your hat brim low over your eyes. You stepped behind the cover of a thatched wall. But those who watch you now watch in spite of such simple obstructions. Their sight isn't mediated by the rays of a gleaming star or torch but by something much older. If there are ways to conceal yourself from these seeing eyes, if there are new kinds of darknesses to befriend, you know nothing of them.}}";

    private const string WatchedExpected =
        "{{K|あなたは見られている。\n\nそれは馴染みのある感覚だ。過去に誰かに見られたとき、光があなたの存在を裏切ったとき、あなたは暗闇を友とした。帽子のつばを目深に下ろした。茅葺きの壁の陰に身を潜めた。だが今あなたを見ている者たちは、そんな単純な遮蔽など意に介さずに見ている。その視線は輝く星や松明の光線を介したものではなく、もっと古い何かによるものだ。この見つめる目から自分自身を隠す方法があるのか、新たに友とすべき暗闇があるのか、あなたには何もわからない。}}";

    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        DummyPopupShow.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        DummyPopupShow.Reset();
        MessageFrameTranslator.ResetForTests();
        DynamicTextObservability.ResetForTests();
    }

    [Test]
    public void Update_TranslatesWatchedPopup_WhenOwnerPatched()
    {
        WithPatchedOwner(() =>
        {
            new DummyPsychicGlimmerProducer
            {
                MessageToShow = WatchedSource,
            }.Update(new DummyGameObject());

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(WatchedExpected));
                Assert.That(HitCount("Watched"), Is.EqualTo(1));
            });
        });
    }

    [TestCase(
        "{{K|You've discovered a way to conceal yourself. For now.}}",
        "{{K|あなたは自分自身を隠す方法を見つけた。今のところは。}}",
        "ConcealSelf")]
    [TestCase(
        "{{K|You've discovered a way to conceal yourself from extradimensional watchers. For now.}}",
        "{{K|あなたは自分自身を超次元の観測者たちから隠す方法を見つけた。今のところは。}}",
        "ConcealFromWatchers")]
    public void Update_TranslatesConcealmentPopups_WhenOwnerPatched(
        string source,
        string expected,
        string detail)
    {
        WithPatchedOwner(() =>
        {
            new DummyPsychicGlimmerProducer
            {
                MessageToShow = source,
            }.Update(new DummyGameObject());

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
                Assert.That(HitCount(detail), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void Update_DoesNotClaimPopupOnlyTraffic_WhenOwnerAbsent()
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOnly(
            () => DummyPopupShow.Show(WatchedSource));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(WatchedSource));
            Assert.That(HitCount("Watched"), Is.Zero);
        });
    }

    [Test]
    public void Update_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        WithPatchedOwner(() =>
        {
            new DummyPsychicGlimmerProducer
            {
                MessageToShow = MessageFrameTranslator.MarkDirectTranslation(WatchedSource),
            }.Update(new DummyGameObject());

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(WatchedSource));
                Assert.That(HitCount("Watched"), Is.Zero);
            });
        });
    }

    [TestCase("")]
    [TestCase("{{K|What you understood to be the psychic sea was only a pond. There are other watchers now, countless in number, beyond the gulf of materiality. Points of light glimmer in all directions, but what are directions on a space that cannot be ordered? All you know now is of an aether vaster than the very mathematics that describe it. And you are not nor will you ever be again alone.}}")]
    public void Update_DoesNotClaimFixedOrEmptyPopups_WhenOwnerPatched(string source)
    {
        WithPatchedOwner(() =>
        {
            new DummyPsychicGlimmerProducer
            {
                MessageToShow = source,
            }.Update(new DummyGameObject());

            Assert.Multiple(() =>
            {
                Assert.That(HitCount("Watched"), Is.Zero);
                Assert.That(HitCount("ConcealSelf"), Is.Zero);
                Assert.That(HitCount("ConcealFromWatchers"), Is.Zero);
            });
        });
    }

    private static void WithPatchedOwner(Action action)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(PsychicGlimmerTranslationPatch),
            RequireOwnerMethod(),
            action);
    }

    private static MethodInfo RequireOwnerMethod()
    {
        return AccessTools.Method(
                   typeof(DummyPsychicGlimmerProducer),
                   nameof(DummyPsychicGlimmerProducer.Update),
                   [typeof(DummyGameObject)])
               ?? throw new MissingMethodException(
                   typeof(DummyPsychicGlimmerProducer).FullName,
                   nameof(DummyPsychicGlimmerProducer.Update));
    }

    private static int HitCount(string detail)
    {
        return OwnerPopupRouteTestHarness.RouteHitCount(typeof(PsychicGlimmerTranslationPatch), detail);
    }

    private sealed class DummyPsychicGlimmerProducer
    {
        public string MessageToShow { get; set; } = string.Empty;

        public void Update(DummyGameObject who)
        {
            _ = who.GetPsychicGlimmer();
            DummyPopupShow.Show(MessageToShow);
        }
    }

    private sealed class DummyGameObject
    {
        private readonly int psychicGlimmer;

        public DummyGameObject(int psychicGlimmer = 20)
        {
            this.psychicGlimmer = psychicGlimmer;
        }

        public int GetPsychicGlimmer()
        {
            return psychicGlimmer;
        }
    }
}
