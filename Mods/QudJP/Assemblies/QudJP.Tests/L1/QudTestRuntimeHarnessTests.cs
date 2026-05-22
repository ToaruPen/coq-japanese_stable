using QudJP.QudTest;
using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class QudTestRuntimeHarnessTests
{
    private string fixturesDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        fixturesDirectory = Path.Combine(TestContext.CurrentContext.WorkDirectory, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixturesDirectory);
        Translator.ResetForTests();
        ScopedDictionaryLookup.ResetForTests();
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        StartReplaceTranslationPatch.ResetForTests();
        StartReplaceTranslationPatch.SetDictionaryPathForTests(RepositoryDictionary("templates-variable.ja.json"));
        Translator.SetDictionaryDirectoryForTests(
            Path.GetFullPath(Path.Combine(RepositoryDictionary("templates-variable.ja.json"), "..")));
    }

    [TearDown]
    public void TearDown()
    {
        StartReplaceTranslationPatch.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        SinkObservation.ResetForTests();
        DynamicTextObservability.ResetForTests();
        ScopedDictionaryLookup.ResetForTests();
        Translator.ResetForTests();
        if (Directory.Exists(fixturesDirectory))
        {
            Directory.Delete(fixturesDirectory, recursive: true);
        }
    }

    [Test]
    public void LoadFixtures_ReadsCaseDocumentsWithNewtonsoftJsonLoader()
    {
        WriteFixture(
            "runtime-smoke.json",
            suite: "runtime",
            """
            {"id":"start-replace.slip-ink","route":"start-replace","input":"{{K|=subject.T= =verb:slip= on the ink!}}","expected":"{{K|=subject.T=はインクで滑った！}}"}
            """);

        var fixtures = QudTestFixtureLoader.LoadDirectory(fixturesDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(fixtures, Has.Count.EqualTo(1));
            Assert.That(fixtures[0].Suite, Is.EqualTo("runtime"));
            Assert.That(fixtures[0].Cases, Has.Count.EqualTo(1));
            Assert.That(fixtures[0].Cases[0].Id, Is.EqualTo("start-replace.slip-ink"));
        });
    }

    [Test]
    public void LoadFixtures_RejectsNullCaseEntries()
    {
        File.WriteAllText(
            Path.Combine(fixturesDirectory, "runtime-smoke.json"),
            """
            {
              "schemaVersion": 1,
              "suite": "runtime",
              "description": "runtime smoke",
              "cases": [null]
            }
            """,
            System.Text.Encoding.UTF8);

        Assert.That(
            () => QudTestFixtureLoader.LoadDirectory(fixturesDirectory),
            Throws.InstanceOf<System.Runtime.Serialization.SerializationException>()
                .With.Message.Contains("case id and route are required"));
    }

    [Test]
    public void LoadFixtures_RejectsNullCasesCollection()
    {
        File.WriteAllText(
            Path.Combine(fixturesDirectory, "runtime-smoke.json"),
            """
            {
              "schemaVersion": 1,
              "suite": "runtime",
              "description": "runtime smoke",
              "cases": null
            }
            """,
            System.Text.Encoding.UTF8);

        Assert.That(
            () => QudTestFixtureLoader.LoadDirectory(fixturesDirectory),
            Throws.InstanceOf<System.Runtime.Serialization.SerializationException>()
                .With.Message.Contains("cases are required"));
    }

    [Test]
    public void Run_RejectsNullArguments()
    {
        Assert.Multiple(() =>
        {
            Assert.That(() => QudTestRunner.Run(null!, fixturesDirectory, "ja"), Throws.ArgumentNullException);
            Assert.That(() => QudTestRunner.Run("qudtest:runtime", null!, "ja"), Throws.ArgumentNullException);
            Assert.That(() => QudTestRunner.Run("qudtest:runtime", fixturesDirectory, null!), Throws.ArgumentNullException);
            Assert.That(() => QudTestRunner.Run("", fixturesDirectory, "ja"), Throws.ArgumentException);
            Assert.That(() => QudTestRunner.Run("qudtest:runtime", "", "ja"), Throws.ArgumentException);
            Assert.That(() => QudTestRunner.Run("qudtest:runtime", fixturesDirectory, ""), Throws.ArgumentException);
        });
    }

    [TestCase(
        "start-replace",
        "{{K|=subject.T= =verb:slip= on the ink!}}",
        "{{K|=subject.T=はインクで滑った！}}")]
    [TestCase("message-log", "\u0001ログに出る", "ログに出る")]
    [TestCase("message-queue", "\u0001キューに出る", "キューに出る")]
    [TestCase("wish-queue", "Turns until nephal arrives: 7", "ネファル到着までのターン数: 7")]
    [TestCase("wish-queue", "Turns until another thing arrives: 7", "Turns until another thing arrives: 7")]
    [TestCase("wish-queue", "", "")]
    [TestCase("wish-queue", "\u0001Turns until nephal arrives: 7", "Turns until nephal arrives: 7")]
    [TestCase("wish-queue", "{{R|Turns until nephal arrives: 7}}", "{{R|Turns until nephal arrives: 7}}")]
    [TestCase(
        "popup-text",
        "You can't find a way to flee from {{C|salt kraken}}.",
        "{{C|salt kraken}}から逃げる経路が見つからない。")]
    [TestCase(
        "popup-askstring-prompt",
        "If you quit without saving, you will lose all your unsaved progress. Are you sure you want to QUIT and LOSE YOUR PROGRESS?\n\n Type 'QUIT' to confirm.",
        "セーブせずに終了すると保存されていない進行状況がすべて失われます。本当に終了しますか？\\n\\n「QUIT」と入力すると確定します。")]
    [TestCase("popup-message-button", "{{W|[Tab]}} {{y|Hold to Accept}}", "{{W|[Tab]}} {{y|長押しして決定}}")]
    [TestCase("popup-message-button", "{{W|[Esc]}} {{y|Quit Without Saving}}", "{{W|[Esc]}} {{y|セーブせずに終了}}")]
    [TestCase("popup-menu-item", "{{W|[space]}} {{y|Continue}}", "{{W|[Space]}} {{y|続ける}}")]
    [TestCase("popup-menu-item", "{{W|[Esc]}} {{y|Cancel}}", "{{W|[Esc]}} {{y|キャンセル}}")]
    [TestCase("bottom-context-item", "{{W|[space]}} {{y|Continue}}", "{{W|[Space]}} {{y|続ける}}")]
    [TestCase("bottom-context-item", "{{y|{{W|[space]}} Continue}}", "{{W|[Space]}} {{y|続ける}}")]
    [TestCase("game-summary-menu-literal", "Save Tombstone File", "墓碑ファイルを保存")]
    [TestCase("game-summary-menu-literal", "Exit", "終了")]
    [TestCase("inventory-display-name", "{{c|copper nugget}} {{y|[empty]}}", "{{c|銅塊}} {{y|[空]}}")]
    public void ExecuteRoute_ProducesFinalRuntimeText(string route, string source, string expected)
    {
        Assert.That(QudTestRouteExecutor.Execute(route, source), Is.EqualTo(expected));
    }

    [Test]
    public void Run_BuildsResultDocumentFromFixtures()
    {
        WriteFixture(
            "runtime-smoke.json",
            suite: "runtime",
            """
            {"id":"start-replace.slip-ink","route":"start-replace","input":"{{K|=subject.T= =verb:slip= on the ink!}}","expected":"{{K|=subject.T=はインクで滑った！}}"},
            {"id":"message-log.direct-marker","route":"message-log","input":"\u0001ログに出る","expected":"ログに出る"}
            """);
        WriteFixture(
            "wish-smoke.json",
            suite: "wish",
            """
            {"id":"wish-queue.reclamation-timer","route":"wish-queue","input":"Turns until nephal arrives: 7","expected":"ネファル到着までのターン数: 7"}
            """);

        var result = QudTestRunner.Run("qudtest:runtime", fixturesDirectory, "ja");

        Assert.Multiple(() =>
        {
            Assert.That(result.Command, Is.EqualTo("qudtest:runtime"));
            Assert.That(result.Suite, Is.EqualTo("runtime"));
            Assert.That(result.ModLanguage, Is.EqualTo("ja"));
            Assert.That(result.Passed, Is.True);
            Assert.That(result.TotalCount, Is.EqualTo(2));
            Assert.That(result.PassCount, Is.EqualTo(2));
            Assert.That(result.FailCount, Is.Zero);
            Assert.That(result.Cases.Select(static testCase => testCase.Id), Is.EqualTo(new[]
            {
                "start-replace.slip-ink",
                "message-log.direct-marker",
            }));
        });
    }

    [Test]
    public void Run_RecordsColorShapeArtifactForInventoryDisplayName()
    {
        WriteFixture(
            "runtime-smoke.json",
            suite: "runtime",
            """
            {"id":"inventory-display-name.game-object-colored-state","route":"inventory-display-name","input":"{{c|copper nugget}} {{y|[empty]}}","expected":"{{c|銅塊}} {{y|[空]}}"}
            """);

        var result = QudTestRunner.Run("qudtest:runtime", fixturesDirectory, "ja");
        var colorShape = result.Cases[0].ColorShape;

        Assert.Multiple(() =>
        {
            Assert.That(result.Passed, Is.True);
            Assert.That(colorShape, Is.Not.Null);
            Assert.That(colorShape!.Route, Is.EqualTo(nameof(InventoryLineTranslationPatch)));
            Assert.That(colorShape.Producer, Is.EqualTo("QudTest.InventoryDisplayNameFixture"));
            Assert.That(colorShape.Source, Is.EqualTo("{{c|copper nugget}} {{y|[empty]}}"));
            Assert.That(colorShape.SourceVisible, Is.EqualTo("copper nugget [empty]"));
            Assert.That(colorShape.Final, Is.EqualTo("{{c|銅塊}} {{y|[空]}}"));
            Assert.That(colorShape.FinalVisible, Is.EqualTo("銅塊 [空]"));
            Assert.That(colorShape.SourceColorSpans, Does.Contain("0:{{c|"));
            Assert.That(colorShape.FinalColorSpans, Does.Contain("0:{{c|"));
            Assert.That(colorShape.MarkupSemanticStatus, Is.EqualTo("clean"));
        });
    }

    [Test]
    public void Run_RecordsFailureWithoutThrowingWhenActualDiffers()
    {
        WriteFixture(
            "runtime-smoke.json",
            suite: "runtime",
            """
            {"id":"message-log.bad-expected","route":"message-log","input":"\u0001ログに出る","expected":"間違い"}
            """);

        var result = QudTestRunner.Run("qudtest:all", fixturesDirectory, "ja");

        Assert.Multiple(() =>
        {
            Assert.That(result.Passed, Is.False);
            Assert.That(result.FailCount, Is.EqualTo(1));
            Assert.That(result.Cases[0].Actual, Is.EqualTo("ログに出る"));
            Assert.That(result.Cases[0].Diagnostic, Does.Contain("expected"));
        });
    }

    [Test]
    public void Run_RecordsFailureWhenSuiteMatchesNoFixtureCases()
    {
        WriteFixture(
            "runtime-smoke.json",
            suite: "runtime",
            """
            {"id":"message-log.direct-marker","route":"message-log","input":"\u0001ログに出る","expected":"ログに出る"}
            """);

        var result = QudTestRunner.Run("qudtest:missing", fixturesDirectory, "ja");

        Assert.Multiple(() =>
        {
            Assert.That(result.Passed, Is.False);
            Assert.That(result.TotalCount, Is.EqualTo(1));
            Assert.That(result.FailCount, Is.EqualTo(1));
            Assert.That(result.Cases[0].Id, Is.EqualTo("qudtest.no-cases"));
            Assert.That(result.Cases[0].Diagnostic, Does.Contain("no fixture cases matched suite"));
        });
    }

    private static string RepositoryDictionary(string fileName)
    {
        return Path.GetFullPath(
            Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "..",
                "Localization",
                "Dictionaries",
                fileName));
    }

    private void WriteFixture(string fileName, string suite, string casesJson)
    {
        File.WriteAllText(
            Path.Combine(fixturesDirectory, fileName),
            $$"""
            {
              "schemaVersion": 1,
              "suite": "{{suite}}",
              "description": "{{suite}} smoke",
              "cases": [
                {{casesJson}}
              ]
            }
            """,
            System.Text.Encoding.UTF8);
    }
}
