using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class WorldPartsProducerTranslationPatchTests
{
    private string tempDirectory = null!;
    private string patternFilePath = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-worldparts-producer-l2", Guid.NewGuid().ToString("N"));
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
        DummyPopupTarget.Reset();
        DummyMessageQueue.Reset();
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
    public void LiquidVolumePatch_TranslatesPopupShowMessage_WhenPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShow(harmony);
            PatchOwner(
                harmony,
                RequireMethod(
                    typeof(DummyLiquidVolumeProducerTarget),
                    nameof(DummyLiquidVolumeProducerTarget.PerformFill),
                    typeof(DummyGameObject),
                    typeof(bool).MakeByRefType(),
                    typeof(bool)),
                typeof(LiquidVolumeTranslationPatch));

            var requestExit = false;
            var target = new DummyLiquidVolumeProducerTarget
            {
                PopupMessageToShow = "Do you want to empty canteen first?",
            };

            target.PerformFill(new DummyGameObject(), ref requestExit);

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("canteenを先に空にしますか？"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void LiquidVolumePatch_TranslatesPopupBlockMessage_WhenPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupTarget), nameof(DummyPopupTarget.ShowBlock)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupTranslationPatch), nameof(PopupTranslationPatch.Prefix))));
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyLiquidVolumeProducerTarget), nameof(DummyLiquidVolumeProducerTarget.HandleEvent), typeof(DummyInventoryActionEvent)),
                typeof(LiquidVolumeTranslationPatch));

            var target = new DummyLiquidVolumeProducerTarget
            {
                PopupMessageToShow = "You are now {{B|hydrated}}.",
            };

            target.HandleEvent(new DummyInventoryActionEvent());

            Assert.That(DummyPopupTarget.LastShowBlockMessage, Is.EqualTo("あなたは今、{{B|hydrated}}。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void ClonelingVehiclePatch_TranslatesPopupFailure_WhenPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShow(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyVehicleRepairProducerTarget), nameof(DummyVehicleRepairProducerTarget.HandleEvent), typeof(DummyInventoryActionEvent)),
                typeof(ClonelingVehicleTranslationPatch));

            var target = new DummyVehicleRepairProducerTarget
            {
                PopupMessageToShow = "You do not have 1 dram of sunslag.",
            };

            target.HandleEvent(new DummyInventoryActionEvent());

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("sunslagを1ドラム持っていない。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void ClonelingVehiclePatch_TranslatesQueuedMessage_WhenPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyClonelingProducerTarget), nameof(DummyClonelingProducerTarget.AttemptCloning)),
                typeof(ClonelingVehicleTranslationPatch));

            var target = new DummyClonelingProducerTarget
            {
                QueuedMessageToSend = "Your onboard systems are out of cloning draught.",
            };

            _ = target.AttemptCloning();

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("搭載システムのcloning draughtが切れている。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void EnclosingPatch_TranslatesExtricatePopup_WhenPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShow(harmony);
            PatchOwner(
                harmony,
                RequireMethod(
                    typeof(DummyEnclosingProducerTarget),
                    nameof(DummyEnclosingProducerTarget.ExitEnclosure),
                    typeof(DummyGameObject),
                    typeof(DummyGameEvent),
                    typeof(DummyEnclosedEffect)),
                typeof(EnclosingTranslationPatch));

            var target = new DummyEnclosingProducerTarget
            {
                PopupMessageToShow = "You extricate yourself from stasis pod.",
            };

            _ = target.ExitEnclosure(new DummyGameObject(), new DummyGameEvent(), new DummyEnclosedEffect());

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("stasis podから抜け出した。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void GivesRepPatch_TranslatesWaterBondedPostfix_WhenPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyGivesRepProducerTarget), nameof(DummyGivesRepProducerTarget.HandleEvent), typeof(DummyGetShortDescriptionEvent)),
                prefix: new HarmonyMethod(RequireMethod(typeof(GivesRepShortDescriptionTranslationPatch), nameof(GivesRepShortDescriptionTranslationPatch.Prefix), typeof(object), typeof(int).MakeByRefType())),
                postfix: new HarmonyMethod(RequireMethod(typeof(GivesRepShortDescriptionTranslationPatch), nameof(GivesRepShortDescriptionTranslationPatch.Postfix), typeof(object), typeof(int))));

            var evt = new DummyGetShortDescriptionEvent();
            evt.Postfix.Append("既存の説明");
            var target = new DummyGivesRepProducerTarget
            {
                PostfixTextToAppend = "\nYou are water-bonded with Mehmet.",
            };

            _ = target.HandleEvent(evt);

            Assert.That(evt.Postfix.ToString(), Is.EqualTo("既存の説明\nMehmetと水の絆で結ばれている。"));
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

    private static void PatchQueue(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyMessageQueue), nameof(DummyMessageQueue.AddPlayerMessage), typeof(string), typeof(string), typeof(bool)),
            prefix: new HarmonyMethod(RequireMethod(typeof(CombatAndLogMessageQueuePatch), nameof(CombatAndLogMessageQueuePatch.Prefix), typeof(string).MakeByRefType(), typeof(string), typeof(bool))));
    }

    private static void PatchOwner(Harmony harmony, MethodInfo original, Type patchType)
    {
        harmony.Patch(
            original: original,
            prefix: new HarmonyMethod(RequireMethod(patchType, nameof(LiquidVolumeTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(patchType, nameof(LiquidVolumeTranslationPatch.Finalizer), typeof(Exception))));
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
