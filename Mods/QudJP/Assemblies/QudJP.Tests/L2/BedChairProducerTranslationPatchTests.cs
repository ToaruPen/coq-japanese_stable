using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class BedChairProducerTranslationPatchTests
{
    private string tempDirectory = null!;
    private string patternFilePath = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-bed-chair-producer-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        patternFilePath = Path.Combine(tempDirectory, "messages.ja.json");

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        MessagePatternTranslator.ResetForTests();
        MessagePatternTranslator.SetPatternFileForTests(patternFilePath);
        File.WriteAllText(patternFilePath, "{\"patterns\":[]}\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
        DummyPopupShow.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        MessagePatternTranslator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void BedPatch_TranslatesBrokenFragmentPopup_WhenPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShow(harmony);
            PatchOwner(
                harmony,
                RequireMethod(
                    typeof(DummyBedProducerTarget),
                    nameof(DummyBedProducerTarget.AttemptSleep),
                    typeof(DummyGameObject),
                    typeof(bool).MakeByRefType(),
                    typeof(bool).MakeByRefType(),
                    typeof(bool).MakeByRefType()),
                typeof(BedTranslationPatch));

            var target = new DummyBedProducerTarget
            {
                MessageToSend = "あなたは と位相がずれている。ベッド.",
            };

            target.AttemptSleep(new DummyGameObject(), out _, out _, out _);

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("ベッドと位相がずれている。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void ChairPatch_TranslatesBrokenFragmentPopup_WhenPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShow(harmony);
            PatchOwner(
                harmony,
                RequireMethod(
                    typeof(DummyChairProducerTarget),
                    nameof(DummyChairProducerTarget.SitDown),
                    typeof(DummyGameObject),
                    typeof(DummyGameEvent)),
                typeof(ChairTranslationPatch));

            var target = new DummyChairProducerTarget
            {
                MessageToSend = " を設定できない。椅子 down!",
            };

            _ = target.SitDown(new DummyGameObject());

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("椅子を置けない！"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void PatchPopupShow(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.Show)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));
    }

    private static void PatchOwner(Harmony harmony, MethodInfo original, Type patchType)
    {
        harmony.Patch(
            original: original,
            prefix: new HarmonyMethod(RequireMethod(patchType, nameof(BedTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(patchType, nameof(BedTranslationPatch.Finalizer), typeof(Exception))));
    }

    private static string CreateHarmonyId()
    {
        return $"qudjp.tests.{Guid.NewGuid():N}";
    }

    private static MethodInfo RequireMethod(Type type, string methodName, params Type[] parameterTypes)
    {
        if (parameterTypes.Length == 0)
        {
            return type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
        }

        return AccessTools.Method(type, methodName, parameterTypes)
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }
}
