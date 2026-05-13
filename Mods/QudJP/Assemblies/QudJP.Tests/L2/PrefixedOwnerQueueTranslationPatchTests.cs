using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;
using QudJP.Tests.L1;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class PrefixedOwnerQueueTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        UseRepositoryDictionaries();
        DummyMessageQueue.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        LocalizationAssetResolver.SetLocalizationRootForTests(null);
        MessageFrameTranslator.ResetForTests();
        DummyMessageQueue.Reset();
    }

    [TestCase(
        nameof(DummySimpleOwnerQueueTarget.FleeTakeAction),
        "You are fleeing from {{R|snapjaw}}!",
        "{{R|snapjaw}}から逃げ出している！")]
    [TestCase(
        nameof(DummySimpleOwnerQueueTarget.InfiltratePerformInfiltrate),
        "You are teleported by {{Y|phase spider}}.",
        "{{Y|phase spider}}によって転送された。")]
    [TestCase(
        nameof(DummySimpleOwnerQueueTarget.TemperatureControllerConfigureTemperatureController),
        "You set a target temperature of -500.",
        "目標温度を-500に設定した。")]
    [TestCase(
        nameof(DummySimpleOwnerQueueTarget.TemperatureControllerConfigureTemperatureController),
        "You set a target temperature of .",
        "目標温度をに設定した。")]
    public void Patch_TranslatesPrefixedQueueMessages_WithRepositoryDictionaries(
        string ownerMethodName,
        string source,
        string expected)
    {
        AssertOwnerQueuedMessage(ownerMethodName, source, expected);
    }

    [TestCase("You are fleeing from {{R|snapjaw}}!")]
    [TestCase("You are teleported by {{Y|phase spider}}.")]
    [TestCase("You set a target temperature of -500.")]
    public void Patch_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent(string source)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchMessageQueue(harmony);

            DummyMessageQueue.AddPlayerMessage(source, null, Capitalize: false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(source));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Patch_DoesNotRetranslateDirectMarkedMessage_WhenOwnerPatched()
    {
        AssertOwnerQueuedMessage(
            nameof(DummySimpleOwnerQueueTarget.FleeTakeAction),
            MessageFrameTranslator.MarkDirectTranslation("You are fleeing from {{R|snapjaw}}!"),
            "You are fleeing from {{R|snapjaw}}!");
    }

    [Test]
    public void Patch_LeavesEmptyMessageUnchanged_WhenOwnerPatched()
    {
        AssertOwnerQueuedMessage(
            nameof(DummySimpleOwnerQueueTarget.FleeTakeAction),
            string.Empty,
            string.Empty);
    }

    private static void AssertOwnerQueuedMessage(string ownerMethodName, string source, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchMessageQueue(harmony);
            PatchOwner(harmony, ownerMethodName);

            var target = new DummySimpleOwnerQueueTarget
            {
                MessageToSend = source,
            };

            InvokeOwner(target, ownerMethodName);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void PatchMessageQueue(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyMessageQueue), nameof(DummyMessageQueue.AddPlayerMessage), typeof(string), typeof(string), typeof(bool)),
            prefix: new HarmonyMethod(RequireMethod(typeof(CombatAndLogMessageQueuePatch), nameof(CombatAndLogMessageQueuePatch.Prefix), typeof(string).MakeByRefType(), typeof(string), typeof(bool))));
        harmony.Patch(
            original: RequireMethod(typeof(DummyMessageQueue), nameof(DummyMessageQueue.AddPlayerMessage), typeof(string), typeof(string), typeof(bool)),
            prefix: new HarmonyMethod(RequireMethod(typeof(MessageLogPatch), nameof(MessageLogPatch.Prefix), typeof(string).MakeByRefType(), typeof(string), typeof(bool))));
    }

    private static void PatchOwner(Harmony harmony, string ownerMethodName)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummySimpleOwnerQueueTarget), ownerMethodName),
            prefix: new HarmonyMethod(RequireMethod(typeof(PrefixedOwnerQueueTranslationPatch), nameof(PrefixedOwnerQueueTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(typeof(PrefixedOwnerQueueTranslationPatch), nameof(PrefixedOwnerQueueTranslationPatch.Finalizer), typeof(Exception))));
    }

    private static void InvokeOwner(DummySimpleOwnerQueueTarget target, string ownerMethodName)
    {
        _ = RequireMethod(typeof(DummySimpleOwnerQueueTarget), ownerMethodName).Invoke(target, Array.Empty<object>());
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

    private static string CreateHarmonyId()
    {
        return $"qudjp.tests.prefixed-owner-queue.{Guid.NewGuid():N}";
    }

    private static void UseRepositoryDictionaries()
    {
        var localizationRoot = Path.Combine(TestProjectPaths.GetRepositoryRoot(), "Mods", "QudJP", "Localization");
        LocalizationAssetResolver.SetLocalizationRootForTests(localizationRoot);
        Translator.SetDictionaryDirectoryForTests(Path.Combine(localizationRoot, "Dictionaries"));
    }
}
