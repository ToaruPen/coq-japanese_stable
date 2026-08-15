using System.Reflection;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;
using QudJP.Tests.L1;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class CodeCompressorTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(Path.Combine(
            TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Localization",
            "Dictionaries"));
        DynamicTextObservability.ResetForTests();
        DummyPopupShow.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        DummyPopupShow.Reset();
    }

    [Test]
    public void LoadCode_TranslatesRequiredModError_WhenOwnerPatched()
    {
        const string source = "Error decoding build code - Required Mod \"{{Y|Example Mod}}\" not found.";

        WithPatchedOwner(() =>
        {
            InvokeDummyLoadCode(source);

            Assert.Multiple(() =>
            {
                Assert.That(
                    DummyPopupShow.LastShowAsyncMessage,
                    Is.EqualTo("ビルドコードのデコードエラー: 必須Mod「{{Y|Example Mod}}」が見つかりません。"));
                Assert.That(RouteHitCount(), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void LoadCode_TranslatesRequiredModError_WithEmptyModName()
    {
        const string source = "Error decoding build code - Required Mod \"\" not found.";

        WithPatchedOwner(() =>
        {
            InvokeDummyLoadCode(source);

            Assert.Multiple(() =>
            {
                Assert.That(
                    DummyPopupShow.LastShowAsyncMessage,
                    Is.EqualTo("ビルドコードのデコードエラー: 必須Mod「」が見つかりません。"));
                Assert.That(RouteHitCount(), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void LoadCode_StripsDirectMarkerWithoutRecordingTransform()
    {
        const string translated = "必須Modが見つかりません。";

        WithPatchedOwner(() =>
        {
            InvokeDummyLoadCode(MessageFrameTranslator.MarkDirectTranslation(translated));

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowAsyncMessage, Is.EqualTo(translated));
                Assert.That(RouteHitCount(), Is.Zero);
            });
        });
    }

    [Test]
    public void LoadCode_FailsOpenWhenDictionaryIsMissing()
    {
        const string source = "Error decoding build code - Required Mod \"Example Mod\" not found.";

        WithTemporaryChargenDictionary(
            dictionaryJson: null,
            () => WithPatchedOwner(() =>
            {
                InvokeDummyLoadCode(source);

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowAsyncMessage, Is.EqualTo(source));
                    Assert.That(RouteHitCount(), Is.Zero);
                });
            }));
    }

    [Test]
    public void LoadCode_FailsOpenWhenTranslatedTemplateHasInvalidPlaceholder()
    {
        const string source = "Error decoding build code - Required Mod \"Example Mod\" not found.";
        WithTemporaryChargenDictionary(
            """
            {
              "meta": { "id": "ui-chargen-test", "lang": "ja", "version": "0.1.0" },
              "entries": [
                {
                  "key": "Error decoding build code - Required Mod \"{0}\" not found.",
                  "context": "XRL.CharacterBuilds.CodeCompressor.loadCode",
                  "text": "不正なテンプレート: {1}"
                }
              ]
            }
            """,
            () => WithPatchedOwner(() =>
            {
                InvokeDummyLoadCode(source);

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowAsyncMessage, Is.EqualTo(source));
                    Assert.That(RouteHitCount(), Is.Zero);
                });
            }));
    }

    [Test]
    public void LoadCode_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        const string source = "Error decoding build code - Required Mod \"Example Mod\" not found.";

        OwnerPopupRouteTestHarness.WithPatchedPopupOnly(
            () => DummyPopupShow.ShowAsync(source).GetAwaiter().GetResult());

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowAsyncMessage, Is.EqualTo(source));
            Assert.That(RouteHitCount(), Is.Zero);
        });
    }

    [TestCase("")]
    [TestCase("Error decoding build code.")]
    public void LoadCode_LeavesUnsupportedTextUnchanged(string source)
    {
        WithPatchedOwner(() =>
        {
            InvokeDummyLoadCode(source);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowAsyncMessage, Is.EqualTo(source));
                Assert.That(RouteHitCount(), Is.Zero);
            });
        });
    }

    [Test]
    public void DummyTarget_MatchesRuntimeParameterShape()
    {
        var parameterTypes = RequireOwnerMethod()
            .GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ToArray();

        Assert.That(
            parameterTypes,
            Is.EqualTo(new[]
            {
                typeof(string),
                typeof(List<XRL.CharacterBuilds.AbstractEmbarkBuilderModule>),
                typeof(bool),
            }));
    }

    private static void WithPatchedOwner(Action action)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(CodeCompressorTranslationPatch),
            RequireOwnerMethod(),
            action);
    }

    private static void InvokeDummyLoadCode(string source)
    {
        DummyCodeCompressorTarget.loadCode(
            source,
            new List<XRL.CharacterBuilds.AbstractEmbarkBuilderModule>(),
            silent: false);
    }

    private static MethodInfo RequireOwnerMethod()
    {
        return OwnerPopupRouteTestHarness.RequireMethod(
            typeof(DummyCodeCompressorTarget),
            nameof(DummyCodeCompressorTarget.loadCode));
    }

    private static int RouteHitCount()
    {
        return OwnerPopupRouteTestHarness.RouteHitCount(
            typeof(CodeCompressorTranslationPatch),
            "CodeCompressor.RequiredModMissing");
    }

    private static void WithTemporaryChargenDictionary(string? dictionaryJson, Action action)
    {
        var tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "qudjp-code-compressor-l2",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        try
        {
            if (dictionaryJson is not null)
            {
                File.WriteAllText(Path.Combine(tempDirectory, "ui-chargen.ja.json"), dictionaryJson);
            }

            Translator.SetDictionaryDirectoryForTests(tempDirectory);
            action();
        }
        finally
        {
            Translator.ResetForTests();
            Directory.Delete(tempDirectory, recursive: true);
        }
    }
}
