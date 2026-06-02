using System.Reflection;
using System.Text;
using System.Text.Json;
using HarmonyLib;
using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class QudJPModTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    [SetUp]
    public void SetUp()
    {
        QudJPMod.ResetInitializationForTests();
        DisplayNameRouteTranslation.ResetForTests();
        Translator.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        QudJPMod.ResetInitializationForTests();
        DisplayNameRouteTranslation.ResetForTests();
        Translator.ResetForTests();
        ScopedDictionaryLookup.ResetForTests();
    }

    [Test]
    public void InitializeForTests_ResetsInitializationGuard_WhenInitializationFails()
    {
        var patchCalls = 0;

        Assert.That(
            () => QudJPMod.InitializeForTests(
                () => throw new InvalidOperationException("font failure"),
                () => patchCalls++),
            Throws.TypeOf<InvalidOperationException>().With.Message.EqualTo("font failure"));

        Assert.Multiple(() =>
        {
            Assert.That(QudJPMod.IsInitializedForTests(), Is.False);
            Assert.That(patchCalls, Is.EqualTo(0));
        });

        QudJPMod.InitializeForTests(
            static () => { },
            () => patchCalls++);

        Assert.Multiple(() =>
        {
            Assert.That(QudJPMod.IsInitializedForTests(), Is.True);
            Assert.That(patchCalls, Is.EqualTo(1));
        });
    }

    [Test]
    public void InitializeForTests_ResetsInitializationGuard_WhenPatchApplicationFails()
    {
        var fontCalls = 0;
        var patchCalls = 0;

        Assert.That(
            () => QudJPMod.InitializeForTests(
                () => fontCalls++,
                () =>
                {
                    patchCalls++;
                    throw new InvalidOperationException("patch failure");
                }),
            Throws.TypeOf<InvalidOperationException>().With.Message.EqualTo("patch failure"));

        Assert.Multiple(() =>
        {
            Assert.That(QudJPMod.IsInitializedForTests(), Is.False);
            Assert.That(fontCalls, Is.EqualTo(1));
            Assert.That(patchCalls, Is.EqualTo(1));
        });

        QudJPMod.InitializeForTests(
            () => fontCalls++,
            () => patchCalls++);

        Assert.Multiple(() =>
        {
            Assert.That(QudJPMod.IsInitializedForTests(), Is.True);
            Assert.That(fontCalls, Is.EqualTo(2));
            Assert.That(patchCalls, Is.EqualTo(2));
        });
    }

    [Test]
    public void InitializeForTests_RegistersDisplayNameRouteTranslator()
    {
        var tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "qudjp-display-name-route-registration-l2",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            Translator.SetDictionaryDirectoryForTests(tempDirectory);
            WriteDisplayNameDictionary(tempDirectory, "raw widget", "登録済み表示名");

            Assert.That(
                DisplayNameRouteTranslation.TranslatePreservingColors("raw widget", "QudJPModTests"),
                Is.EqualTo("raw widget"));

            QudJPMod.InitializeForTests(static () => { }, static () => { });

            Assert.That(
                DisplayNameRouteTranslation.TranslatePreservingColors("raw widget", nameof(GetDisplayNamePatch)),
                Is.EqualTo("登録済み表示名"));
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    [Test]
    public void InvokePatchAll_ScansQudJPModAssembly_WithoutRequiringRealHarmonyPatchApplication()
    {
        var harmony = new RecordingHarmonyProcessor();

        Assert.That(() => QudJPMod.InvokePatchAll(harmony), Throws.Nothing);

        Assert.Multiple(() =>
        {
            Assert.That(harmony.RequestedPatchTypes, Does.Contain(typeof(MessageQueueTranslationPatch)));
            Assert.That(harmony.RequestedPatchTypes, Does.Contain(typeof(PatchAllTestPatch)));
            Assert.That(harmony.RequestedPatchTypes, Does.Not.Contain(typeof(QudJPModTests)));
            Assert.That(harmony.PatchCallCount, Is.EqualTo(harmony.RequestedPatchTypes.Count));
        });
    }

    private static void WriteDisplayNameDictionary(string directory, string key, string text)
    {
        var contents = "{\"entries\":[{\"key\":"
            + JsonSerializer.Serialize(key)
            + ",\"text\":"
            + JsonSerializer.Serialize(text)
            + "}]}";
        File.WriteAllText(
            Path.Combine(directory, "ui-displayname-route.ja.json"),
            contents,
            Utf8WithoutBom);
    }

    [Test]
    public void GetHarmonyPatchTypes_ReturnsHarmonyPatchClassesOnly()
    {
        var patchTypes = QudJPMod.GetHarmonyPatchTypes(typeof(QudJPModTests).Assembly);

        Assert.Multiple(() =>
        {
            Assert.That(patchTypes, Does.Contain(typeof(PatchAllTestPatch)));
            Assert.That(patchTypes, Does.Not.Contain(typeof(QudJPModTests)));
            Assert.That(patchTypes, Does.Not.Contain(typeof(PatchAllDummyTarget)));
        });
    }

    [Test]
    public void GetHarmonyPatchTypes_ExcludesMessageQueueHelperPrefixClasses()
    {
        var patchTypes = QudJPMod.GetHarmonyPatchTypes(typeof(MessageLogPatch).Assembly);

        Assert.Multiple(() =>
        {
            Assert.That(patchTypes, Does.Contain(typeof(MessageQueueTranslationPatch)));
            Assert.That(patchTypes, Does.Not.Contain(typeof(MessageLogPatch)));
            Assert.That(patchTypes, Does.Not.Contain(typeof(PhysicsEnterCellPassByTranslationPatch)));
            Assert.That(patchTypes, Does.Not.Contain(typeof(ZoneManagerSetActiveZoneMessageQueuePatch)));
            Assert.That(patchTypes, Does.Not.Contain(typeof(CombatAndLogMessageQueuePatch)));
        });
    }

    [Test]
    public void GetHarmonyPatchTypes_ExcludesStatusTabHelperPostfixClasses()
    {
        var patchTypes = QudJPMod.GetHarmonyPatchTypes(typeof(MessageLogStatusScreenTranslationPatch).Assembly);

        Assert.Multiple(() =>
        {
            Assert.That(patchTypes, Does.Contain(typeof(StatusScreenTabTranslationPatch)));
            Assert.That(patchTypes, Does.Not.Contain(typeof(MessageLogStatusScreenTranslationPatch)));
            Assert.That(patchTypes, Does.Not.Contain(typeof(JournalStatusScreenTabTranslationPatch)));
            Assert.That(patchTypes, Does.Not.Contain(typeof(QuestsStatusScreenTabTranslationPatch)));
        });
    }

    [Test]
    public void GetHarmonyPatchTypes_ExcludesLegacyGamepadPromptHelperTranspilerClasses()
    {
        var patchTypes = QudJPMod.GetHarmonyPatchTypes(typeof(InventoryScreenTranslationPatch).Assembly);

        Assert.Multiple(() =>
        {
            Assert.That(patchTypes, Does.Contain(typeof(LegacyGamepadPromptTranslationPatch)));
            Assert.That(patchTypes, Does.Not.Contain(typeof(InventoryScreenTranslationPatch)));
            Assert.That(patchTypes, Does.Not.Contain(typeof(StatusScreenTranslationPatch)));
            Assert.That(patchTypes, Does.Not.Contain(typeof(JournalScreenTranslationPatch)));
            Assert.That(patchTypes, Does.Not.Contain(typeof(TinkeringScreenTranslationPatch)));
            Assert.That(patchTypes, Does.Not.Contain(typeof(QuestLogGamepadPromptTranslationPatch)));
            Assert.That(patchTypes, Does.Not.Contain(typeof(FactionsScreenGamepadPromptTranslationPatch)));
            Assert.That(patchTypes, Does.Not.Contain(typeof(SkillsAndPowersScreenTranslationPatch)));
            Assert.That(patchTypes, Does.Not.Contain(typeof(EquipmentScreenTranslationPatch)));
        });
    }

    [Test]
    public void LogPatchResults_OutputsMethodCount_AfterPatchingAssembly()
    {
        var harmonyId = $"qudjp.tests.logpatch.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: AccessTools.Method(typeof(PatchAllDummyTarget), nameof(PatchAllDummyTarget.Echo)),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(PatchAllTestPatch), nameof(PatchAllTestPatch.Postfix))));

            var output = TestTraceHelper.CaptureTrace(() => QudJPMod.LogPatchResults(harmony));
            Assert.That(output, Does.Contain("method(s) patched"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void TryPreparePatchType_ReturnsFalse_WhenTargetMethodReturnsNull()
    {
        var prepared = QudJPMod.TryPreparePatchType(typeof(NullTargetPatch), out var reason);

        Assert.Multiple(() =>
        {
            Assert.That(prepared, Is.False);
            Assert.That(reason, Does.Contain("returned null"));
        });
    }

    [Test]
    public void TryPreparePatchType_ReturnsFalse_WhenTargetMethodsReturnsEmpty()
    {
        var prepared = QudJPMod.TryPreparePatchType(typeof(EmptyTargetsPatch), out var reason);

        Assert.Multiple(() =>
        {
            Assert.That(prepared, Is.False);
            Assert.That(reason, Does.Contain("returned no target methods"));
        });
    }

    [Test]
    public void TryPreparePatchType_ReturnsTrue_ForSimpleHarmonyPatchWithoutCustomTargetResolver()
    {
        var prepared = QudJPMod.TryPreparePatchType(typeof(PatchAllTestPatch), out var reason);

        Assert.Multiple(() =>
        {
            Assert.That(prepared, Is.True);
            Assert.That(reason, Is.Empty);
        });
    }

    [Test]
    public void TryPreparePatchType_IncludesResolverAndExceptionType_WhenTargetMethodThrows()
    {
        var prepared = QudJPMod.TryPreparePatchType(typeof(ThrowingTargetPatch), out var reason);

        Assert.Multiple(() =>
        {
            Assert.That(prepared, Is.False);
            Assert.That(reason, Does.Contain("ThrowingTargetPatch"));
            Assert.That(reason, Does.Contain("threw:"));
            Assert.That(reason, Does.Contain(nameof(InvalidOperationException)));
            Assert.That(reason, Does.Contain("single target failed"));
        });
    }

    [Test]
    public void TryPreparePatchType_IncludesResolverAndExceptionType_WhenTargetMethodsThrows()
    {
        var prepared = QudJPMod.TryPreparePatchType(typeof(ThrowingTargetsPatch), out var reason);

        Assert.Multiple(() =>
        {
            Assert.That(prepared, Is.False);
            Assert.That(reason, Does.Contain("ThrowingTargetsPatch"));
            Assert.That(reason, Does.Contain("threw:"));
            Assert.That(reason, Does.Contain(nameof(InvalidOperationException)));
            Assert.That(reason, Does.Contain("multiple targets failed"));
        });
    }

    [Test]
    public void LogPatchResults_HandlesGetPatchedMethodsFailure_WithoutThrowing()
    {
        var output = TestTraceHelper.CaptureTrace(() => Assert.That(() => QudJPMod.LogPatchResults(ThrowingPatchedMethodsProbe.Create()), Throws.Nothing));
        Assert.That(output, Does.Contain("Failed to enumerate patched methods"));
    }

    [Test]
    public void LogPatchResults_WhenNoMethodsPatched_LogsAppleSiliconRosettaGuidance()
    {
        var output = TestTraceHelper.CaptureTrace(() => QudJPMod.LogPatchResults(EmptyPatchedMethodsProbe.Create()));

        Assert.Multiple(() =>
        {
            Assert.That(output, Does.Contain("Harmony patching complete: 0 method(s) patched."));
            Assert.That(output, Does.Contain("mprotect returned EACCES"));
            Assert.That(output, Does.Contain("arch -x86_64"));
            Assert.That(output, Does.Contain("0Harmony.dll"));
            Assert.That(output, Does.Contain("Harmony 2.4.2"));
        });
    }

    [Test]
    public void LogToUnity_WritesToTrace_InTestEnvironment()
    {
        var output = TestTraceHelper.CaptureTrace(() => QudJPMod.LogToUnity("[QudJP] test message"));
        Assert.That(output, Does.Contain("[QudJP] test message"));
    }

    // Simple test-local target for PatchAll assembly scanning to discover.
    internal static class PatchAllDummyTarget
    {
        public static string Echo(string input) => input;
    }

    // This [HarmonyPatch] class will be found by PatchAll when the test assembly is
    // scanned. If PatchAll() no-args gets the wrong assembly via GetCallingAssembly(),
    // this class will NOT be found and the test will fail (RED).
    [HarmonyPatch(typeof(PatchAllDummyTarget), nameof(PatchAllDummyTarget.Echo))]
    internal static class PatchAllTestPatch
    {
        public static void Postfix(ref string __result)
        {
            __result = $"[patched] {__result}";
        }
    }

    [HarmonyPatch]
    internal static class NullTargetPatch
    {
        [HarmonyTargetMethod]
        private static MethodBase? TargetMethod()
        {
            return null;
        }
    }

    [HarmonyPatch]
    internal static class EmptyTargetsPatch
    {
        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield break;
        }
    }

    [HarmonyPatch]
    internal static class ThrowingTargetPatch
    {
        [HarmonyTargetMethod]
        private static MethodBase TargetMethod()
        {
            throw new InvalidOperationException("single target failed");
        }
    }

    [HarmonyPatch]
    internal static class ThrowingTargetsPatch
    {
        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> TargetMethods()
        {
            throw new InvalidOperationException("multiple targets failed");
        }
    }

    internal sealed class RecordingHarmonyProcessor
    {
        public List<Type> RequestedPatchTypes { get; } = new();

        public int PatchCallCount { get; private set; }

        public RecordingPatchProcessor CreateClassProcessor(Type patchType)
        {
            RequestedPatchTypes.Add(patchType);
            return new RecordingPatchProcessor(this);
        }

        internal void RecordPatchCall()
        {
            PatchCallCount++;
        }
    }

    internal sealed class RecordingPatchProcessor(RecordingHarmonyProcessor owner)
    {
        public void Patch()
        {
            owner.RecordPatchCall();
        }
    }

    internal sealed class ThrowingPatchedMethodsProbe
    {
        private readonly int sentinel = 1;

        private ThrowingPatchedMethodsProbe()
        {
        }

        public static ThrowingPatchedMethodsProbe Create()
        {
            return new ThrowingPatchedMethodsProbe();
        }

        public System.Collections.IEnumerable GetPatchedMethods()
        {
            _ = sentinel;
            throw new InvalidOperationException("simulated patched-method enumeration failure");
        }
    }

    internal sealed class EmptyPatchedMethodsProbe
    {
        private readonly int sentinel = 1;

        private EmptyPatchedMethodsProbe()
        {
        }

        public static EmptyPatchedMethodsProbe Create()
        {
            return new EmptyPatchedMethodsProbe();
        }

        public System.Collections.IEnumerable GetPatchedMethods()
        {
            _ = sentinel;
            return Array.Empty<MethodBase>();
        }
    }
}
