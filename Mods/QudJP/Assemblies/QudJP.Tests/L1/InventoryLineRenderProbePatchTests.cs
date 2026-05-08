using System.IO;

namespace QudJP.Tests.L1;

[NUnit.Framework.TestFixture]
[NUnit.Framework.Category("L1")]
public sealed class InventoryLineRenderProbePatchTests
{
    [NUnit.Framework.Test]
    public void InventoryLineRenderProbePatch_DoesNotScheduleReplacementOverlay()
    {
        var sourcePath = Path.Combine(
            TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Assemblies",
            "src",
            "Patches",
            "InventoryLineRenderProbePatch.cs");
        var source = File.ReadAllText(sourcePath);

        NUnit.Framework.Assert.That(
            source,
            NUnit.Framework.Does.Not.Contain("DelayedInventoryLineRepairScheduler.ScheduleRepair("));
    }

    [NUnit.Framework.Test]
    public void InventoryLineTranslationPatch_ForcesPrimaryFontAfterFinalItemText()
    {
        var sourcePath = Path.Combine(
            TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Assemblies",
            "src",
            "Patches",
            "InventoryLineTranslationPatch.cs");
        var source = File.ReadAllText(sourcePath);

        NUnit.Framework.Assert.That(
            source,
            NUnit.Framework.Does.Contain("InventoryLineFontFixer.TryForcePrimaryFontOnTextSkin(itemTextSkin, translatedDisplayName)"));
    }

    [NUnit.Framework.Test]
    public void InventoryLineActiveTextRefreshPatch_RefreshesAfterLineBecomesActive()
    {
        var sourcePath = Path.Combine(
            TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Assemblies",
            "src",
            "Patches",
            "InventoryLineActiveTextRefreshPatch.cs");

        NUnit.Framework.Assert.That(File.Exists(sourcePath), NUnit.Framework.Is.True);
        var source = File.ReadAllText(sourcePath);

        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("\"LateUpdate\""));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("InventoryLineFontFixer.IsActiveItemLine(__instance)"));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("InventoryLineFontFixer.HasActiveReplacementForCurrentItemText(__instance)"));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("InventoryLineFontFixer.TryRefreshActiveItemLine(__instance)"));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("DelayedInventoryLineRepairScheduler.ScheduleRepairForCurrentText("));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Not.Contain("TextShellReplacementRenderer"));
    }

    [NUnit.Framework.Test]
    public void InventoryLineFontFixer_TreatsZeroCharactersAsRefreshFailure()
    {
        var sourcePath = Path.Combine(
            TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Assemblies",
            "src",
            "UI",
            "InventoryLineFontFixer.cs");
        var source = File.ReadAllText(sourcePath);

        NUnit.Framework.Assert.That(
            source,
            NUnit.Framework.Does.Contain("return tmp.textInfo.characterCount > 0;"));
    }

    [NUnit.Framework.Test]
    public void InventoryLineFontFixer_AllowsWrappedTextSkinTmpFields()
    {
        var sourcePath = Path.Combine(
            TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Assemblies",
            "src",
            "UI",
            "InventoryLineFontFixer.cs");
        var source = File.ReadAllText(sourcePath);

        NUnit.Framework.Assert.That(
            source,
            NUnit.Framework.Does.Not.Contain("textSkin is not Component"));
        NUnit.Framework.Assert.That(
            source,
            NUnit.Framework.Does.Contain("Access(textSkin, \"_tmp\") as TextMeshProUGUI"));
    }

    [NUnit.Framework.Test]
    public void DelayedInventoryLineRepairScheduler_RearmsOnlyWhenLineTextChanges()
    {
        var sourcePath = Path.Combine(
            TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Assemblies",
            "src",
            "UI",
            "DelayedInventoryLineRepairScheduler.cs");
        var source = File.ReadAllText(sourcePath);

        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("LastScheduledTextByLine"));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("ScheduleRepairForCurrentText"));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("AttemptCounts.TryRemove(lineId, out _)"));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Not.Contain("ScheduleRepair(__instance, resetAttempts: true)"));
    }

    [NUnit.Framework.Test]
    public void DelayedInventoryLineRepairScheduler_LogsReplacementEvidenceAfterSuccessfulRepair()
    {
        var sourcePath = Path.Combine(
            TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Assemblies",
            "src",
            "UI",
            "DelayedInventoryLineRepairScheduler.cs");
        var source = File.ReadAllText(sourcePath);

        NUnit.Framework.Assert.That(
            source,
            NUnit.Framework.Does.Contain("TextShellReplacementRenderer.TryRenderReplacementTexts(component, out var replacementLogLine)"));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("RuntimeDiagnostics.VerboseProbesEnabled"));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("LogInventoryReplacementEvidence(replacementLogLine)"));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("LogVerboseRepairProbeSnapshots(component)"));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("TextShellReplacementRenderer.TryBuildReplacementState("));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("\"InventoryLineReplacementStateNextFrame/v1\""));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("LogInventoryReplacementEvidence(stateLogLine)"));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("ScreenHierarchyObservability.TryBuildLineItemSnapshot("));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("\"InventoryLineItemProbe/v1\""));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("LogInventoryReplacementEvidence(itemLogLine)"));

        var repairIndex = source.IndexOf(
            "TextShellReplacementRenderer.TryRenderReplacementTexts(component, out var replacementLogLine)",
            System.StringComparison.Ordinal);
        var invisibleRepairIndex = source.IndexOf(
            "TmpTextRepairer.TryRepairInvisibleTexts(component)",
            System.StringComparison.Ordinal);
        var verboseGateIndex = source.IndexOf(
            "RuntimeDiagnostics.VerboseProbesEnabled",
            System.StringComparison.Ordinal);
        var logIndex = source.IndexOf(
            "LogInventoryReplacementEvidence(replacementLogLine)",
            System.StringComparison.Ordinal);

        NUnit.Framework.Assert.That(repairIndex, NUnit.Framework.Is.GreaterThanOrEqualTo(0));
        NUnit.Framework.Assert.That(invisibleRepairIndex, NUnit.Framework.Is.GreaterThan(repairIndex));
        NUnit.Framework.Assert.That(verboseGateIndex, NUnit.Framework.Is.GreaterThan(invisibleRepairIndex));
        NUnit.Framework.Assert.That(logIndex, NUnit.Framework.Is.GreaterThan(verboseGateIndex));
    }
}
