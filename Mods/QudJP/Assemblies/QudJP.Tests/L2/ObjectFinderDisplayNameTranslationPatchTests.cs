using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class ObjectFinderDisplayNameTranslationPatchTests
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

    [TestCase(typeof(DummyAutogotItemsContextTarget), "近くのアイテム")]
    [TestCase(typeof(DummyNearbyItemsContextTarget), "近くのアイテム")]
    [TestCase(typeof(DummyIdSorterTarget), "ID")]
    [TestCase(typeof(DummyValueSorterTarget), "価値")]
    public void Postfix_TranslatesObjectFinderDisplayName_WhenPatched(Type targetType, string expected)
    {
        var harmonyId = "qudjp.tests.object-finder-display-name." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(targetType, "GetDisplayName"),
                postfix: new HarmonyMethod(RequireMethod(
                    typeof(ObjectFinderDisplayNameTranslationPatch),
                    nameof(ObjectFinderDisplayNameTranslationPatch.Postfix),
                    typeof(string).MakeByRefType())));

            var result = (string)RequireMethod(targetType, "GetDisplayName").Invoke(Activator.CreateInstance(targetType), null)!;

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(expected));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        ObjectFinderDisplayNameTranslationPatch.Context,
                        ObjectFinderDisplayNameTranslationPatch.Family),
                    Is.EqualTo(1));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [TestCase("Other", "Other")]
    [TestCase("", "")]
    [TestCase("\u0001Something", "\u0001Something")]
    public void Postfix_LeavesUnknownAndEdgeDisplayNameUnchanged(string source, string expected)
    {
        ObjectFinderDisplayNameTranslationPatch.Postfix(ref source);

        Assert.Multiple(() =>
        {
            Assert.That(source, Is.EqualTo(expected));
            Assert.That(
                DynamicTextObservability.GetRouteFamilyHitCountForTests(
                    ObjectFinderDisplayNameTranslationPatch.Context,
                    ObjectFinderDisplayNameTranslationPatch.Family),
                Is.Zero);
        });
    }

    private static MethodInfo RequireMethod(Type type, string methodName, params Type[] parameterTypes)
    {
        var method = parameterTypes.Length == 0
            ? AccessTools.Method(type, methodName)
            : AccessTools.Method(type, methodName, parameterTypes);
        return method
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private sealed class DummyAutogotItemsContextTarget
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public string GetDisplayName() => "Nearby Items";
    }

    private sealed class DummyNearbyItemsContextTarget
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public string GetDisplayName() => "Nearby Items";
    }

    private sealed class DummyIdSorterTarget
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public string GetDisplayName() => "Id";
    }

    private sealed class DummyValueSorterTarget
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public string GetDisplayName() => "Value";
    }
}
