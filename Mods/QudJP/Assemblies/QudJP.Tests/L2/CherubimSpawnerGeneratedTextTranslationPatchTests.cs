using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
public sealed class CherubimSpawnerGeneratedTextTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        DynamicTextObservability.ResetForTests();
    }

    [Test]
    public void DummyGameObjectGetPart_RejectsUnrelatedPartType()
    {
        var gameObject = new DummyCherubimGameObject();

        Assert.Throws<InvalidOperationException>(() => gameObject.GetPart<RulesDescription>());
    }

    [Test]
    public void BestowElementPostfix_TranslatesElementDisplayNameAndAddedRules_WhenPrependNameIsTrue()
    {
        var gameObject = new DummyCherubimGameObject();
        gameObject.Render.DisplayName = "ヒヒの智天使";

        CherubimSpawnerBestowElementTranslationPatch.Prefix(gameObject, out var state);
        DummyCherubimSpawnerTarget.BestowElement(gameObject, "glass", PrependName: true);
        CherubimSpawnerBestowElementTranslationPatch.Postfix(gameObject, "glass", true, state);

        var rules = (RulesDescription)gameObject.PartsList.Single();
        Assert.Multiple(() =>
        {
            Assert.That(gameObject.Render.DisplayName, Is.EqualTo("ガラスのヒヒの智天使"));
            Assert.That(gameObject.ResetNameCacheCallCount, Is.EqualTo(1));
            Assert.That(rules.Text, Does.Contain("ガラスの智天使"));
            Assert.That(rules.Text, Does.Contain("10%の確率で四肢を切断"));
            Assert.That(rules.Text, Does.Not.Contain("glass cherubim"));
        });
    }

    [Test]
    public void BestowElementPostfix_LeavesDisplayNameAndTranslatesAddedRules_WhenPrependNameIsFalse()
    {
        var gameObject = new DummyCherubimGameObject();
        gameObject.Render.DisplayName = "ヒヒの智天使";

        CherubimSpawnerBestowElementTranslationPatch.Prefix(gameObject, out var state);
        DummyCherubimSpawnerTarget.BestowElement(gameObject, "time", PrependName: false);
        CherubimSpawnerBestowElementTranslationPatch.Postfix(gameObject, "time", false, state);

        var rules = (RulesDescription)gameObject.PartsList.Single();
        Assert.Multiple(() =>
        {
            Assert.That(gameObject.Render.DisplayName, Is.EqualTo("ヒヒの智天使"));
            Assert.That(gameObject.ResetNameCacheCallCount, Is.EqualTo(0));
            Assert.That(rules.Text, Does.Contain("時の智天使"));
            Assert.That(rules.Text, Does.Contain("時間遁走 10"));
            Assert.That(rules.Text, Does.Not.Contain("time cherubim"));
        });
    }

    [Test]
    public void BestowElementPostfix_OnlyTranslatesRulesAddedAfterPrefixState()
    {
        var gameObject = new DummyCherubimGameObject();
        gameObject.Render.DisplayName = "ヒヒの智天使";
        var existingRules = gameObject.AddPart<RulesDescription>();
        existingRules.Text = "\nThis creature belongs to the caste of glass cherubim.";

        CherubimSpawnerBestowElementTranslationPatch.Prefix(gameObject, out var state);
        DummyCherubimSpawnerTarget.BestowElement(gameObject, "chance", PrependName: false);
        CherubimSpawnerBestowElementTranslationPatch.Postfix(gameObject, "chance", false, state);

        var addedRules = (RulesDescription)gameObject.PartsList[1];
        Assert.Multiple(() =>
        {
            Assert.That(existingRules.Text, Is.EqualTo("\nThis creature belongs to the caste of glass cherubim."));
            Assert.That(addedRules.Text, Does.Contain("混沌の智天使"));
            Assert.That(addedRules.Text, Does.Not.Contain("chaotic cherubim"));
            Assert.That(addedRules.Text, Does.Not.Contain("there's a 25% chance"));
        });
    }

    [Test]
    public void HexacherubimPostfix_TranslatesDisplayNameAndGeneratedBaseDescription()
    {
        var gameObject = new DummyCherubimGameObject();
        gameObject.Render.DisplayName = "ヒヒの智天使";
        gameObject.DescriptionPart.Short = "Processed English short description from getter path.";
        gameObject.DescriptionPart._Short =
            "Gallium veins press against the underside of =pronouns.possessive= crystalline skin and gleam warmly. " +
            "=pronouns.Possessive= body is perfect, and the whole of it is wet with amniotic slick; could " +
            "=pronouns.subjective= have just now peeled =pronouns.reflexive= off an oil canvas? " +
            "=verb:Were:afterpronoun= =pronouns.subjective= cast into the material realm by a dreaming, dripping brain? " +
            "Whatever the embryo, =pronouns.subjective= =verb:are:afterpronoun= now the archetypal ヒヒの智天使; " +
            "it's all there in impeccable simulacrum: 六枚の翼と六つの顔. Perfection is realized.";
        var ev = new DummyBeforeObjectCreatedEvent { ReplacementObject = gameObject };

        HexacherubimSpawnerHandleEventTranslationPatch.Postfix(ev);

        Assert.Multiple(() =>
        {
            Assert.That(gameObject.Render.DisplayName, Is.EqualTo("ヒヒの六智天使"));
            Assert.That(gameObject.DescriptionPart._Short, Does.Contain("ガリウムの脈"));
            Assert.That(gameObject.DescriptionPart._Short, Does.Contain("原型たるヒヒの六智天使"));
            Assert.That(gameObject.DescriptionPart._Short, Does.Contain("六枚の翼と六つの顔"));
            Assert.That(gameObject.DescriptionPart._Short, Does.Not.Contain("Gallium veins"));
            Assert.That(gameObject.DescriptionPart._Short, Does.Not.Contain("archetypal"));
            Assert.That(gameObject.DescriptionPart.Short, Is.EqualTo(gameObject.DescriptionPart._Short));
        });
    }

    [Test]
    public void HexacherubimPostfix_DoesNotDoubleTranslateAlreadyLocalizedHexacherubimName()
    {
        var gameObject = new DummyCherubimGameObject();
        gameObject.Render.DisplayName = "ヒヒの六智天使";
        gameObject.DescriptionPart._Short =
            "Gallium veins press against the underside of =pronouns.possessive= crystalline skin and gleam warmly. " +
            "=pronouns.Possessive= body is perfect, and the whole of it is wet with amniotic slick; could " +
            "=pronouns.subjective= have just now peeled =pronouns.reflexive= off an oil canvas? " +
            "=verb:Were:afterpronoun= =pronouns.subjective= cast into the material realm by a dreaming, dripping brain? " +
            "Whatever the embryo, =pronouns.subjective= =verb:are:afterpronoun= now the archetypal ヒヒの六智天使; " +
            "it's all there in impeccable simulacrum: 六枚の翼と六つの顔. Perfection is realized.";
        var ev = new DummyBeforeObjectCreatedEvent { ReplacementObject = gameObject };

        HexacherubimSpawnerHandleEventTranslationPatch.Postfix(ev);

        Assert.Multiple(() =>
        {
            Assert.That(gameObject.Render.DisplayName, Is.EqualTo("ヒヒの六智天使"));
            Assert.That(gameObject.DescriptionPart._Short, Does.Contain("原型たるヒヒの六智天使"));
            Assert.That(gameObject.DescriptionPart._Short, Does.Not.Contain("六六智天使"));
        });
    }

    [Test]
    public void CherubimHandleEventPostfix_TranslatesMechanicalPrefixAddedAfterBestowElement()
    {
        var gameObject = new DummyCherubimGameObject();
        gameObject.Render.DisplayName = "mechanical ガラスのヒヒの智天使";
        var ev = new DummyBeforeObjectCreatedEvent { ReplacementObject = gameObject };

        CherubimSpawnerHandleEventTranslationPatch.Postfix(ev);

        Assert.Multiple(() =>
        {
            Assert.That(gameObject.Render.DisplayName, Is.EqualTo("機械仕掛けのガラスのヒヒの智天使"));
            Assert.That(gameObject.ResetNameCacheCallCount, Is.EqualTo(1));
        });
    }
}
