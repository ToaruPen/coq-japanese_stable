using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class BasePronounProviderCustomizePopupTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DummyPopupShow.Reset();
    }

    [TestCase(
        "Should your gender be treated as fully plural, with you being addressed as a multiple subject in all circumstances?",
        "あなたのジェンダーを完全な複数形として扱い、あらゆる状況で複数主語として呼びかけますか？")]
    [TestCase(
        "Should your pronoun set be treated as conditionally plural, with you being addressed as a multiple subject only following a pronoun, as with with singular \"they\"?",
        "あなたの代名詞セットを条件付きの複数形として扱い、単数の \"they\" のように、代名詞の後でのみ複数主語として呼びかけますか？")]
    [TestCase(
        "Is an entity with this pronoun set treated grammatically as a person, such that it would be improper to say \"look at ze\" in reference to hir -- one would say \"look at ze person\" or \"look at hir\" instead?",
        "この代名詞セットのエンティティを文法上の人物として扱いますか？ つまり、hirを指して「look at ze」と言うのは不適切で、「look at ze person」または「look at hir」と言うべきですか？")]
    public void BasePronounProviderCustomize_TranslatesPopupMessages_WhenOwnerPatched(
        string source,
        string expected)
    {
        AssertPopupMessage(source, expected);
    }

    [Test]
    public void BasePronounProviderCustomize_DoesNotTranslateTraffic_WhenOwnerAbsent()
    {
        const string source = "Should your gender be treated as fully plural, with you being addressed as a multiple subject in all circumstances?";
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShowYesNoCancelAsync(harmony);

            _ = DummyPopupShow.ShowYesNoCancelAsync(source).GetAwaiter().GetResult();

            Assert.That(DummyPopupShow.LastShowYesNoCancelAsyncMessage, Is.EqualTo(source));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void BasePronounProviderCustomize_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        AssertPopupMessage(
            MessageFrameTranslator.MarkDirectTranslation("Should your gender be treated as fully plural?"),
            "Should your gender be treated as fully plural?");
    }

    [TestCase("")]
    [TestCase("What subjective pronoun (he, she, they, etc.) should be used for this gender?")]
    [TestCase("Should this pronoun set be plural?")]
    public void BasePronounProviderCustomize_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched(string source)
    {
        AssertPopupMessage(source, source);
    }

    private static void AssertPopupMessage(string source, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShowYesNoCancelAsync(harmony);
            PatchOwner(harmony);

            DummyBasePronounProviderCustomizeTarget.MessageToShow = source;
            _ = DummyBasePronounProviderCustomizeTarget.CustomizeProcess().GetAwaiter().GetResult();

            Assert.That(DummyPopupShow.LastShowYesNoCancelAsyncMessage, Is.EqualTo(expected));
        }
        finally
        {
            DummyBasePronounProviderCustomizeTarget.Reset();
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void PatchPopupShowYesNoCancelAsync(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.ShowYesNoCancelAsync)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));
    }

    private static void PatchOwner(Harmony harmony)
    {
        var sourceMethod = RequireMethod(typeof(DummyBasePronounProviderCustomizeTarget), nameof(DummyBasePronounProviderCustomizeTarget.CustomizeProcess));
        var moveNext = ResolveStateMachineMoveNext(sourceMethod)
            ?? throw new InvalidOperationException("Dummy CustomizeProcess state machine MoveNext not found.");

        harmony.Patch(
            original: moveNext,
            prefix: new HarmonyMethod(RequireMethod(typeof(BasePronounProviderCustomizePopupTranslationPatch), nameof(BasePronounProviderCustomizePopupTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(typeof(BasePronounProviderCustomizePopupTranslationPatch), nameof(BasePronounProviderCustomizePopupTranslationPatch.Finalizer), typeof(Exception))));
    }

    private static MethodInfo? ResolveStateMachineMoveNext(MethodInfo sourceMethod)
    {
        var asyncStateMachine = sourceMethod.GetCustomAttribute<AsyncStateMachineAttribute>();
        return asyncStateMachine?.StateMachineType is null
            ? null
            : AccessTools.Method(asyncStateMachine.StateMachineType, "MoveNext");
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
        return $"qudjp.tests.{Guid.NewGuid():N}";
    }

    private static class DummyBasePronounProviderCustomizeTarget
    {
        public static string MessageToShow { get; set; } = string.Empty;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static async Task<bool> CustomizeProcess()
        {
            _ = await DummyPopupShow.ShowYesNoCancelAsync(MessageToShow).ConfigureAwait(false);
            return true;
        }

        public static void Reset()
        {
            MessageToShow = string.Empty;
        }
    }
}
