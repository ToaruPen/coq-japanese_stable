using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class CoreInvalidObjectDisplayNameTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        DynamicTextObservability.ResetForTests();
    }

    [TestCase(
        nameof(DummyCoreInvalidObjectTarget.CreateObjectFull),
        "[invalid blueprint:MissingThing]",
        "[無効なブループリント:MissingThing]")]
    [TestCase(
        nameof(DummyCoreInvalidObjectTarget.CreateObjectWithBefore),
        "[invalid blueprint:OtherThing]",
        "[無効なブループリント:OtherThing]")]
    [TestCase(
        nameof(DummyCoreInvalidObjectTarget.GetCachedObjects),
        "INVALID CACHE OBJECT: cache-1",
        "無効なキャッシュオブジェクト: cache-1")]
    public void Postfix_TranslatesInvalidObjectDisplayNames_WhenPatched(
        string methodName,
        string source,
        string expected)
    {
        var harmonyId = "qudjp.tests.core-invalid-object-display-name." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyCoreInvalidObjectTarget), methodName),
                postfix: new HarmonyMethod(RequireMethod(
                    typeof(CoreInvalidObjectDisplayNameTranslationPatch),
                    nameof(CoreInvalidObjectDisplayNameTranslationPatch.Postfix),
                    typeof(object))));

            DummyCoreInvalidObjectTarget.NextDisplayName = source;
            IReadOnlyList<DummyCoreInvalidObject> result = methodName switch
            {
                nameof(DummyCoreInvalidObjectTarget.CreateObjectFull) =>
                    new[] { DummyCoreInvalidObjectTarget.CreateObjectFull("MissingThing") },
                nameof(DummyCoreInvalidObjectTarget.CreateObjectWithBefore) =>
                    new[] { DummyCoreInvalidObjectTarget.CreateObjectWithBefore("OtherThing", static _ => { }) },
                _ => DummyCoreInvalidObjectTarget.GetCachedObjects("cache-1"),
            };

            Assert.Multiple(() =>
            {
                Assert.That(result.Single().Render.DisplayName, Is.EqualTo(expected));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        CoreInvalidObjectDisplayNameTranslationPatch.Context,
                        CoreInvalidObjectDisplayNameTranslationPatch.Family),
                    Is.EqualTo(1));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_LeavesRegularDisplayNameUnchanged_WhenPatched()
    {
        DummyCoreInvalidObjectTarget.NextDisplayName = "snapjaw";

        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyCoreInvalidObjectTarget), nameof(DummyCoreInvalidObjectTarget.CreateObjectFull), typeof(string)),
                postfix: new HarmonyMethod(RequireMethod(typeof(CoreInvalidObjectDisplayNameTranslationPatch), nameof(CoreInvalidObjectDisplayNameTranslationPatch.Postfix), typeof(object))));

            var target = DummyCoreInvalidObjectTarget.CreateObjectFull("snapjaw");

            Assert.Multiple(() =>
            {
                Assert.That(target.Render.DisplayName, Is.EqualTo("snapjaw"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        CoreInvalidObjectDisplayNameTranslationPatch.Context,
                        CoreInvalidObjectDisplayNameTranslationPatch.Family),
                    Is.Zero);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
            DummyCoreInvalidObjectTarget.NextDisplayName = string.Empty;
        }
    }

    private static MethodInfo RequireMethod(Type type, string methodName, params Type[] parameterTypes)
    {
        var method = parameterTypes.Length == 0
            ? AccessTools.Method(type, methodName)
            : AccessTools.Method(type, methodName, parameterTypes);
        return method
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private static class DummyCoreInvalidObjectTarget
    {
        public static string NextDisplayName { get; set; } = string.Empty;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static DummyCoreInvalidObject CreateObjectFull(string objectBlueprint)
        {
            _ = objectBlueprint;
            return CreateInvalidObject();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static DummyCoreInvalidObject CreateObjectWithBefore(string objectBlueprint, Action<DummyCoreInvalidObject> beforeObjectCreated)
        {
            _ = objectBlueprint;
            _ = beforeObjectCreated;
            return CreateInvalidObject();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static List<DummyCoreInvalidObject> GetCachedObjects(string id)
        {
            _ = id;
            return new List<DummyCoreInvalidObject> { CreateInvalidObject() };
        }

        private static DummyCoreInvalidObject CreateInvalidObject()
        {
            return new DummyCoreInvalidObject
            {
                Render = new DummyCoreInvalidObjectRender
                {
                    DisplayName = NextDisplayName,
                },
            };
        }
    }

    private sealed class DummyCoreInvalidObject
    {
        public DummyCoreInvalidObjectRender Render { get; set; } = new DummyCoreInvalidObjectRender();
    }

    private sealed class DummyCoreInvalidObjectRender
    {
        public string DisplayName { get; set; } = string.Empty;
    }
}
