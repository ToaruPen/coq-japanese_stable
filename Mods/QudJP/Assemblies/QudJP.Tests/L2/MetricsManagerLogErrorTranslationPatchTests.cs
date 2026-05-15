using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class MetricsManagerLogErrorTranslationPatchTests
{
    private const string TitleFamily = "Popup.ProducerText.MetricsManagerLogErrorTranslationPatch.Title";
    private const string DiagnosticBodyFamily = "Popup.ProducerText.MetricsManagerLogErrorTranslationPatch.DiagnosticBodyPreserved";

    private string tempDirectory = null!;
    private string dictionaryDirectory = null!;
    private string patternFilePath = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-metrics-manager-log-error-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        dictionaryDirectory = Path.Combine(tempDirectory, "dict");
        Directory.CreateDirectory(dictionaryDirectory);
        patternFilePath = Path.Combine(tempDirectory, "messages.ja.json");

        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(true);
        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(dictionaryDirectory);
        MessagePatternTranslator.ResetForTests();
        MessagePatternTranslator.SetPatternFileForTests(patternFilePath);
        MessageFrameTranslator.ResetForTests();
        File.WriteAllText(patternFilePath, "{\"patterns\":[]}\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
        DummyPopupTarget.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(null);
        Translator.ResetForTests();
        MessagePatternTranslator.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void LogError_TranslatesTitleAndPreservesDiagnosticBody_WhenOwnerPatched()
    {
        AssertOwnerCall(
            nameof(DummyMetricsManagerLogErrorTarget.LogErrorMessage),
            "boom\n   at MetricsManager.LogError()");
        AssertOwnerCall(
            nameof(DummyMetricsManagerLogErrorTarget.LogErrorContextMessage),
            "LoadMap - boom\n   at MetricsManager.LogError()");
        AssertOwnerCall(
            nameof(DummyMetricsManagerLogErrorTarget.LogErrorContextException),
            "LoadMap:\nSystem.InvalidOperationException: boom");

        Assert.Multiple(() =>
        {
            Assert.That(GetHitCount(TitleFamily), Is.EqualTo(3));
            Assert.That(GetHitCount(DiagnosticBodyFamily), Is.EqualTo(3));
        });
    }

    [Test]
    public void LogError_DoesNotClaimDiagnosticPopup_WhenOwnerAbsent()
    {
        const string source = "LoadMap - boom\n   at MetricsManager.LogError()";
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShowBlock(harmony);

            DummyPopupTarget.ShowBlock(source, "{{R|Error}}");

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupTarget.LastShowBlockMessage, Is.EqualTo(source));
                Assert.That(DummyPopupTarget.LastShowBlockTitle, Is.EqualTo("{{R|Error}}"));
                Assert.That(GetHitCount(TitleFamily), Is.EqualTo(0));
                Assert.That(GetHitCount(DiagnosticBodyFamily), Is.EqualTo(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void LogError_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        const string body = "LoadMap - boom\n   at MetricsManager.LogError()";
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShowBlock(harmony);
            PatchOwnerMethod(harmony, nameof(DummyMetricsManagerLogErrorTarget.LogErrorMarkedDirect));

            DummyMetricsManagerLogErrorTarget.LogErrorMarkedDirect();

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupTarget.LastShowBlockMessage, Is.EqualTo(body));
                Assert.That(DummyPopupTarget.LastShowBlockTitle, Is.EqualTo("{{R|Error}}"));
                Assert.That(GetHitCount(TitleFamily), Is.EqualTo(0));
                Assert.That(GetHitCount(DiagnosticBodyFamily), Is.EqualTo(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void LogError_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShowBlock(harmony);
            PatchOwnerMethod(harmony, nameof(DummyMetricsManagerLogErrorTarget.LogErrorEmpty));

            DummyMetricsManagerLogErrorTarget.LogErrorEmpty();

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupTarget.LastShowBlockMessage, Is.Empty);
                Assert.That(DummyPopupTarget.LastShowBlockTitle, Is.Empty);
                Assert.That(GetHitCount(TitleFamily), Is.EqualTo(0));
                Assert.That(GetHitCount(DiagnosticBodyFamily), Is.EqualTo(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void LogExceptionRuntimeShape_StaysDeferred_WhenOwnerAbsent()
    {
        const string source = "LoadMap:\nSystem.InvalidOperationException: boom";
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShowBlock(harmony);

            DummyPopupTarget.ShowBlock(source, "{{R|Error}}");

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupTarget.LastShowBlockMessage, Is.EqualTo(source));
                Assert.That(DummyPopupTarget.LastShowBlockTitle, Is.EqualTo("{{R|Error}}"));
                Assert.That(GetHitCount(DiagnosticBodyFamily), Is.EqualTo(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertOwnerCall(string methodName, string expectedBody)
    {
        DummyPopupTarget.Reset();
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShowBlock(harmony);
            PatchOwnerMethod(harmony, methodName);

            RequireMethod(typeof(DummyMetricsManagerLogErrorTarget), methodName).Invoke(null, Array.Empty<object>());

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupTarget.LastShowBlockMessage, Is.EqualTo(expectedBody));
                Assert.That(DummyPopupTarget.LastShowBlockTitle, Is.EqualTo("{{R|エラー}}"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void PatchPopupShowBlock(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupTarget), nameof(DummyPopupTarget.ShowBlock)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupTranslationPatch), nameof(PopupTranslationPatch.Prefix))));
    }

    private static void PatchOwnerMethod(Harmony harmony, string methodName)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyMetricsManagerLogErrorTarget), methodName),
            prefix: new HarmonyMethod(RequireMethod(typeof(MetricsManagerLogErrorTranslationPatch), nameof(MetricsManagerLogErrorTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(typeof(MetricsManagerLogErrorTranslationPatch), nameof(MetricsManagerLogErrorTranslationPatch.Finalizer), typeof(Exception))));
    }

    private static int GetHitCount(string family)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(PopupTranslationPatch), family);
    }

    private static string CreateHarmonyId()
    {
        return "qudjp.metrics-manager-log-error-l2." + Guid.NewGuid().ToString("N");
    }

    private static MethodInfo RequireMethod(Type type, string name, params Type[] parameterTypes)
    {
        var method = parameterTypes.Length == 0
            ? type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            : type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static, parameterTypes);
        Assert.That(method, Is.Not.Null, $"{type.FullName}.{name} was not found.");
        return method!;
    }
}

internal static class DummyMetricsManagerLogErrorTarget
{
    public static void LogErrorMessage()
    {
        DummyPopupTarget.ShowBlock("boom\n   at MetricsManager.LogError()", "{{R|Error}}");
    }

    public static void LogErrorContextMessage()
    {
        DummyPopupTarget.ShowBlock("LoadMap - boom\n   at MetricsManager.LogError()", "{{R|Error}}");
    }

    public static void LogErrorContextException()
    {
        DummyPopupTarget.ShowBlock("LoadMap:\nSystem.InvalidOperationException: boom", "{{R|Error}}");
    }

    public static void LogErrorMarkedDirect()
    {
        DummyPopupTarget.ShowBlock(
            MessageFrameTranslator.MarkDirectTranslation("LoadMap - boom\n   at MetricsManager.LogError()"),
            MessageFrameTranslator.MarkDirectTranslation("{{R|Error}}"));
    }

    public static void LogErrorEmpty()
    {
        DummyPopupTarget.ShowBlock(string.Empty, string.Empty);
    }
}
