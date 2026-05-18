using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class DynamicQuestConstructorConversationTextTranslatorTests
{
    [TestCase("Will you do it?", "引き受けてくれるか？")]
    [TestCase("Will you?", "引き受けてくれるか？")]
    [TestCase("What say you?", "どうだ？")]
    [TestCase("What do you think?", "どう思う？")]
    [TestCase("Will you find it for us?", "われらのために見つけてくれるか？")]
    [TestCase("Would you be willing to locate it?", "それを探し出してくれないか？")]
    [TestCase("Would you", "引き受けてくれるか")]
    [TestCase("What do you say", "どうだ")]
    [TestCase("We will reward your services", "あなたの奉仕には報いる")]
    [TestCase("We will reward your labor", "あなたの労には報いる")]
    [TestCase("We will pay you for your assistance", "あなたの助力には代価を支払う")]
    [TestCase("We will compensate you for your work", "あなたの働きには報酬を出す")]
    [TestCase("I will reward your services", "あなたの奉仕には報いる")]
    [TestCase("I have a task for you", "あなたに頼みたい仕事がある")]
    [TestCase("I have a errand that needs doing", "やるべき用事がある")]
    [TestCase("I could use you for a project", "計画であなたの力を借りたい")]
    [TestCase("There's a charge you could do for me", "あなたにやってもらえる任務がある")]
    [TestCase("Come, close!", "近くへ、友よ")]
    [TestCase("Live and drink, friend.", "生きて飲め、友よ")]
    [TestCase("Adventurer!", "冒険者よ。")]
    [TestCase("Traveler!", "旅人よ。")]
    [TestCase("Thank the azure wheel that you are here, friend.", "来てくれてありがたい、友よ")]
    [TestCase("Come, close! My kin used to spend our days *Activity*", "かつて私の同胞は*Activity*に日々を費やしていた")]
    [TestCase("Friend, you know, once my clan would spend all our time *Activity*", "かつて私の同胞は*Activity*に日々を費やしていた")]
    [TestCase("But after learning the *sanctityOfSacredThing*, we changed our ways and composed new rituals", "*sanctityOfSacredThing*を知ってから、われらは習わしを改め、新たな儀式を作った")]
    [TestCase("Unfortunately", "残念ながら")]
    [TestCase("May brain rust take us", "なんという災いか")]
    [TestCase("Recently I learned that there's *itemName.an* nearby in *itemLocation*", "*itemLocation*の近くに*itemName.an*があると知った")]
    [TestCase("The other day *villagerName* gathered that *itemName.an* *itemName.have* arrived at *itemLocation*", "*villagerName*が*itemName.an*が*itemLocation*に届いたと知った")]
    [TestCase("Just a while ago I visited *itemLocation* and saw *itemName.an*", "*itemLocation*で*itemName.an*を見かけた")]
    [TestCase("A short while ago I lost my *itemName* at *itemLocation*", "*itemLocation*で*itemName*を失くした")]
    [TestCase("*NeedsItemFor*, I must get a hold of *it*", "*NeedsItemFor*のために、それが必要だ")]
    [TestCase("Traveling pilgrims came to our village the other day", "先日、旅人たちがわれらの村に来た")]
    [TestCase("While *GuestActivity*, they spoke of a fascinating place, *site*", "彼らは*GuestActivity*の間、*site*という興味深い場所について話した")]
    [TestCase("Adventurer, we've been poring over our records and we learned of a nearby location forgotten to our people, *siteInitLower*", "冒険者よ、われらは記録を調べ、民に忘れられていた近くの場所、*siteInitLower*を見つけた")]
    [TestCase("A shrine to ", "聖所：")]
    [TestCase("*looks around suspiciously and leans in*", "*慎重にあたりを見回す*")]
    [TestCase("My *plan* has nearly come to fruition. There is but one more step", "私の*plan*はもうすぐ成就する。残る手順はあと一つだ")]
    [TestCase("I have a secret *plan* I'm setting into motion", "私は秘密の*plan*を進めている")]
    [TestCase("I need someone to go to *deliveryTarget* and *verb* the *itemName* there", "*deliveryTarget*へ行き、そこの*itemName*に関わってくれる者が必要だ")]
    [TestCase("By *sacredThing*, speak to no one about this", "*sacredThing*にかけて、このことは誰にも話すな")]
    [TestCase("This conversation never happened", "このことは誰にも話すな")]
    [TestCase("It would be a great boon to the technological prospects of our village", "それはわれらの村の技術の見通しにとって大きな恩恵となる")]
    [TestCase("What mysteries might this place contain", "この場所にはどんな謎が収められているのか")]
    [TestCase("Would you be willing to recover *it* for us?", "われらのためにそれを取り戻してくれるか？")]
    [TestCase("Would you seek *it* out and return *it* to us?", "それを探し出し、われらのもとへ返してくれるか？")]
    [TestCase("We need someone to recover *it*, adventurer. Will you do it?", "それを取り戻してくれる者が必要だ、冒険者よ。引き受けてくれるか？")]
    [TestCase("We hear *it* *has* been taken to *deliveryTarget*", "*deliveryTarget*に運ばれたと聞いている")]
    [TestCase("I hear *it* *has* been taken to *deliveryTarget*", "*deliveryTarget*に運ばれたと聞いている")]
    [TestCase("My clan tell me *it* *has* been taken to *deliveryTarget*", "私の氏族によれば、*deliveryTarget*に運ばれたそうだ")]
    [TestCase("Adventurer, have you heard of the *itemName* at *deliveryTarget*", "*deliveryTarget*にある*itemName*のことを聞いたことはあるか、冒険者よ")]
    [TestCase("*It* is a sacred shrine to us", "それはわれらにとって神聖な祠だ")]
    [TestCase("*It* is a cursed horror to *sacredThing* and everything else we hold dear", "それは*sacredThing*とわれらが大切にするすべてにとって忌まわしい恐怖だ")]
    [TestCase("Often we make pilgrimages to *verb* *it* and contemplate *sacredThing*", "われらはしばしばそこへ巡礼し、*sacredThing*について思索する")]
    [TestCase("It would honor us if you would do the same", "あなたも同じことをしてくれれば、われらの誉れとなる")]
    [TestCase("It would be a blessing if you would do it too", "あなたもそうしてくれれば、それは祝福となる")]
    [TestCase("It would honor us greatly if you *verb* *it*", "あなたがそれを冒涜してくれれば、われらにとって大きな誉れとなる")]
    [TestCase("It would be a boon if you *verb* it", "あなたがそれを冒涜してくれれば、それは恩恵となる")]
    [TestCase("*name* lost our *itemName*", "*name*がわれらの*itemName*を失った")]
    [TestCase("our *itemName* *were* stolen from us", "われらの*itemName*は盗まれた")]
    [TestCase("misfortune has separated us from our *itemName*", "不運により、われらは*itemName*から引き離された")]
    [TestCase("If you recover *it* for me, I'll reward your services", "それを取り戻してくれれば、あなたの奉仕には報いる")]
    [TestCase("Fetch *it* and I'll pay you for your assistance", "それを取り戻してくれれば、あなたの助力には代価を支払う")]
    [TestCase("I'm willing to compensate you for your work if you procure *it* for me", "それを取り戻してくれれば、あなたの働きには報酬を出す")]
    [TestCase("If you locate it for us, we will compensate you for your labor", "それを見つけてくれれば、あなたの労には報酬を出す")]
    [TestCase("We hear that it's located next to *landmark*", "*landmark*の隣にあると聞いている")]
    [TestCase("We hear it's located somewhere between *min* and *max* parasangs *direction* of *landmark*", "*landmark*の*direction*、*min*から*max*パラサング離れたどこかにあると聞いている")]
    [TestCase("We hear you can find it by following the *path* that passes through *landmark* *direction*", "*landmark*を*direction*に通る*path*をたどれば見つかると聞いている")]
    public void TryTranslate_TranslatesConstructorSafePromptFrames(string source, string expected)
    {
        var translated = DynamicQuestConstructorConversationTextTranslator.TryTranslate(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo(expected));
        });
    }

    [Test]
    public void TryTranslate_PreservesWholeSourceColorWrapper()
    {
        var translated = DynamicQuestConstructorConversationTextTranslator.TryTranslate("{{G|Will you?}}", out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo("{{G|引き受けてくれるか？}}"));
        });
    }

    [Test]
    public void TryTranslate_StripsDirectMarkerWithoutRetranslating()
    {
        var source = MessageFrameTranslator.DirectTranslationMarker + "Will you?";

        var translated = DynamicQuestConstructorConversationTextTranslator.TryTranslate(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(result, Is.EqualTo("Will you?"));
        });
    }

    [TestCase("")]
    [TestCase(null)]
    [TestCase("I know a place but cannot tell you where it is.")]
    [TestCase("I know a place for {{Y|cooking}}.")]
    public void TryTranslate_LeavesUnknownOrEmptyText(string? source)
    {
        var translated = DynamicQuestConstructorConversationTextTranslator.TryTranslate(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(result, Is.EqualTo(source ?? string.Empty));
        });
    }

    [Test]
    public void TryTranslate_NormalizesKnownCompositeCapturesOnlyAfterOwnedFrameTranslation()
    {
        const string source = "やるべき用事がある. {{Y|cooking}}のために、それが必要だ.";

        var translated = DynamicQuestConstructorConversationTextTranslator.TryTranslate(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo("やるべき用事がある。{{Y|料理}}のために、それが必要だ。"));
        });
    }

    [Test]
    public void TryTranslate_LeavesMixedUnknownCompositeUnchanged()
    {
        const string source = "未知の日本語文. {{Y|cooking}} is still raw.";

        var translated = DynamicQuestConstructorConversationTextTranslator.TryTranslate(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(result, Is.EqualTo(source));
        });
    }
}
