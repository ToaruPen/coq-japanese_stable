#if HAS_TMP
using TMPro;
#endif
using System.IO;

namespace QudJP.Tests.L1;

[NUnit.Framework.TestFixture]
[NUnit.Framework.Category("L1")]
public sealed class InventoryReplacementHardeningTests
{
    [NUnit.Framework.TestCase(true, true, 0, TextShellReplacementRenderer.ReplacementRenderAction.AttemptReplacement)]
    [NUnit.Framework.TestCase(false, true, 0, TextShellReplacementRenderer.ReplacementRenderAction.PreserveActiveReplacement)]
    [NUnit.Framework.TestCase(true, false, 0, TextShellReplacementRenderer.ReplacementRenderAction.PreserveActiveReplacement)]
    [NUnit.Framework.TestCase(true, false, 3, TextShellReplacementRenderer.ReplacementRenderAction.PreserveActiveReplacement)]
    [NUnit.Framework.TestCase(false, false, 0, TextShellReplacementRenderer.ReplacementRenderAction.PreserveActiveReplacement)]
    [NUnit.Framework.TestCase(true, true, 3, TextShellReplacementRenderer.ReplacementRenderAction.DisableReplacement)]
    public void DecideRenderActionForTests_ReturnsExpectedAction(
        bool originalEnabled,
        bool originalActiveInHierarchy,
        int originalCharacterCount,
        object expectedAction)
    {
        NUnit.Framework.Assert.That(
            TextShellReplacementRenderer.DecideRenderActionForTests(
                originalEnabled,
                originalActiveInHierarchy,
                originalCharacterCount),
            NUnit.Framework.Is.EqualTo(expectedAction));
    }

#if HAS_TMP
    [NUnit.Framework.Test]
    public void GetReplacementOverflowModeForTests_UsesOverflow()
    {
        NUnit.Framework.Assert.That(
            TextShellReplacementRenderer.GetReplacementOverflowModeForTests(),
            NUnit.Framework.Is.EqualTo(TextOverflowModes.Overflow));
    }

    [NUnit.Framework.TestCase(true, true, "translated", "Text", ExpectedResult = true)]
    [NUnit.Framework.TestCase(true, true, "translated", "QudJPReplacementText", ExpectedResult = false)]
    [NUnit.Framework.TestCase(true, false, "translated", "Text", ExpectedResult = false)]
    [NUnit.Framework.TestCase(true, true, "", "Text", ExpectedResult = false)]
    public bool CanAttemptRepairForTests_RejectsReplacementAndInvalidStates(
        bool enabled,
        bool activeInHierarchy,
        string text,
        string objectName)
    {
        return TmpTextRepairer.CanAttemptRepairForTests(enabled, activeInHierarchy, text, objectName);
    }
#endif

    [NUnit.Framework.TestCase("QudJPReplacementText", ExpectedResult = true)]
    [NUnit.Framework.TestCase("Text", ExpectedResult = false)]
    public bool IsReplacementTextNameForTests_DetectsOnlyReplacementName(string objectName)
    {
        return TextShellReplacementRenderer.IsReplacementTextNameForTests(objectName);
    }

    [NUnit.Framework.Test]
    public void HasActiveReplacementForCurrentItemText_DoesNotForceMeshUpdateFromLateUpdateGuard()
    {
        var sourcePath = Path.Combine(
            TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Assemblies",
            "src",
            "UI",
            "TextShellReplacementRenderer.cs");
        var source = File.ReadAllText(sourcePath);
        var methodStart = source.IndexOf(
            "internal static bool HasActiveReplacementForCurrentItemText",
            System.StringComparison.Ordinal);
        var methodEnd = source.IndexOf(
            "internal static TextOverflowModes GetReplacementOverflowModeForTests",
            System.StringComparison.Ordinal);

        NUnit.Framework.Assert.That(methodStart, NUnit.Framework.Is.GreaterThanOrEqualTo(0));
        NUnit.Framework.Assert.That(methodEnd, NUnit.Framework.Is.GreaterThan(methodStart));
        var methodSource = source[methodStart..methodEnd];

        NUnit.Framework.Assert.That(methodSource, NUnit.Framework.Does.Not.Contain("ForceMeshUpdate"));
    }

    [NUnit.Framework.TestCase("current", "original", ExpectedResult = "original")]
    [NUnit.Framework.TestCase("current", "", ExpectedResult = "current")]
    public string ResolvePreservedReplacementTextForTests_KeepsCurrentTextWhenOriginalIsEmpty(
        string currentReplacementText,
        string originalText)
    {
        return TextShellReplacementRenderer.ResolvePreservedReplacementTextForTests(currentReplacementText, originalText);
    }

    [NUnit.Framework.TestCase(true, true, ExpectedResult = true)]
    [NUnit.Framework.TestCase(false, true, ExpectedResult = false)]
    [NUnit.Framework.TestCase(true, false, ExpectedResult = true)]
    [NUnit.Framework.TestCase(false, false, ExpectedResult = false)]
    public bool ResolvePreservedReplacementActiveSelfForTests_PreservesOriginalActiveSelf(
        bool originalActiveSelf,
        bool originalActiveInHierarchy)
    {
        return TextShellReplacementRenderer.ResolvePreservedReplacementActiveSelfForTests(
            originalActiveSelf,
            originalActiveInHierarchy);
    }

    [NUnit.Framework.Test]
    public void ResolvePreservedReplacementActiveSelfForTests_KeepsReplacementActiveSelfAcrossCollapsedParent()
    {
        NUnit.Framework.Assert.That(
            TextShellReplacementRenderer.ResolvePreservedReplacementActiveSelfForTests(
                originalActiveSelf: true,
                originalActiveInHierarchy: false),
            NUnit.Framework.Is.True);
    }

    [NUnit.Framework.TestCase(true, true, ExpectedResult = true)]
    [NUnit.Framework.TestCase(false, true, ExpectedResult = false)]
    [NUnit.Framework.TestCase(true, false, ExpectedResult = false)]
    [NUnit.Framework.TestCase(false, false, ExpectedResult = false)]
    public bool ShouldRestoreOriginalAfterFailedPreservedReuseForTests_RequiresVisibleOriginal(
        bool originalActiveSelf,
        bool originalActiveInHierarchy)
    {
        return TextShellReplacementRenderer.ShouldRestoreOriginalAfterFailedPreservedReuseForTests(
            originalActiveSelf,
            originalActiveInHierarchy);
    }
}
