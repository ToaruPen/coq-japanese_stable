using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class MutationDisplayNameTranslationPatchTests
{
    private string localizationRoot = null!;

    [SetUp]
    public void SetUp()
    {
        localizationRoot = Path.Combine(Path.GetTempPath(), "qudjp-mutation-display-name-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(localizationRoot);
        File.WriteAllText(
            Path.Combine(localizationRoot, "Mutations.jp.xml"),
            """
            <mutations>
              <category Name="Physical">
                <mutation Name="Force Wall" DisplayName="力場壁" />
                <mutation Name="Albino" DisplayName="アルビノ" />
              </category>
            </mutations>
            """);
        File.WriteAllText(
            Path.Combine(localizationRoot, "HiddenMutations.jp.xml"),
            """
            <mutations>
              <category Name="Hidden">
                <mutation Name="Quantum Jitters" DisplayName="量子的震え" />
              </category>
            </mutations>
            """);

        LocalizationAssetResolver.SetLocalizationRootForTests(localizationRoot);
        StatusScreenPopupTranslationPatch.ResetForTests();
        DynamicTextObservability.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        LocalizationAssetResolver.SetLocalizationRootForTests(null);
        StatusScreenPopupTranslationPatch.ResetForTests();
        DynamicTextObservability.ResetForTests();
        if (Directory.Exists(localizationRoot))
        {
            Directory.Delete(localizationRoot, recursive: true);
        }
    }

    [TestCase(nameof(DummyMutationDisplayNameTarget.BaseMutationGetDisplayName), "Force Wall", "力場壁")]
    [TestCase(nameof(DummyMutationDisplayNameTarget.MutationEntryGetDisplayName), "{{R|Albino}} ({{r|D}})", "{{R|アルビノ}} ({{r|D}})")]
    [TestCase(nameof(DummyMutationDisplayNameTarget.MutationEntryGetDisplayName), "Quantum Jitters", "量子的震え")]
    public void Postfix_TranslatesMutationDisplayNames_WhenPatched(
        string methodName,
        string source,
        string expected)
    {
        var harmonyId = "qudjp.tests.mutation-display-name." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyMutationDisplayNameTarget), methodName, typeof(bool)),
                postfix: new HarmonyMethod(RequireMethod(
                    typeof(MutationDisplayNameTranslationPatch),
                    nameof(MutationDisplayNameTranslationPatch.Postfix),
                    typeof(string).MakeByRefType())));

            DummyMutationDisplayNameTarget.NextResult = source;
            var result = (string)RequireMethod(typeof(DummyMutationDisplayNameTarget), methodName, typeof(bool))
                .Invoke(null, new object[] { true })!;

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(expected));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        MutationDisplayNameTranslationPatch.Context,
                        MutationDisplayNameTranslationPatch.Family),
                    Is.EqualTo(1));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [TestCase("")]
    [TestCase("Unknown Mutation")]
    public void Postfix_LeavesUnsupportedMutationDisplayNamesUnchanged(string source)
    {
        var result = InvokePatchedDisplayName(
            nameof(DummyMutationDisplayNameTarget.BaseMutationGetDisplayName),
            source);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(source));
            Assert.That(
                DynamicTextObservability.GetRouteFamilyHitCountForTests(
                    MutationDisplayNameTranslationPatch.Context,
                    MutationDisplayNameTranslationPatch.Family),
                Is.Zero);
        });
    }

    [Test]
    public void Postfix_StripsDirectMarkedDisplayName()
    {
        var result = InvokePatchedDisplayName(
            nameof(DummyMutationDisplayNameTarget.BaseMutationGetDisplayName),
            MessageFrameTranslator.MarkDirectTranslation("Force Wall"));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo("Force Wall"));
            Assert.That(
                DynamicTextObservability.GetRouteFamilyHitCountForTests(
                    MutationDisplayNameTranslationPatch.Context,
                    MutationDisplayNameTranslationPatch.Family),
                Is.EqualTo(1));
        });
    }

    private static string InvokePatchedDisplayName(string methodName, string source)
    {
        var harmonyId = "qudjp.tests.mutation-display-name." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyMutationDisplayNameTarget), methodName, typeof(bool)),
                postfix: new HarmonyMethod(RequireMethod(
                    typeof(MutationDisplayNameTranslationPatch),
                    nameof(MutationDisplayNameTranslationPatch.Postfix),
                    typeof(string).MakeByRefType())));

            DummyMutationDisplayNameTarget.NextResult = source;
            return (string)RequireMethod(typeof(DummyMutationDisplayNameTarget), methodName, typeof(bool))
                .Invoke(null, new object[] { true })!;
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static MethodInfo RequireMethod(Type type, string methodName, params Type[] parameterTypes)
    {
        return AccessTools.Method(type, methodName, parameterTypes)
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private static class DummyMutationDisplayNameTarget
    {
        public static string NextResult { get; set; } = string.Empty;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string BaseMutationGetDisplayName(bool WithAnnotations = true)
        {
            _ = WithAnnotations;
            return NextResult;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string MutationEntryGetDisplayName(bool WithAnnotations = false)
        {
            _ = WithAnnotations;
            return NextResult;
        }
    }
}
