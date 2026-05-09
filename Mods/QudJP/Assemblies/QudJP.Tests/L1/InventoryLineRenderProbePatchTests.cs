using System.IO;

namespace QudJP.Tests.L1;

[NUnit.Framework.TestFixture]
[NUnit.Framework.Category("L1")]
public sealed class InventoryLineRenderProbePatchTests
{
    [NUnit.Framework.Test]
    public void InventoryActionDictionary_CoversObservedContextMenuLabelsWithoutQudMenuItemDuplication()
    {
        var inventoryActionPath = Path.Combine(
            TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Localization",
            "Dictionaries",
            "ui-inventory-actions.ja.json");
        var inventoryActionSource = File.ReadAllText(inventoryActionPath);

        NUnit.Framework.Assert.That(inventoryActionSource, NUnit.Framework.Does.Contain("\"key\": \"mark important\""));
        NUnit.Framework.Assert.That(inventoryActionSource, NUnit.Framework.Does.Contain("\"context\": \"XRL.World.IInventoryActionsEvent\""));
        NUnit.Framework.Assert.That(inventoryActionSource, NUnit.Framework.Does.Contain("\"text\": \"重要にする\""));
        NUnit.Framework.Assert.That(inventoryActionSource, NUnit.Framework.Does.Contain("\"key\": \"add notes\""));
        NUnit.Framework.Assert.That(inventoryActionSource, NUnit.Framework.Does.Contain("\"text\": \"メモを追加\""));
        NUnit.Framework.Assert.That(inventoryActionSource, NUnit.Framework.Does.Contain("\"key\": \"remove\""));
        NUnit.Framework.Assert.That(inventoryActionSource, NUnit.Framework.Does.Contain("\"text\": \"外す\""));
        NUnit.Framework.Assert.That(inventoryActionSource, NUnit.Framework.Does.Not.Contain("\"key\": \"drop\""));
        NUnit.Framework.Assert.That(inventoryActionSource, NUnit.Framework.Does.Not.Contain("\"key\": \"detonate\""));

        var commonMenuActionPath = Path.Combine(
            TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Localization",
            "Dictionaries",
            "Scoped",
            "ui-menu-actions.ja.json");
        var commonMenuActionSource = File.ReadAllText(commonMenuActionPath);

        NUnit.Framework.Assert.That(commonMenuActionSource, NUnit.Framework.Does.Contain("\"key\": \"drop\""));
        NUnit.Framework.Assert.That(commonMenuActionSource, NUnit.Framework.Does.Contain("\"text\": \"落とす\""));
        NUnit.Framework.Assert.That(commonMenuActionSource, NUnit.Framework.Does.Contain("\"key\": \"detonate\""));
        NUnit.Framework.Assert.That(commonMenuActionSource, NUnit.Framework.Does.Contain("\"text\": \"起爆する\""));

        var qudMenuItemPath = Path.Combine(
            TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Localization",
            "Dictionaries",
            "Scoped",
            "ui-popup-qud-menu-item.ja.json");
        var qudMenuItemSource = File.ReadAllText(qudMenuItemPath);

        NUnit.Framework.Assert.That(qudMenuItemSource, NUnit.Framework.Does.Not.Contain("\"key\": \"mark important\""));
        NUnit.Framework.Assert.That(qudMenuItemSource, NUnit.Framework.Does.Not.Contain("\"key\": \"add notes\""));
        NUnit.Framework.Assert.That(qudMenuItemSource, NUnit.Framework.Does.Not.Contain("\"key\": \"remove\""));
        NUnit.Framework.Assert.That(qudMenuItemSource, NUnit.Framework.Does.Not.Contain("\"key\": \"drop\""));
        NUnit.Framework.Assert.That(qudMenuItemSource, NUnit.Framework.Does.Not.Contain("\"key\": \"detonate\""));
    }

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
    public void InventoryLineTranslationPatch_RefreshesFallbackFontAfterFinalItemText()
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
            NUnit.Framework.Does.Contain("InventoryLineFontFixer.TryRefreshTextSkinWithFallbackFont(itemTextSkin, translatedDisplayName)"));
    }

    [NUnit.Framework.Test]
    public void InventoryLineTranslationPatch_LogsOriginalTmpLifecycleAroundOwnerTextAndFontRefresh()
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
            NUnit.Framework.Does.Contain("InventoryLineTmpLifecycleObservability.LogOriginalTmpLifecycle("));
        NUnit.Framework.Assert.That(
            source,
            NUnit.Framework.Does.Contain("\"translation-after-owner-set\""));
        NUnit.Framework.Assert.That(
            source,
            NUnit.Framework.Does.Contain("\"translation-after-font-refresh\""));

        var afterOwnerIndex = source.IndexOf(
            "\"translation-after-owner-set\"",
            System.StringComparison.Ordinal);
        var ownerSetIndex = source.LastIndexOf(
            "OwnerTextSetter.SetTranslatedText(",
            afterOwnerIndex,
            System.StringComparison.Ordinal);
        var fontRefreshIndex = source.IndexOf(
            "InventoryLineFontFixer.TryRefreshTextSkinWithFallbackFont(itemTextSkin, translatedDisplayName)",
            System.StringComparison.Ordinal);
        var afterRefreshIndex = source.IndexOf(
            "\"translation-after-font-refresh\"",
            System.StringComparison.Ordinal);

        NUnit.Framework.Assert.That(afterOwnerIndex, NUnit.Framework.Is.GreaterThan(ownerSetIndex));
        NUnit.Framework.Assert.That(fontRefreshIndex, NUnit.Framework.Is.GreaterThan(afterOwnerIndex));
        NUnit.Framework.Assert.That(afterRefreshIndex, NUnit.Framework.Is.GreaterThan(fontRefreshIndex));
    }

    [NUnit.Framework.Test]
    public void InventoryLineRenderProbePatch_LogsOriginalTmpLifecycleAroundSetDataFontRefresh()
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
            NUnit.Framework.Does.Contain("InventoryLineTmpLifecycleObservability.LogOriginalTmpLifecycle("));
        NUnit.Framework.Assert.That(
            source,
            NUnit.Framework.Does.Contain("\"setData-postfix-before-font-refresh\""));
        NUnit.Framework.Assert.That(
            source,
            NUnit.Framework.Does.Contain("\"setData-postfix-after-font-refresh\""));
        NUnit.Framework.Assert.That(
            source,
            NUnit.Framework.Does.Contain("forceMesh: false"));
        NUnit.Framework.Assert.That(
            source,
            NUnit.Framework.Does.Not.Contain("forceMesh: true"));
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
    public void InventoryLineActiveTextRefreshPatch_LogsRefreshDecisionProbe()
    {
        var sourcePath = Path.Combine(
            TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Assemblies",
            "src",
            "Patches",
            "InventoryLineActiveTextRefreshPatch.cs");
        var source = File.ReadAllText(sourcePath);

        NUnit.Framework.Assert.That(
            source,
            NUnit.Framework.Does.Contain("InventoryLineTmpLifecycleObservability.LogActiveRefreshDecision("));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("isActiveItemLine"));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("hasActiveReplacement"));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("refreshSucceeded"));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("scheduledRepair"));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("RuntimeDiagnostics.VerboseProbesEnabled"));
    }

    [NUnit.Framework.Test]
    public void InventoryLineTmpLifecycleObservability_ProvidesDevOnlyOriginalTmpAndDecisionProbes()
    {
        var sourcePath = Path.Combine(
            TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Assemblies",
            "src",
            "Observability",
            "InventoryLineTmpLifecycleObservability.cs");

        NUnit.Framework.Assert.That(File.Exists(sourcePath), NUnit.Framework.Is.True);
        var source = File.ReadAllText(sourcePath);

        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("#if HAS_TMP"));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("#if QUDJP_DEV_BUILD"));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("#if QUDJP_HAS_TMP_DEV_BUILD"));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("\"[QudJP] InventoryLineOriginalTmpLifecycle/v1: \""));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("\"[QudJP] InventoryLineActiveRefreshDecision/v1: \""));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("TextMeshProUGUI"));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Not.Contain("ForceMeshUpdate("));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("RuntimeDiagnostics.LogVerboseProbe"));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("activeInHierarchy"));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("canvasRenderer.cull"));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Not.Contain("DecisionLogCountsByLine.AddOrUpdate"));
    }

    [NUnit.Framework.Test]
    public void InventoryLineTmpLifecycleObservability_EscapesProbeTextAfterTruncating()
    {
        var sourcePath = Path.Combine(
            TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Assemblies",
            "src",
            "Observability",
            "InventoryLineTmpLifecycleObservability.cs");
        var source = File.ReadAllText(sourcePath);

        var truncateIndex = source.IndexOf(
            "var truncated = value!.Length <= 96",
            System.StringComparison.Ordinal);
        var escapeIndex = source.IndexOf(
            "return truncated.Replace(\"\\\\\", \"\\\\\\\\\")",
            System.StringComparison.Ordinal);

        NUnit.Framework.Assert.That(truncateIndex, NUnit.Framework.Is.GreaterThanOrEqualTo(0));
        NUnit.Framework.Assert.That(escapeIndex, NUnit.Framework.Is.GreaterThan(truncateIndex));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain(".Replace(\"'\", \"\\\\'\")"));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain(".Replace(\"\\r\", \"\\\\r\")"));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain(".Replace(\"\\n\", \"\\\\n\")"));
    }

    [NUnit.Framework.Test]
    public void InventoryAndEquipmentStatusScreenShowRepairPatch_SchedulesVisibleProbeAfterFirstShow()
    {
        var sourcePath = Path.Combine(
            TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Assemblies",
            "src",
            "Patches",
            "InventoryAndEquipmentStatusScreenShowRepairPatch.cs");

        NUnit.Framework.Assert.That(File.Exists(sourcePath), NUnit.Framework.Is.True);
        var source = File.ReadAllText(sourcePath);

        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("\"ShowScreen\""));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("Postfix(object? __instance)"));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("#if HAS_TMP && QUDJP_DEV_BUILD"));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("DelayedInventoryLineRepairScheduler.ScheduleVisibleInventoryProbeSnapshotsAfterDelay(__instance)"));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Not.Contain("ScheduleVisibleInventoryRepairsAfterDelay"));
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
    public void DelayedInventoryLineRepairScheduler_RearmsVisibleRowsAfterDelayedScreenScan()
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
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("ScheduleVisibleInventoryProbeSnapshotsAfterDelay"));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("RunVisibleInventoryProbeScan"));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("GetComponentsInChildren<Component>(includeInactive: true)"));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Not.Contain("Resources.FindObjectsOfTypeAll<Component>()"));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("new WaitForEndOfFrame()"));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Not.Contain("ScheduleRepair(component, resetAttempts: true)"));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("visibleProbeScanScheduled"));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("Interlocked.Exchange(ref visibleProbeScanScheduled, 1)"));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("\"InventoryLineVisibleRepairScan/v1\""));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("InventoryLineTmpLifecycleObservability.LogOriginalTmpLifecycle("));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("\"visible-scan-candidate\""));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("\"repair-before-replacement\""));
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
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("RuntimeDiagnostics.LogVerboseProbe(() => logLine!)"));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Not.Contain("QudJPMod.LogToUnity("));

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

    [NUnit.Framework.Test]
    public void TextShellReplacementRenderer_ProvidesOriginalReplacementComparisonProbe()
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

        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("InventoryLineOriginalReplacementComparison/v1"));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("TryBuildOriginalReplacementComparison("));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("original={"));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("replacement={"));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("componentState={"));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("textInfoState={"));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("meshState={"));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("layoutState={"));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("materialState={"));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("canvasState={"));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("subMeshCount="));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("InternalFlags"));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("LogProbeIfPresent(comparisonLogLine)"));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("ComparisonProbeLogged.ContainsKey(key)"));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("return ComparisonProbeLogged.TryAdd(key, 0);"));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("comparison probe build failed"));
    }

    [NUnit.Framework.Test]
    public void TextShellReplacementRenderer_EscapesComparisonProbeTextAfterTruncating()
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

        var truncateIndex = source.IndexOf(
            "var truncated = value!.Length <= 160",
            System.StringComparison.Ordinal);
        var escapeIndex = source.IndexOf(
            "return truncated.Replace(\"\\\\\", \"\\\\\\\\\")",
            System.StringComparison.Ordinal);

        NUnit.Framework.Assert.That(truncateIndex, NUnit.Framework.Is.GreaterThanOrEqualTo(0));
        NUnit.Framework.Assert.That(escapeIndex, NUnit.Framework.Is.GreaterThan(truncateIndex));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain(".Replace(\"'\", \"\\\\'\")"));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain(".Replace(\"\\r\", \"\\\\r\")"));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain(".Replace(\"\\n\", \"\\\\n\")"));
    }
}
