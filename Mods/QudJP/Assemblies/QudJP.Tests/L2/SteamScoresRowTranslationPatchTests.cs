using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;
using QudJP.Tests.L1;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class SteamScoresRowTranslationPatchTests
{
    private static Type? dynamicHighScoresDataElementType;

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
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        DynamicTextObservability.ResetForTests();
    }

    [TestCase(
        "{{R|The game is currently running offline.}}",
        "{{R|現在、ゲームはオフラインで動作しています。}}")]
    [TestCase("{{R|An error has occurred.}}", "{{R|エラーが発生しました。}}")]
    public void SetData_TranslatesReviewedStatusForRendering_ThenRestoresModel(
        string source,
        string expected)
    {
        WithPatch(() =>
        {
            var data = CreateHighScoresDataElement(source);
            var row = new DummySteamScoresRowTarget();

            row.setData(data);

            Assert.Multiple(() =>
            {
                Assert.That(row.RenderedMessage, Is.EqualTo(expected));
                Assert.That(GetMessage(data), Is.EqualTo(source));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(SteamScoresRowTranslationPatch),
                        "SteamScoresRow.StatusMessage"),
                    Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void SetData_LeavesTechnicalStackTraceUnchanged()
    {
        const string source = "{{R|Error: \nSystem.InvalidOperationException at Example.Run()}}";

        WithPatch(() =>
        {
            var data = CreateHighScoresDataElement(source);
            var row = new DummySteamScoresRowTarget();

            row.setData(data);

            Assert.Multiple(() =>
            {
                Assert.That(row.RenderedMessage, Is.EqualTo(source));
                Assert.That(GetMessage(data), Is.EqualTo(source));
            });
        });
    }

    [Test]
    public void SetData_RestoresModel_WhenRendererThrows()
    {
        const string source = "{{R|An error has occurred.}}";

        WithPatch(() =>
        {
            var data = CreateHighScoresDataElement(source);
            var row = new DummySteamScoresRowTarget { ThrowAfterRender = true };

            Assert.That(() => row.setData(data), Throws.TypeOf<InvalidOperationException>());
            Assert.That(GetMessage(data), Is.EqualTo(source));
        });
    }

    [Test]
    public void Prefix_LeavesDifferentDataElementTypeUnchanged()
    {
        const string source = "{{R|An error has occurred.}}";
        var data = new DummyHighScoresDataElement { message = source };

        SteamScoresRowTranslationPatch.Prefix(data, out var state);

        Assert.Multiple(() =>
        {
            Assert.That(data.message, Is.EqualTo(source));
            Assert.That(state, Is.Null);
        });
    }

    [TestCase("")]
    [TestCase("{{R|An unknown leaderboard status.}}")]
    public void Prefix_LeavesUnsupportedStatusUnchanged(string source)
    {
        var data = CreateHighScoresDataElement(source);

        SteamScoresRowTranslationPatch.Prefix(data, out var state);

        Assert.Multiple(() =>
        {
            Assert.That(GetMessage(data), Is.EqualTo(source));
            Assert.That(state, Is.Null);
        });
    }

    [Test]
    public void Prefix_DoesNotUseSameKeyFromWrongContext()
    {
        const string source = "{{R|An error has occurred.}}";
        WithTemporaryScoresDictionary(
            """
            {
              "meta": { "id": "ui-scores-test", "lang": "ja", "version": "0.1.0" },
              "entries": [
                {
                  "key": "{{R|An error has occurred.}}",
                  "context": "Poison.Context",
                  "text": "{{R|誤った翻訳}}"
                }
              ]
            }
            """,
            () =>
            {
                var data = CreateHighScoresDataElement(source);

                SteamScoresRowTranslationPatch.Prefix(data, out var state);

                Assert.Multiple(() =>
                {
                    Assert.That(GetMessage(data), Is.EqualTo(source));
                    Assert.That(state, Is.Null);
                });
            });
    }

    [Test]
    public void Prefix_FailsOpenWhenScoresDictionaryIsMissing()
    {
        const string source = "{{R|An error has occurred.}}";
        WithTemporaryScoresDictionary(
            dictionaryJson: null,
            () =>
            {
                var data = CreateHighScoresDataElement(source);

                SteamScoresRowTranslationPatch.Prefix(data, out var state);

                Assert.Multiple(() =>
                {
                    Assert.That(GetMessage(data), Is.EqualTo(source));
                    Assert.That(state, Is.Null);
                });
            });
    }

    private static void WithPatch(Action action)
    {
        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummySteamScoresRowTarget), nameof(DummySteamScoresRowTarget.setData)),
                prefix: new HarmonyMethod(RequireMethod(typeof(SteamScoresRowTranslationPatch), nameof(SteamScoresRowTranslationPatch.Prefix))),
                finalizer: new HarmonyMethod(RequireMethod(typeof(SteamScoresRowTranslationPatch), nameof(SteamScoresRowTranslationPatch.Finalizer))));
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static MethodInfo RequireMethod(Type type, string methodName)
    {
        return AccessTools.Method(type, methodName)
               ?? throw new MissingMethodException(type.FullName, methodName);
    }

    private static void WithTemporaryScoresDictionary(string? dictionaryJson, Action action)
    {
        var tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "qudjp-steam-scores-row-l2",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        try
        {
            if (dictionaryJson is not null)
            {
                File.WriteAllText(Path.Combine(tempDirectory, "ui-scores.ja.json"), dictionaryJson);
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

    private static object CreateHighScoresDataElement(string source)
    {
        var dataType = AccessTools.TypeByName("Qud.UI.HighScoresDataElement")
                       ?? (dynamicHighScoresDataElementType ??= CreateDynamicHighScoresDataElementType());
        var data = Activator.CreateInstance(dataType)
                   ?? throw new InvalidOperationException("Could not create the high-scores data test value.");
        RequireMessageField(data).SetValue(data, source);
        return data;
    }

    private static string? GetMessage(object data)
    {
        return RequireMessageField(data).GetValue(data) as string;
    }

    private static FieldInfo RequireMessageField(object data)
    {
        return data.GetType().GetField("message")
               ?? throw new MissingFieldException(data.GetType().FullName, "message");
    }

    private static Type CreateDynamicHighScoresDataElementType()
    {
        var assemblyName = new AssemblyName($"QudJP.Tests.DynamicHighScores.{Guid.NewGuid():N}");
        var assembly = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule(assemblyName.Name!);
        var type = module.DefineType(
            "Qud.UI.HighScoresDataElement",
            TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed);
        _ = type.DefineField("message", typeof(string), FieldAttributes.Public);
        return type.CreateType()
               ?? throw new InvalidOperationException("Could not create the high-scores data test type.");
    }
}
