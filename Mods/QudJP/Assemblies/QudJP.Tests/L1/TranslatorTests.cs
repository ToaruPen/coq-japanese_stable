using System.Text;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class TranslatorTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-translator-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void Translate_ReturnsJapanese_WhenKeyExists()
    {
        WriteDictionary("ui-test.ja.json", "Hello", "こんにちは");

        var translated = Translator.Translate("Hello");

        Assert.That(translated, Is.EqualTo("こんにちは"));
    }

    [Test]
    public void Translate_UsesExactDictionaryKeys_WithoutRouteAwareness()
    {
        WriteDictionary(
            "ui-test.ja.json",
            ("Hello", "こんにちは"),
            ("MessageFrame:Hello", "フレームこんにちは"));

        using var _ = Translator.PushLogContext("MessageFrame");

        var exactTranslation = Translator.Translate("Hello");
        var routeStyleTranslation = Translator.Translate("MessageFrame:Hello");

        Assert.Multiple(() =>
        {
            Assert.That(exactTranslation, Is.EqualTo("こんにちは"));
            Assert.That(routeStyleTranslation, Is.EqualTo("フレームこんにちは"));
        });
    }

    [Test]
    public void Translate_ReturnsOriginal_WhenKeyIsMissing()
    {
        WriteDictionary("ui-test.ja.json", "Hello", "こんにちは");

        var translated = Translator.Translate("Goodbye");

        Assert.That(translated, Is.EqualTo("Goodbye"));
    }

    [Test]
    public void Translate_LogsContext_WhenKeyIsMissing()
    {
        WriteDictionary("ui-test.ja.json", "Hello", "こんにちは");
        using var writer = new StringWriter();
        using var listener = new System.Diagnostics.TextWriterTraceListener(writer);
        System.Diagnostics.Trace.Listeners.Add(listener);

        try
        {
            using var _ = Translator.PushLogContext("TestRoute");
            const string missingKey = "MISSING_KEY_FOR_LogsContext";

            var translated = Translator.Translate(missingKey);

            listener.Flush();
            var output = writer.ToString();

            Assert.Multiple(() =>
            {
                Assert.That(translated, Is.EqualTo(missingKey));
                Assert.That(output, Does.Contain($"missing key '{missingKey}'"));
                Assert.That(output, Does.Contain("context: TestRoute"));
            });
        }
        finally
        {
            System.Diagnostics.Trace.Listeners.Remove(listener);
        }
    }

    [Test]
    public void Translate_PushMissingKeyLoggingSuppression_PreservesCounters()
    {
        WriteDictionary("ui-test.ja.json", "Hello", "こんにちは");
        _ = Translator.Translate("Hello");

        var output = TestTraceHelper.CaptureTrace(() =>
        {
            using var route = Translator.PushLogContext("MessageLogPatch");
            using var suppression = Translator.PushMissingKeyLoggingSuppression(true);

            _ = Translator.Translate("MISSING_KEY_FOR_Suppressed_A");
            _ = Translator.Translate("MISSING_KEY_FOR_Suppressed_B");
        });

        Assert.Multiple(() =>
        {
            Assert.That(output, Does.Not.Contain("Translator: missing key 'MISSING_KEY_FOR_Suppressed_A'"));
            Assert.That(output, Does.Not.Contain("Translator: missing key 'MISSING_KEY_FOR_Suppressed_B'"));
            Assert.That(Translator.GetMissingKeyHitCountForTests("MISSING_KEY_FOR_Suppressed_A"), Is.EqualTo(1));
            Assert.That(Translator.GetMissingKeyHitCountForTests("MISSING_KEY_FOR_Suppressed_B"), Is.EqualTo(1));
            Assert.That(Translator.GetMissingRouteHitCountForTests("MessageLogPatch"), Is.EqualTo(2));
        });
    }

    [Test]
    public void Translate_LogsNestedContextDetails_WhileKeepingPrimaryRouteCounts()
    {
        WriteDictionary("ui-test.ja.json", "Hello", "こんにちは");

        var output = TestTraceHelper.CaptureTrace(() =>
        {
            using var route = Translator.PushLogContext("MainMenuLocalizationPatch");
            using var collection = Translator.PushLogContext("collection=LeftOptions");
            using var itemType = Translator.PushLogContext("itemType=DummyMenuItem");
            using var field = Translator.PushLogContext("field=Text");
            _ = Translator.Translate("MISSING_KEY_FOR_NestedContext");
        });

        Assert.Multiple(() =>
        {
            Assert.That(
                output,
                Does.Contain("context: MainMenuLocalizationPatch > collection=LeftOptions > itemType=DummyMenuItem > field=Text"));
            Assert.That(
                Translator.GetMissingRouteHitCountForTests("MainMenuLocalizationPatch"),
                Is.EqualTo(1));
        });
    }

    [Test]
    public void Translate_LoadsDictionaryOnlyOnce_WhenCalledRepeatedly()
    {
        WriteDictionary("ui-test.ja.json", "Inventory", "インベントリ");

        var first = Translator.Translate("Inventory");
        var second = Translator.Translate("Inventory");

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo("インベントリ"));
            Assert.That(second, Is.EqualTo("インベントリ"));
            Assert.That(Translator.LoadInvocationCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void Translate_RepeatedMissingKeysRemainMeasurable()
    {
        WriteDictionary("ui-test.ja.json", "Hello", "こんにちは");

        using (Translator.PushLogContext("MessageLogPatch"))
        {
            _ = Translator.Translate("MISSING_KEY_FOR_RepeatedHits");
            _ = Translator.Translate("MISSING_KEY_FOR_RepeatedHits");
            _ = Translator.Translate("MISSING_KEY_FOR_RepeatedHits");
        }

        Assert.Multiple(() =>
        {
            Assert.That(
                Translator.GetMissingKeyHitCountForTests("MISSING_KEY_FOR_RepeatedHits"),
                Is.EqualTo(3));
            Assert.That(
                Translator.GetMissingRouteHitCountForTests("MessageLogPatch"),
                Is.EqualTo(3));
        });
    }

    [Test]
    public void Translate_MissingKeyLogging_IsThrottledToPowerOfTwoHits()
    {
        WriteDictionary("ui-test.ja.json", "Hello", "こんにちは");

        var output = TestTraceHelper.CaptureTrace(() =>
        {
            using (Translator.PushLogContext("MessageLogPatch"))
            {
                _ = Translator.Translate("MISSING_KEY_FOR_Throttle");
                _ = Translator.Translate("MISSING_KEY_FOR_Throttle");
                _ = Translator.Translate("MISSING_KEY_FOR_Throttle");
                _ = Translator.Translate("MISSING_KEY_FOR_Throttle");
            }
        });

        Assert.Multiple(() =>
        {
            Assert.That(output, Does.Contain("hit 1"));
            Assert.That(output, Does.Contain("hit 2"));
            Assert.That(output, Does.Not.Contain("hit 3"));
            Assert.That(output, Does.Contain("hit 4"));
        });
    }

    [Test]
    public void Translate_MissingKeySummary_RanksRoutes()
    {
        WriteDictionary("ui-test.ja.json", "Hello", "こんにちは");

        using (Translator.PushLogContext("PopupTranslationPatch"))
        {
            _ = Translator.Translate("MISSING_KEY_FOR_Popup");
        }

        using (Translator.PushLogContext("MessageLogPatch"))
        {
            _ = Translator.Translate("MISSING_KEY_FOR_MessageLog_A");
            _ = Translator.Translate("MISSING_KEY_FOR_MessageLog_B");
        }

        var summary = Translator.GetMissingKeySummaryForTests();
        var messageLogIndex = summary.IndexOf("MessageLogPatch=2", StringComparison.Ordinal);
        var popupIndex = summary.IndexOf("PopupTranslationPatch=1", StringComparison.Ordinal);

        Assert.Multiple(() =>
        {
            Assert.That(summary, Does.Contain("MessageLogPatch=2"));
            Assert.That(summary, Does.Contain("PopupTranslationPatch=1"));
            Assert.That(messageLogIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(popupIndex, Is.GreaterThan(messageLogIndex));
        });
    }

    [Test]
    public void Translate_LogsLoadSummary_AndDuplicateKeyDiagnostics()
    {
        WriteDictionary(
            "first.ja.json",
            ("Hello", "こんにちは"),
            ("Inventory", "インベントリ"));
        WriteDictionary("second.ja.json", ("Hello", "やあ"));

        var output = TestTraceHelper.CaptureTrace(() => Assert.That(Translator.Translate("Hello"), Is.EqualTo("やあ")));
        var summary = Translator.GetDictionaryLoadSummaryForTests();

        Assert.Multiple(() =>
        {
            Assert.That(output, Does.Contain("duplicate key overrides: Hello=1"));
            Assert.That(output, Does.Contain("loaded 2 unique entries from 2 file(s)"));
            Assert.That(summary, Does.Contain("3 raw entries"));
            Assert.That(summary, Does.Contain("1 duplicate key override(s)"));
            Assert.That(summary, Does.Contain("1 distinct key(s)"));
        });
    }

    [Test]
    [Category("L1")]
    public void Translate_ThrowsDirectoryNotFoundException_WhenDictionaryDirectoryMissing()
    {
        Translator.SetDictionaryDirectoryForTests("/nonexistent/qudjp/path");

        Assert.Throws<DirectoryNotFoundException>(() => Translator.Translate("test"));
    }

    [Test]
    [Category("L1")]
    public void Translate_ContinuesLoading_WhenDictionaryJsonIsCorrupt()
    {
        WriteDictionary("valid.ja.json", "Hello", "こんにちは");
        WriteRawDictionary("corrupt.ja.json", "{\"entries\":[{\"key\":\"Hello\",\"text\":\"こんにちは\"}");

        var output = TestTraceHelper.CaptureTrace(() =>
            Assert.That(Translator.Translate("Hello"), Is.EqualTo("こんにちは")));

        Assert.Multiple(() =>
        {
            Assert.That(output, Does.Contain("Failed to load dictionary file"));
            Assert.That(output, Does.Contain("corrupt.ja.json"));
        });
    }

    [Test]
    [Category("L1")]
    public void Translate_ContinuesLoading_WhenEntriesArrayIsMissing()
    {
        WriteDictionary("valid.ja.json", "Hello", "こんにちは");
        WriteRawDictionary("missing-entries.ja.json", "{}");

        var output = TestTraceHelper.CaptureTrace(() =>
            Assert.That(Translator.Translate("Hello"), Is.EqualTo("こんにちは")));

        Assert.Multiple(() =>
        {
            Assert.That(output, Does.Contain("Failed to load dictionary file"));
            Assert.That(output, Does.Contain("missing-entries.ja.json"));
        });
    }

    [Test]
    [Category("L1")]
    public void Translate_ThrowsArgumentNullException_WhenKeyIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => Translator.Translate(null!));
    }

    [Test]
    public void Translate_LogsFirstDuplicateOverrideDetails_OncePerKey()
    {
        WriteDictionary("first.ja.json", ("Hello", "こんにちは"));
        WriteDictionary("second.ja.json", ("Hello", "やあ"));
        WriteDictionary("third.ja.json", ("Hello", "もしもし"));

        var output = TestTraceHelper.CaptureTrace(() =>
            Assert.That(Translator.Translate("Hello"), Is.EqualTo("もしもし")));

        Assert.Multiple(() =>
        {
            Assert.That(
                output,
                Does.Contain("Translator duplicate key 'Hello': overridden by"));
            Assert.That(output, Does.Contain("second.ja.json"));
            Assert.That(output, Does.Contain("first.ja.json"));
            Assert.That(CountOccurrences(output, "Translator duplicate key 'Hello':"), Is.EqualTo(1));
        });
    }

    private void WriteDictionary(string fileName, string key, string text)
    {
        WriteDictionary(fileName, (key, text));
    }

    private void WriteDictionary(string fileName, params (string key, string text)[] entries)
    {
        var builder = new StringBuilder();
        builder.Append("{\"entries\":[");
        AppendEntries(builder, entries);
        builder.AppendLine("]}");
        WriteDictionaryFile(fileName, builder.ToString());
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var startIndex = 0;

        while ((startIndex = text.IndexOf(value, startIndex, StringComparison.Ordinal)) >= 0)
        {
            count++;
            startIndex += value.Length;
        }

        return count;
    }

    private void WriteRawDictionary(string fileName, string content)
    {
        WriteDictionaryFile(fileName, content + Environment.NewLine);
    }

    private static void AppendEntries(StringBuilder builder, IReadOnlyList<(string key, string text)> entries)
    {
        for (var index = 0; index < entries.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            var (key, text) = entries[index];
            builder.Append("{\"key\":\"");
            builder.Append(EscapeJson(key));
            builder.Append("\",\"text\":\"");
            builder.Append(EscapeJson(text));
            builder.Append("\"}");
        }
    }

    private void WriteDictionaryFile(string fileName, string content)
    {
        var path = Path.Combine(tempDirectory, fileName);
        File.WriteAllText(path, content, Utf8WithoutBom);
    }
}
