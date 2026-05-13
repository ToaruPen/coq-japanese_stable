using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;
using QudJP.Tests.L1;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class DeployableInfrastructurePopupTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(true);
        DynamicTextObservability.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        UseRepositoryVerbDictionary();
        DummyPopupShow.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(null);
        DynamicTextObservability.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        DummyPopupShow.Reset();
    }

    [TestCase("You deploy 3フィート分のワイヤー", "あなたは3フィート分のワイヤーを展開した")]
    [TestCase("You deploy ワイヤー.", "あなたはワイヤーを展開した")]
    public void AttemptDeploy_TranslatesDeploySuccessPopup_WhenOwnerPatched(string source, string expected)
    {
        var target = new DummyDeployableInfrastructureTarget
        {
            PopupMessageToSend = source,
        };

        WithPatchedPopupOwner(target.AttemptDeploy);

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
            Assert.That(DeployableInfrastructureHitCount("DoesVerb"), Is.EqualTo(1));
        });
    }

    [Test]
    public void AttemptDeploy_TranslatesNoUsefulWayPopup_WhenOwnerPatched()
    {
        var target = new DummyDeployableInfrastructureTarget
        {
            PopupMessageToSend = "There is no useful way to deploy {{Y|ワイヤー}} there.",
            UseShowFail = true,
        };

        WithPatchedPopupOwner(target.AttemptDeploy);

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("ここで{{Y|ワイヤー}}を展開する有用な方法はない。"));
            Assert.That(DeployableInfrastructureHitCount("NoUsefulWay"), Is.EqualTo(1));
        });
    }

    [Test]
    public void AttemptDeploy_LeavesPopupUnchanged_WhenOwnerAbsent()
    {
        const string source = "You deploy ワイヤー.";
        var target = new DummyDeployableInfrastructureTarget
        {
            PopupMessageToSend = source,
        };

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);

            _ = target.AttemptDeploy(new DummyGameObject());

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
                Assert.That(DeployableInfrastructureHitCount("DoesVerb"), Is.Zero);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void AttemptDeploy_StripsDirectMarkerWithoutRecordingTransform_WhenOwnerPatched()
    {
        const string translated = "ワイヤーを展開した。";
        var target = new DummyDeployableInfrastructureTarget
        {
            PopupMessageToSend = MessageFrameTranslator.MarkDirectTranslation(translated),
        };

        WithPatchedPopupOwner(target.AttemptDeploy);

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(translated));
            Assert.That(DeployableInfrastructureHitCount("DoesVerb"), Is.Zero);
            Assert.That(DeployableInfrastructureHitCount("NoUsefulWay"), Is.Zero);
        });
    }

    [Test]
    public void AttemptDeploy_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        var target = new DummyDeployableInfrastructureTarget();

        WithPatchedPopupOwner(target.AttemptDeploy);

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(string.Empty));
            Assert.That(DeployableInfrastructureHitCount("DoesVerb"), Is.Zero);
            Assert.That(DeployableInfrastructureHitCount("NoUsefulWay"), Is.Zero);
        });
    }

    private static void WithPatchedPopupOwner(Func<DummyGameObject, bool> action)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);
            PatchOwner(harmony);

            _ = action(new DummyGameObject());
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

    private static void PatchOwner(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(
                typeof(DummyDeployableInfrastructureTarget),
                nameof(DummyDeployableInfrastructureTarget.AttemptDeploy),
                typeof(DummyGameObject)),
            prefix: new HarmonyMethod(RequireMethod(typeof(DeployableInfrastructureTranslationPatch), "Prefix")),
            finalizer: new HarmonyMethod(RequireMethod(typeof(DeployableInfrastructureTranslationPatch), "Finalizer")));
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

    private static int DeployableInfrastructureHitCount(string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(PopupShowTranslationPatch),
            "Popup.Show." + nameof(DeployableInfrastructureTranslationPatch) + "." + detail);
    }

    private static void UseRepositoryVerbDictionary()
    {
        var repositoryDictionaryPath = Path.Combine(
            TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Localization",
            "MessageFrames",
            "verbs.ja.json");
        MessageFrameTranslator.SetDictionaryPathForTests(repositoryDictionaryPath);
    }

    private static string CreateHarmonyId()
    {
        return $"qudjp.tests.{Guid.NewGuid():N}";
    }
}
