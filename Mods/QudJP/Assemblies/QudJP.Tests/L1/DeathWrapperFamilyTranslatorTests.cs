using System.Text;
using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class DeathWrapperFamilyTranslatorTests
{
    private string tempDirectory = null!;
    private string dictionaryDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-death-wrapper-l1", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        dictionaryDirectory = Path.Combine(tempDirectory, "dict");
        Directory.CreateDirectory(dictionaryDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(dictionaryDirectory);
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();

        WriteDictionary(CommonEntries());
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [TestCaseSource(nameof(SupportedDeathCases))]
    public void TryTranslateMessage_TranslatesSupportedDeathCases(string source, string expectedBody)
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                DeathWrapperFamilyTranslator.TryTranslateMessage(source, spans: null, out var bareTranslated),
                Is.True,
                source);
            Assert.That(bareTranslated, Is.EqualTo(expectedBody));

            var wrappedSource = "You died.\n\n" + source;
            Assert.That(
                DeathWrapperFamilyTranslator.TryTranslateMessage(wrappedSource, spans: null, out var wrappedTranslated),
                Is.True,
                wrappedSource);
            Assert.That(wrappedTranslated, Is.EqualTo("あなたは死んだ。\n\n" + expectedBody));
        });
    }

    [TestCaseSource(nameof(PopupDeathCases))]
    public void TryTranslatePopup_TranslatesRepresentativeWrappedCases(string source, string expectedBody)
    {
        var wrappedSource = "You died.\n\n" + source;

        var translated = DeathWrapperFamilyTranslator.TryTranslatePopup(
            wrappedSource,
            spans: null,
            nameof(PopupTranslationPatch),
            out var popupTranslated);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(popupTranslated, Is.EqualTo("あなたは死んだ。\n\n" + expectedBody));
        });
    }

    [Test]
    public void TryTranslateMessage_KeepsAlreadyLocalizedKillerName()
    {
        const string source = "You were killed by 監視官イラメ.";

        var translated = DeathWrapperFamilyTranslator.TryTranslateMessage(source, spans: null, out var messageTranslated);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(messageTranslated, Is.EqualTo("監視官イラメに殺された。"));
        });
    }

    [Test]
    public void TryTranslateMessage_PreservesMarkupWrappedEnglishModifierInKillerName()
    {
        WriteDictionary(
            CommonEntries().Concat(
            [
                ("dromad merchant", "ドロマド商人"),
                ("bloody", "{{r|血まみれの}}"),
                ("[sitting]", "[座っている]"),
            ]));

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(
            "You were killed by {{r|bloody}} Tam, dromad merchant [sitting].");

        var translated = DeathWrapperFamilyTranslator.TryTranslateMessage(stripped, spans, out var messageTranslated);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(messageTranslated, Is.EqualTo("{{r|血まみれの}}Tam、ドロマド商人 [座っている]に殺された。"));
        });
    }

    [Test]
    public void TryTranslateMessage_PreservesOuterKillerWrapper_WhenTranslationInjectsMarkup()
    {
        WriteDictionary(
            CommonEntries().Concat(
            [
                ("dromad merchant", "ドロマド商人"),
                ("bloody", "{{r|血まみれの}}"),
                ("[sitting]", "[座っている]"),
            ]));

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(
            "You were killed by {{C|{{r|bloody}} Tam, dromad merchant [sitting]}}.");

        var translated = DeathWrapperFamilyTranslator.TryTranslateMessage(stripped, spans, out var messageTranslated);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(messageTranslated, Is.EqualTo("{{C|{{r|血まみれの}}Tam、ドロマド商人 [座っている]}}に殺された。"));
        });
    }

    [Test]
    public void TryTranslateMessage_PreservesAmpersandWholeKillerWrapper_WhenTranslationInjectsMarkup()
    {
        WriteDictionary(
            CommonEntries().Concat(
            [
                ("dromad merchant", "ドロマド商人"),
                ("bloody", "{{r|血まみれの}}"),
                ("[sitting]", "[座っている]"),
            ]));

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(
            "You were killed by &G{{r|bloody}} Tam, dromad merchant [sitting]&y.");

        var translated = DeathWrapperFamilyTranslator.TryTranslateMessage(stripped, spans, out var messageTranslated);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(messageTranslated, Is.EqualTo("&G{{r|血まみれの}}Tam、ドロマド商人 [座っている]&yに殺された。"));
        });
    }

    [Test]
    public void TryTranslateMessage_DoesNotReapplySourceKillerMarkup_WhenTranslatedKillerOwnsMarkup()
    {
        WriteDictionary(
            CommonEntries().Concat(
            [
                ("bloody Tam, dromad merchant [sitting]", "{{r|血まみれの}}Tam、ドロマド商人 [座っている]"),
            ]));

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(
            "You were killed by {{r|bloody}} Tam, dromad merchant [sitting].");

        var translated = DeathWrapperFamilyTranslator.TryTranslateMessage(stripped, spans, out var messageTranslated);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(
                messageTranslated,
                Is.EqualTo("{{r|血まみれの}}Tam、ドロマド商人 [座っている]に殺された。"));
            Assert.That(messageTranslated, Does.Not.Contain("{{r|{{r|"));
            Assert.That(messageTranslated, Does.Not.Match("血ま.*}}.*みれ"));
            Assert.That(messageTranslated, Does.Not.Match("\\[座ってい}}る\\]"));
        });
    }

    [Test]
    public void TryTranslateMessage_PreservesSourceWholeKillerWrapper_WhenTranslatedKillerOwnsMarkup()
    {
        WriteDictionary(
            CommonEntries().Concat(
            [
                ("bloody Tam", "{{r|血まみれの}}Tam"),
            ]));

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(
            "You were killed by {{C|bloody Tam}}.");

        var translated = DeathWrapperFamilyTranslator.TryTranslateMessage(stripped, spans, out var messageTranslated);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(messageTranslated, Is.EqualTo("{{C|{{r|血まみれの}}Tam}}に殺された。"));
        });
    }

    [Test]
    public void TryTranslatePopup_PreservesTerminalEmptyWrapperInLocalizedKillerName()
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(
            "You died.\n\nYou were killed by タム、ドロマド商団 [座っている]{{B|}}.");

        var translated = DeathWrapperFamilyTranslator.TryTranslatePopup(
            stripped,
            spans,
            nameof(PopupShowSpaceTranslationPatch),
            out var popupTranslated);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(
                popupTranslated,
                Is.EqualTo("あなたは死んだ。\n\nタム、ドロマド商団 [座っている]{{B|}}に殺された。"));
        });
    }

    [TestCase("You were killed by a snapjaw.", "スナップジョーに殺された。")]
    [TestCase("You were killed by an amoeba.", "アメーバに殺された。")]
    [TestCase("You were killed by the snapjaw.", "スナップジョーに殺された。")]
    public void TryTranslateMessage_StripsEnglishArticlesFromKiller(string source, string expected)
    {
        var translated = DeathWrapperFamilyTranslator.TryTranslateMessage(source, spans: null, out var messageTranslated);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(messageTranslated, Is.EqualTo(expected));
        });
    }

    [Test]
    public void TryTranslateMessage_ReturnsFalseForUnknownCause()
    {
        const string source = "You were frobulated by a snapjaw.";

        var translated = DeathWrapperFamilyTranslator.TryTranslateMessage(source, spans: null, out var messageTranslated);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(messageTranslated, Is.EqualTo(source));
        });
    }

    private static IEnumerable<TestCaseData> SupportedDeathCases()
    {
        yield return Case("You were killed by a snapjaw.", "スナップジョーに殺された。");
        yield return Case("You were accidentally killed by a snapjaw.", "スナップジョーにうっかり殺された。");
        yield return Case("You were bitten to death by a snapjaw.", "スナップジョーに噛み殺された。");
        yield return Case("You were frozen to death by a snapjaw.", "スナップジョーに凍死させられた。");
        yield return Case("You were immolated by a snapjaw.", "スナップジョーに焼き殺された。");
        yield return Case("You were vaporized by a snapjaw.", "スナップジョーに蒸発させられた。");
        yield return Case("You were electrocuted by an amoeba.", "アメーバに感電死させられた。");
        yield return Case("You were dissolved by a snapjaw.", "スナップジョーに溶解させられた。");
        yield return Case("You were disintegrated by a snapjaw.", "スナップジョーに分解された。");
        yield return Case("You were plasma-burned to death by a snapjaw.", "スナップジョーにプラズマで焼き殺された。");
        yield return Case("You were lased to death by a snapjaw.", "スナップジョーにレーザーで殺された。");
        yield return Case("You were illuminated to death by a snapjaw.", "スナップジョーに光で殺された。");
        yield return Case("You were cooked by a snapjaw.", "スナップジョーに調理された。");
        yield return Case("You died of poison from a snapjaw.", "スナップジョーの毒で死亡した。");
        yield return Case("You bled to death because of a snapjaw.", "スナップジョーのせいで出血死した。");
        yield return Case("You died of asphyxiation from a snapjaw.", "スナップジョーによる窒息で死亡した。");
        yield return Case("Your metabolism failed from a snapjaw.", "スナップジョーによる代謝不全で死亡した。");
        yield return Case("Your vital essence was drained to extinction by a snapjaw.", "スナップジョーに生命力を吸い尽くされた。");
        yield return Case("You were psychically extinguished by a snapjaw.", "スナップジョーに精神的に消滅させられた。");
        yield return Case("You were mentally obliterated by a snapjaw.", "スナップジョーに精神破壊された。");
        yield return Case("You were pricked to death by a snapjaw.", "スナップジョーに刺し殺された。");
        yield return Case("You were killed by colliding with a wall.", "壁に衝突して死亡した。");
        yield return Case("You were decapitated by a snapjaw.", "スナップジョーに斬首された。");
        yield return Case("You were consumed whole by a snapjaw.", "スナップジョーに丸呑みにされた。");
        yield return Case("You were slammed by a snapjaw.", "スナップジョーに叩きつけられて死んだ。");
        yield return Case("You were slammed into a wall by a snapjaw.", "スナップジョーに壁へ叩きつけられて死んだ。");
        yield return Case("You were slammed into two walls by a snapjaw.", "スナップジョーに二つの壁へ叩きつけられて死んだ。");
        yield return Case("You were relieved of your vital anatomy by a snapjaw.", "スナップジョーに生命維持に必要な部位を失わされて死んだ。");
        yield return Case("You died in the explosion of a grenade.", "グレネードの爆発で死んだ。");
        yield return Case("You died in an explosion.", "爆発で死んだ。");
        yield return Case("You exploded.", "爆発した。");
        yield return Case("You were crushed under the weight of a thousand suns.", "千の太陽の重みで押し潰された。");
        yield return Case("You died of thirst.", "渇きで死んだ。");
        yield return Case("You killed yourself.", "自殺した。");
        yield return Case("You accidentally killed yourself.", "うっかり自殺した。");
    }

    private static IEnumerable<TestCaseData> PopupDeathCases()
    {
        yield return Case("You were killed by a snapjaw.", "スナップジョーに殺された。");
        yield return Case("You died of poison from a snapjaw.", "スナップジョーの毒で死亡した。");
        yield return Case("You bled to death because of a snapjaw.", "スナップジョーのせいで出血死した。");
        yield return Case("You died in the explosion of a grenade.", "グレネードの爆発で死んだ。");
        yield return Case("You died of thirst.", "渇きで死んだ。");
    }

    private static TestCaseData Case(string source, string expectedBody)
    {
        return new TestCaseData(source, expectedBody)
            .SetName(source.Replace("\n", "\\n", StringComparison.Ordinal));
    }

    private static IEnumerable<(string key, string text)> CommonEntries()
    {
        return new (string key, string text)[]
        {
            ("QudJP.DeathWrapper.Generic.Wrapped", "あなたは死んだ。\n\n{body}"),
            ("QudJP.DeathWrapper.KilledBy.Bare", "{killer}に殺された。"),
            ("QudJP.DeathWrapper.KilledByCollidingWith.Bare", "{killer}に衝突して死亡した。"),
            ("QudJP.DeathWrapper.AccidentallyKilledBy.Bare", "{killer}にうっかり殺された。"),
            ("QudJP.DeathWrapper.BittenToDeathBy.Bare", "{killer}に噛み殺された。"),
            ("QudJP.DeathWrapper.FrozenToDeathBy.Bare", "{killer}に凍死させられた。"),
            ("QudJP.DeathWrapper.ImmolatedBy.Bare", "{killer}に焼き殺された。"),
            ("QudJP.DeathWrapper.VaporizedBy.Bare", "{killer}に蒸発させられた。"),
            ("QudJP.DeathWrapper.ElectrocutedBy.Bare", "{killer}に感電死させられた。"),
            ("QudJP.DeathWrapper.DissolvedBy.Bare", "{killer}に溶解させられた。"),
            ("QudJP.DeathWrapper.DisintegratedBy.Bare", "{killer}に分解された。"),
            ("QudJP.DeathWrapper.PlasmaBurnedToDeathBy.Bare", "{killer}にプラズマで焼き殺された。"),
            ("QudJP.DeathWrapper.LasedToDeathBy.Bare", "{killer}にレーザーで殺された。"),
            ("QudJP.DeathWrapper.IlluminatedToDeathBy.Bare", "{killer}に光で殺された。"),
            ("QudJP.DeathWrapper.CookedBy.Bare", "{killer}に調理された。"),
            ("QudJP.DeathWrapper.DiedOfPoisonFrom.Bare", "{killer}の毒で死亡した。"),
            ("QudJP.DeathWrapper.BledToDeathBecauseOf.Bare", "{killer}のせいで出血死した。"),
            ("QudJP.DeathWrapper.DiedOfAsphyxiationFrom.Bare", "{killer}による窒息で死亡した。"),
            ("QudJP.DeathWrapper.MetabolismFailedFrom.Bare", "{killer}による代謝不全で死亡した。"),
            ("QudJP.DeathWrapper.VitalEssenceDrainedToExtinctionBy.Bare", "{killer}に生命力を吸い尽くされた。"),
            ("QudJP.DeathWrapper.PsychicallyExtinguishedBy.Bare", "{killer}に精神的に消滅させられた。"),
            ("QudJP.DeathWrapper.MentallyObliteratedBy.Bare", "{killer}に精神破壊された。"),
            ("QudJP.DeathWrapper.PrickedToDeathBy.Bare", "{killer}に刺し殺された。"),
            ("QudJP.DeathWrapper.DecapitatedBy.Bare", "{killer}に斬首された。"),
            ("QudJP.DeathWrapper.ConsumedWholeBy.Bare", "{killer}に丸呑みにされた。"),
            ("QudJP.DeathWrapper.SlammedBy.Bare", "{killer}に叩きつけられて死んだ。"),
            ("QudJP.DeathWrapper.SlammedIntoWallBy.Bare", "{killer}に壁へ叩きつけられて死んだ。"),
            ("QudJP.DeathWrapper.SlammedIntoTwoWallsBy.Bare", "{killer}に二つの壁へ叩きつけられて死んだ。"),
            ("QudJP.DeathWrapper.RelievedOfYourVitalAnatomyBy.Bare", "{killer}に生命維持に必要な部位を失わされて死んだ。"),
            ("QudJP.DeathWrapper.DiedInExplosionOf.Bare", "{killer}の爆発で死んだ。"),
            ("QudJP.DeathWrapper.DiedInExplosion.Bare", "爆発で死んだ。"),
            ("QudJP.DeathWrapper.Exploded.Bare", "爆発した。"),
            ("QudJP.DeathWrapper.CrushedUnderSuns.Bare", "千の太陽の重みで押し潰された。"),
            ("QudJP.DeathWrapper.DiedOfThirst.Bare", "渇きで死んだ。"),
            ("QudJP.DeathWrapper.Suicide.Bare", "自殺した。"),
            ("QudJP.DeathWrapper.AccidentalSuicide.Bare", "うっかり自殺した。"),
            ("snapjaw", "スナップジョー"),
            ("amoeba", "アメーバ"),
            ("wall", "壁"),
            ("grenade", "グレネード")
        };
    }

    private void WriteDictionary(IEnumerable<(string key, string text)> entries)
    {
        var builder = new StringBuilder();
        builder.Append('{');
        builder.Append("\"entries\":[");

        var first = true;
        foreach (var (key, text) in entries)
        {
            if (!first)
            {
                builder.Append(',');
            }

            first = false;
            builder.Append("{\"key\":\"");
            builder.Append(EscapeJson(key));
            builder.Append("\",\"text\":\"");
            builder.Append(EscapeJson(text));
            builder.Append("\"}");
        }

        builder.Append("]}");
        File.WriteAllText(
            Path.Combine(dictionaryDirectory, "test.ja.json"),
            builder.ToString(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);
    }
}
