using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class DescriptionLookPopupTranslationPatchTests
{
    [Test]
    public void HandleEvent_TranslatesLookPopupStoryChrome_WhenOwnerPatched()
    {
        using var patch = PatchDummyTarget();
        var target = new DummyDescriptionLookPopupTarget();

        target.HandleEvent();

        Assert.Multiple(() =>
        {
            Assert.That(target.StoryButtonText, Is.EqualTo("{{W|S}} ストーリーを思い出す"));
            Assert.That(target.LegacyPromptWithStory, Is.EqualTo("[{{W|space}}で続行 / {{W|s}}でストーリー]"));
            Assert.That(target.LegacyPromptWithoutStory, Is.EqualTo("[{{W|space}}で続行]"));
        });
    }

    [Test]
    public void HandleEvent_LeavesUnknownLookPopupChrome_WhenOwnerPatched()
    {
        using var patch = PatchDummyTarget(
            typeof(DummyDescriptionLookPopupEdgeTarget),
            nameof(DummyDescriptionLookPopupEdgeTarget.HandleUnknown));
        var target = new DummyDescriptionLookPopupEdgeTarget();

        target.HandleUnknown();

        Assert.That(target.Text, Is.EqualTo("Unknown look popup text"));
    }

    [Test]
    public void HandleEvent_HandlesEmptyLookPopupChrome_WhenOwnerPatched()
    {
        using var patch = PatchDummyTarget(
            typeof(DummyDescriptionLookPopupEdgeTarget),
            nameof(DummyDescriptionLookPopupEdgeTarget.HandleEmpty));
        var target = new DummyDescriptionLookPopupEdgeTarget();

        target.HandleEmpty();

        Assert.That(target.Text, Is.Empty);
    }

    [Test]
    public void HandleEvent_TranslatesColorTaggedLookPopupChrome_WhenOwnerPatched()
    {
        using var patch = PatchDummyTarget(
            typeof(DummyDescriptionLookPopupEdgeTarget),
            nameof(DummyDescriptionLookPopupEdgeTarget.HandleColorTagged));
        var target = new DummyDescriptionLookPopupEdgeTarget();

        target.HandleColorTagged();

        Assert.That(target.Text, Is.EqualTo("{{C|ストーリーを思い出す}}"));
    }

    [Test]
    public void HandleEvent_StripsDirectMarkedLookPopupChrome_WhenOwnerPatched()
    {
        using var patch = PatchDummyTarget(
            typeof(DummyDescriptionLookPopupEdgeTarget),
            nameof(DummyDescriptionLookPopupEdgeTarget.HandleDirectMarked));
        var target = new DummyDescriptionLookPopupEdgeTarget();

        target.HandleDirectMarked();

        Assert.That(target.Text, Is.EqualTo("{{W|S}} ストーリーを思い出す"));
    }

    private static IDisposable PatchDummyTarget()
    {
        return PatchDummyTarget(
            typeof(DummyDescriptionLookPopupTarget),
            nameof(DummyDescriptionLookPopupTarget.HandleEvent));
    }

    private static IDisposable PatchDummyTarget(Type targetType, string methodName)
    {
        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);
        harmony.Patch(
            original: RequireMethod(targetType, methodName),
            transpiler: new HarmonyMethod(RequireMethod(
                typeof(DescriptionLookPopupTranslationPatch),
                nameof(DescriptionLookPopupTranslationPatch.Transpiler))));
        return new HarmonyScope(harmony, harmonyId);
    }

    private static MethodInfo RequireMethod(Type type, string methodName)
    {
        return AccessTools.Method(type, methodName)
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private sealed class DummyDescriptionLookPopupTarget
    {
        public string StoryButtonText { get; private set; } = string.Empty;

        public string LegacyPromptWithStory { get; private set; } = string.Empty;

        public string LegacyPromptWithoutStory { get; private set; } = string.Empty;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void HandleEvent()
        {
            var storyButton = new DummyQudMenuItem
            {
                command = "Story",
                hotkey = "S",
                text = "Recall {{W|S}}tory",
            };

            StoryButtonText = storyButton.text;
            LegacyPromptWithStory = "[press {{W|space}} or recall {{W|s}}tory]";
            LegacyPromptWithoutStory = "[press {{W|space}}]";
        }
    }

    private sealed class DummyQudMenuItem
    {
        public string command = string.Empty;

        public string hotkey = string.Empty;

        public string text = string.Empty;
    }

    private sealed class DummyDescriptionLookPopupEdgeTarget
    {
        public string Text { get; private set; } = string.Empty;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void HandleUnknown()
        {
            Text = "Unknown look popup text";
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void HandleEmpty()
        {
            Text = "";
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void HandleColorTagged()
        {
            Text = "{{C|Recall Story}}";
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void HandleDirectMarked()
        {
            Text = "\u0001{{W|S}} ストーリーを思い出す";
        }
    }

    private sealed class HarmonyScope : IDisposable
    {
        private readonly Harmony harmony;
        private readonly string harmonyId;

        public HarmonyScope(Harmony harmony, string harmonyId)
        {
            this.harmony = harmony;
            this.harmonyId = harmonyId;
        }

        public void Dispose()
        {
            harmony.UnpatchAll(harmonyId);
        }
    }
}
