using QudJP.Patches;
using QudJP.Tests.L1;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class GameObjectDestroyTranslationPatchTests
{
    [TestCase("Your mind winks out of existence.", "あなたの精神は存在からかき消えた。")]
    [TestCase("Your mind winked out of existence.", "あなたの精神は存在からかき消えた。")]
    [TestCase("You die! (good job)", "あなたは死んだ！（よくできました）")]
    [TestCase("You were ", "あなたは")]
    [TestCase("obliterated", "跡形もなく消滅した。")]
    [TestCase("destroyed", "破壊された。")]
    [TestCase("unsupported destroy text", "unsupported destroy text")]
    public void TranslateFixedLiteralForTests_TranslatesDestroyLiterals(string source, string expected)
    {
        Assert.That(GameObjectDestroyTranslationPatch.TranslateFixedLiteralForTests(source), Is.EqualTo(expected));
    }

    [Test]
    public void TranslateCompanionDeathMessage_TranslatesNameVerbAndReason()
    {
        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(Path.Combine(
            TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Localization",
            "Dictionaries"));
        try
        {
            Assert.That(
                GameObjectDestroyTranslationPatch.TryTranslateCompanionDeathMessage(
                    "Your companion, snapjaw, died. snapjaw was vaporized.",
                    out var translated),
                Is.True);
            Assert.That(translated, Is.EqualTo("仲間のスナップジョーは死亡した。スナップジョーは蒸発した。"));
        }
        finally
        {
            Translator.ResetForTests();
        }
    }

    [TestCase("Your companion, snapjaw, died.", true, "仲間のスナップジョーは死亡した。")]
    [TestCase("Your companion, snapjaw, left.", false, "Your companion, snapjaw, left.")]
    [TestCase("", false, "")]
    [TestCase("\u0001Your companion, snapjaw, died.", false, "\u0001Your companion, snapjaw, died.")]
    public void TranslateCompanionDeathMessage_HandlesFallbackEmptyAndDirectMarker(
        string source,
        bool expectedTranslated,
        string expected)
    {
        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(Path.Combine(
            TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Localization",
            "Dictionaries"));
        try
        {
            Assert.Multiple(() =>
            {
                Assert.That(
                    GameObjectDestroyTranslationPatch.TryTranslateCompanionDeathMessage(source, out var translated),
                    Is.EqualTo(expectedTranslated));
                Assert.That(translated, Is.EqualTo(expected));
            });
        }
        finally
        {
            Translator.ResetForTests();
        }
    }

    [Test]
    public void TranslateCompanionDeathMessage_PreservesColorTaggedCompanionName()
    {
        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(Path.Combine(
            TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Localization",
            "Dictionaries"));
        try
        {
            Assert.That(
                GameObjectDestroyTranslationPatch.TryTranslateCompanionDeathMessage(
                    "Your companion, {{R|snapjaw}}, died.",
                    out var translated),
                Is.True);
            Assert.That(translated, Is.EqualTo("仲間の{{R|スナップジョー}}は死亡した。"));
        }
        finally
        {
            Translator.ResetForTests();
        }
    }

    [Test]
    public void TranslateCompanionDeathMessage_StripsDirectMarkerInsideColorTaggedCompanionName()
    {
        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(Path.Combine(
            TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Localization",
            "Dictionaries"));
        try
        {
            Assert.That(
                GameObjectDestroyTranslationPatch.TryTranslateCompanionDeathMessage(
                    "Your companion, {{R|\u0001snapjaw}}, died.",
                    out var translated),
                Is.True);
            Assert.That(translated, Is.EqualTo("仲間の{{R|スナップジョー}}は死亡した。"));
        }
        finally
        {
            Translator.ResetForTests();
        }
    }
}
