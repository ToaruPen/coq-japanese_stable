using System.Text;
using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class EquipmentApiTwiddleObjectTranslationPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-equipment-api-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(Path.Combine(
            QudJP.Tests.L1.TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Localization",
            "Dictionaries"));

        var patternFilePath = Path.Combine(tempDirectory, "messages.ja.json");
        File.WriteAllText(patternFilePath, "{\"patterns\":[]}\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        MessagePatternTranslator.ResetForTests();
        MessagePatternTranslator.SetPatternFileForTests(patternFilePath);

        MessageFrameTranslator.ResetForTests();
        MessageFrameTranslator.SetDictionaryPathForTests(Path.Combine(
            QudJP.Tests.L1.TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Localization",
            "MessageFrames",
            "verbs.ja.json"));
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        MessagePatternTranslator.ResetForTests();
        MessageFrameTranslator.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [TestCase(
        "{{Y|telekinetic lever}} are out of your telekinetic range.",
        "{{Y|telekinetic lever}}",
        "{{Y|telekinetic lever}}はあなたの念動力の範囲外だ")]
    [TestCase(
        "You cannot do that from here.",
        "",
        "ここからはそれはできない。")]
    public void TwiddleObject_TranslatesUsabilityPopups_WhenOwnerScoped(
        string source,
        string captureToken,
        string expected)
    {
        AssertScopedPipeline(source, captureToken, expected);
    }

    [Test]
    public void TwiddleObject_DoesNotRetranslateDirectMarkedPopup_WhenOwnerScoped()
    {
        AssertScopedPipeline(
            MessageFrameTranslator.MarkDirectTranslation("You cannot do that from here."),
            "",
            "You cannot do that from here.");
    }

    [Test]
    public void TwiddleObject_LeavesEmptyPopupUnchanged_WhenOwnerScoped()
    {
        AssertScopedPipeline(string.Empty, "", string.Empty);
    }

    [Test]
    public void TwiddleObject_LeavesUnsupportedPopupUnchanged_WhenOwnerScoped()
    {
        AssertScopedPipeline("Something else happened.", "", "Something else happened.");
    }

    [TestCase("{{Y|telekinetic lever}} are out of your telekinetic range.", "{{Y|telekinetic lever}}")]
    [TestCase("You cannot do that from here.", "")]
    public void TwiddleObject_DoesNotClaimSupportedPopup_WhenOwnerAbsent(string source, string captureToken)
    {
        var candidate = BuildSource(source, captureToken);

        var claimed = EquipmentApiTwiddleObjectTranslationPatch.TryTranslatePopupMessage(
            candidate,
            nameof(PopupShowTranslationPatch),
            nameof(EquipmentApiTwiddleObjectTranslationPatch),
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(claimed, Is.False);
            Assert.That(translated, Is.EqualTo(candidate));
        });
    }

    private static void AssertScopedPipeline(string source, string captureToken, string expected)
    {
        EquipmentApiTwiddleObjectTranslationPatch.Prefix();
        try
        {
            var translated = PopupShowSemanticPipeline.TranslateMessage(
                BuildSource(source, captureToken),
                nameof(PopupShowTranslationPatch));

            Assert.That(translated, Is.EqualTo(expected));
            if (captureToken.Length > 0)
            {
                Assert.That(translated, Does.Contain(captureToken));
            }
        }
        finally
        {
            _ = EquipmentApiTwiddleObjectTranslationPatch.Finalizer(null);
        }
    }

    private static string BuildSource(string source, string captureToken)
    {
        if (captureToken.Length == 0)
        {
            return source;
        }

        var fragment = source[..source.IndexOf(" out of", StringComparison.Ordinal)];
        return DoesVerbRouteTranslator.MarkDoesFragment(fragment, "are", captureToken.Length, null)
            + " out of your telekinetic range.";
    }
}
