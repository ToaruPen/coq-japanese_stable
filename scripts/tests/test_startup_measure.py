"""Tests for startup measurement tooling."""

from __future__ import annotations

import json
import sys
import time
from pathlib import Path

import pytest

from scripts import startup_measure
from scripts.startup_measure import (
    IterationMetadata,
    IterationResult,
    PhaseSummary,
    ProfileSummary,
    _resolve_ready_marker,
    _restore_mod,
    _wait_for_ready_marker,
    build_patch_surface_inventory,
    compare_profiles,
    main,
    parse_startup_log_text,
    summarize_iterations,
)


def test_parse_startup_log_text_extracts_timing_markers() -> None:
    """StartupTiming lines are parsed with escaped detail fields."""
    log = (
        "[QudJP] Build marker: marker\n"
        "[QudJP] StartupTiming/v1: phase=harmony.prepare_patch_types elapsed_ms=12.346 "
        r"detail=patch_types\=140\;\ prepared\=139\;\ skipped\=1"
        "\n[QudJP] StartupTiming/v1: phase=harmony.apply_patch_types elapsed_ms=100.500 "
        r"detail=patch_types\=140\;applied\=139\;skipped\=1"
        "\nINFO - Finished 'Loading Naming.xml' task in 42ms"
        "\n[QudJP] Harmony patching complete: 590 method(s) patched."
    )

    parsed = parse_startup_log_text(log)

    assert parsed.build_marker_seen is True
    assert parsed.harmony_complete_seen is True
    assert parsed.harmony_patched_methods == 590
    assert len(parsed.timings) == 3
    assert parsed.timings[0].phase == "harmony.prepare_patch_types"
    assert parsed.timings[0].elapsed_ms == 12.346
    assert parsed.timings[0].detail == "patch_types=140; prepared=139; skipped=1"
    assert parsed.timings[2].phase == "game.loading.naming_xml"
    assert parsed.timings[2].elapsed_ms == 42.0
    assert parsed.timings[2].detail == "Naming.xml"
    metric_names = [metric.name for metric in parsed.metrics]
    assert len(metric_names) == len(set(metric_names))
    assert {metric.name: metric.value for metric in parsed.metrics} == {
        "harmony.prepare_patch_types.patch_types": 140,
        "harmony.prepare_patch_types.prepared": 139,
        "harmony.prepare_patch_types.skipped": 1,
        "harmony.apply_patch_types.patch_types": 140,
        "harmony.apply_patch_types.applied": 139,
        "harmony.apply_patch_types.skipped": 1,
        "harmony.patched_methods": 590,
    }


def test_summarize_iterations_groups_by_profile_and_phase() -> None:
    """Iteration summaries report per-profile median and ready counts."""
    results = [
        _result("enabled", 1, "ready", {"font.initialize": 100.0, "harmony.setup_and_apply": 20.0}),
        _result("enabled", 2, "ready", {"font.initialize": 120.0, "harmony.setup_and_apply": 30.0}),
        _result("disabled", 1, "ready", {"bootstrap.total": 50.0}),
    ]

    summaries = summarize_iterations(results)
    enabled = next(summary for summary in summaries if summary.profile == "enabled")
    font = next(phase for phase in enabled.phases if phase.phase == "font.initialize")
    runner = next(phase for phase in enabled.phases if phase.phase == "runner.elapsed_until_ready")
    patched_methods = next(metric for metric in enabled.metrics if metric.metric == "harmony.patched_methods")

    assert enabled.iterations == 2
    assert enabled.ready_iterations == 2
    assert font.count == 2
    assert font.median_ms == 110.0
    assert font.mean_ms == 110.0
    assert runner.count == 2
    assert runner.median_ms == 1000.0
    assert patched_methods.count == 2
    assert patched_methods.median == 590.0


def test_compare_profiles_reports_median_delta() -> None:
    """Profile comparisons use shared phase medians."""
    summaries = (
        ProfileSummary(
            profile="disabled",
            iterations=1,
            ready_iterations=1,
            phases=(PhaseSummary("bootstrap.total", 1, 50.0, 50.0, 50.0, 50.0),),
            metrics=(),
        ),
        ProfileSummary(
            profile="enabled",
            iterations=1,
            ready_iterations=1,
            phases=(PhaseSummary("bootstrap.total", 1, 80.0, 80.0, 80.0, 80.0),),
            metrics=(),
        ),
    )

    comparisons = compare_profiles(summaries, baseline_profile="disabled", candidate_profile="enabled")

    assert len(comparisons) == 1
    assert comparisons[0].phase == "bootstrap.total"
    assert comparisons[0].delta_median_ms == 30.0


def test_parse_cli_reads_preserved_iteration_artifacts(tmp_path: Path) -> None:
    """The parse command summarizes saved Player.log and metadata files."""
    iteration_dir = tmp_path / "profiles" / "enabled" / "iteration-01"
    iteration_dir.mkdir(parents=True)
    (iteration_dir / "metadata.json").write_text(
        json.dumps(
            {
                "profile": "enabled",
                "iteration": 1,
                "command": ["scripts/launch_rosetta.sh"],
                "started_at": "2026-05-09T00:00:00+00:00",
                "status": "ready",
                "elapsed_until_ready_ms": 1000.0,
                "exit_code": 0,
            },
        ),
        encoding="utf-8",
    )
    (iteration_dir / "Player.log").write_text(
        "[QudJP] StartupTiming/v1: phase=font.initialize elapsed_ms=25\n",
        encoding="utf-8",
    )
    output = tmp_path / "summary.json"
    markdown = tmp_path / "summary.md"

    exit_code = main(["parse", "--input-dir", str(tmp_path), "--output", str(output), "--markdown", str(markdown)])

    summary = json.loads(output.read_text(encoding="utf-8"))
    assert exit_code == 0
    assert summary[0]["profile"] == "enabled"
    assert summary[0]["phases"][0]["phase"] == "font.initialize"
    assert markdown.read_text(encoding="utf-8").startswith("# Startup Measurement Summary")


def test_top_cli_reports_highest_matching_phases(tmp_path: Path) -> None:
    """The top command ranks matching phase medians per profile."""
    summary = [
        {
            "profile": "enabled",
            "iterations": 1,
            "ready_iterations": 1,
            "phases": [
                {
                    "phase": "harmony.patch_apply.FastPatch",
                    "count": 1,
                    "mean_ms": 5.0,
                    "median_ms": 5.0,
                    "min_ms": 5.0,
                    "max_ms": 5.0,
                },
                {
                    "phase": "harmony.patch_apply.SlowPatch",
                    "count": 1,
                    "mean_ms": 50.0,
                    "median_ms": 50.0,
                    "min_ms": 50.0,
                    "max_ms": 50.0,
                },
                {
                    "phase": "font.initialize",
                    "count": 1,
                    "mean_ms": 100.0,
                    "median_ms": 100.0,
                    "min_ms": 100.0,
                    "max_ms": 100.0,
                },
            ],
        },
    ]
    summary_path = tmp_path / "summary.json"
    output = tmp_path / "top.json"
    markdown = tmp_path / "top.md"
    summary_path.write_text(json.dumps(summary), encoding="utf-8")

    exit_code = main(
        [
            "top",
            "--summary",
            str(summary_path),
            "--prefix",
            "harmony.patch_apply.",
            "--limit",
            "1",
            "--output",
            str(output),
            "--markdown",
            str(markdown),
        ],
    )

    top = json.loads(output.read_text(encoding="utf-8"))
    assert exit_code == 0
    assert top["enabled"][0]["phase"] == "harmony.patch_apply.SlowPatch"
    assert "SlowPatch" in markdown.read_text(encoding="utf-8")


def test_build_patch_surface_inventory_groups_harmony_patch_files(tmp_path: Path) -> None:
    """Static patch inventory reports Harmony shape and semantic groups."""
    patch_root = tmp_path / "Patches"
    patch_root.mkdir()
    (patch_root / "MessagePopupPatch.cs").write_text(
        """
using HarmonyLib;

[HarmonyPatch]
public static class MessagePopupPatch
{
    [HarmonyTargetMethods]
    public static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method("XRL.Messages.MessageQueue:AddPlayerMessage");
        yield return AccessTools.Method("XRL.UI.Popup:Show");
    }

    public static bool Prefix(ref string Message)
    {
        return true;
    }
}
""",
        encoding="utf-8",
    )
    (patch_root / "UiDisplayPatch.cs").write_text(
        """
using HarmonyLib;

[HarmonyPatch]
public static class UiDisplayPatch
{
    [HarmonyTargetMethod]
    public static MethodBase TargetMethod()
    {
        return AccessTools.Method("SomeLine:setData");
    }

    public static void Postfix(ref string __result)
    {
        _ = GetDisplayNameRouteTranslator.TranslatePreservingColors(__result, nameof(UiDisplayPatch));
        _ = Grammar.MakeAndList(new[] { __result });
        text.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => instructions;
    public static Exception? Finalizer(Exception? __exception) => __exception;
}
""",
        encoding="utf-8",
    )
    (patch_root / "ModMenuLineTranslationPatch.cs").write_text(
        """
using HarmonyLib;

[HarmonyPatch]
public static class ModMenuLineTranslationPatch
{
    [HarmonyTargetMethod]
    public static MethodBase TargetMethod()
    {
        return AccessTools.Method("Qud.UI.ModMenuLine:Update");
    }

    public static void Postfix() {}
}
""",
        encoding="utf-8",
    )
    (patch_root / "SteamWorkshopUploaderViewTranslationPatch.cs").write_text(
        """
using HarmonyLib;

[HarmonyPatch]
public static class SteamWorkshopUploaderViewTranslationPatch
{
    [HarmonyTargetMethods]
    public static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method("SteamWorkshopUploaderView:Popup");
    }

    public static void Prefix(object[] args) {}
}
""",
        encoding="utf-8",
    )

    inventory = build_patch_surface_inventory(patch_root)
    groups = {group.group: group for group in inventory.groups}
    functional_families = {group.group: group for group in inventory.functional_families}

    assert inventory.patch_files == 4
    assert inventory.source_files == 4
    assert inventory.harmony_patch_files == 4
    assert inventory.harmony_patch_attributes == 4
    assert inventory.harmony_target_method_attributes == 2
    assert inventory.harmony_target_methods_attributes == 2
    assert inventory.prefix_methods == 2
    assert inventory.postfix_methods == 2
    assert inventory.transpiler_methods == 1
    assert inventory.finalizer_methods == 1
    assert inventory.protected_files == 0
    assert groups["message_queue"].file_count == 1
    assert groups["popup"].file_count == 2
    assert groups["mod_management"].file_count == 2
    assert groups["ui_set_data"].file_count == 1
    assert groups["display_name"].file_count == 1
    assert groups["grammar"].file_count == 1
    assert groups["font_mesh"].file_count == 1
    assert functional_families["message_queue_semantic_pipeline"].file_count == 1
    assert functional_families["mod_management_ui"].file_count == 2
    assert functional_families["ui_line_set_data"].protected_file_count == 0
    ui_file = next(file for file in inventory.files if file.path == "UiDisplayPatch.cs")
    assert ui_file.source_kind == "harmony_patch"
    assert ui_file.is_harmony_patch is True
    assert ui_file.functional_family == "ui_line_set_data"
    assert ui_file.touch_policy == "editable"
    assert ui_file.protected is False
    assert ui_file.protection_reasons == ()
    assert "display_name_route" in ui_file.semantic_groups
    mod_file = next(file for file in inventory.files if file.path == "ModMenuLineTranslationPatch.cs")
    assert mod_file.functional_family == "mod_management_ui"
    assert mod_file.touch_policy == "editable"
    workshop_file = next(
        file for file in inventory.files if file.path == "SteamWorkshopUploaderViewTranslationPatch.cs"
    )
    assert workshop_file.functional_family == "mod_management_ui"
    assert "popup_pipeline" in workshop_file.semantic_groups


def test_build_patch_surface_inventory_classifies_large_producer_families(tmp_path: Path) -> None:
    """Broad producer routes are grouped before falling back to generic producer_patch."""
    patch_root = tmp_path / "Patches"
    patch_root.mkdir()
    for filename, text in {
        "CharGenLocalizationPatch.cs": (
            'CharGenProducerTranslationHelpers.TranslateStringMember(data, "Title", Context);'
        ),
        "JournalObservationAddTranslationPatch.cs": "JournalTextTranslator.TranslateObservation(text);",
        "ZoneManagerSetActiveZoneTranslationPatch.cs": "ZoneDisplayNameTranslationCatalog.Translate(zone);",
        "JournalLineTranslationPatch.cs": "GetDisplayNameRouteTranslator.TranslatePreservingColors(text, Context);",
        "ModManagementSemanticPipeline.cs": '"{{W|Update Available}}" => "{{W|更新あり}}"',
        "ConversationDisplayTextPatch.cs": "ConversationTemplateTranslator.Translate(text);",
        "DescriptionShortDescriptionPatch.cs": "DescriptionTextTranslator.TranslateShortDescription(text);",
        "GameObjectMoveTranslationPatch.cs": "GameObject.Move();",
        "AbilityManagerLineTranslationPatch.cs": "AbilityManagerLine setData ability text;",
        "InventoryScreenTranslationPatch.cs": "InventoryScreen.Show();",
        "QuestLogTranslationPatch.cs": "QuestLog.Show();",
        "FactionsLineTranslationPatch.cs": "FactionsLine.setData(data);",
        "SkillsAndPowersScreenTranslationPatch.cs": "SkillsAndPowersScreen.Show();",
        "TinkeringStatusScreenTranslationPatch.cs": "TinkeringStatusScreen.Show();",
        "CyberneticsTerminalScreenTranslationPatch.cs": "CyberneticsTerminalScreen.Show();",
        "TutorialManagerHighlightTranslationPatch.cs": "TutorialManager.Highlight();",
        "GameSummaryScreenShowTranslationPatch.cs": "GameSummaryScreen.Show();",
        "SavesApiReadSaveJsonTranslationPatch.cs": "SavesApi.ReadSaveJson();",
        "BookScreenTranslationPatch.cs": "BookScreen.showScreen();",
        "OptionsLocalizationPatch.cs": "OptionsLocalizationPatch.Translate();",
        "HistoricStringExpanderPatch.cs": "HistoricStringExpander.Expand();",
    }.items():
        (patch_root / filename).write_text(text, encoding="utf-8")

    inventory = build_patch_surface_inventory(patch_root)
    by_path = {file.path: file for file in inventory.files}

    assert by_path["CharGenLocalizationPatch.cs"].functional_family == "chargen_producer"
    assert by_path["JournalObservationAddTranslationPatch.cs"].functional_family == "journal_producer"
    assert by_path["ZoneManagerSetActiveZoneTranslationPatch.cs"].functional_family == "zone_world_producer"
    assert by_path["JournalLineTranslationPatch.cs"].functional_family == "display_name_route"
    assert "runtime_ui_text" not in by_path["ModManagementSemanticPipeline.cs"].semantic_groups
    assert by_path["ConversationDisplayTextPatch.cs"].functional_family == "conversation_text"
    assert by_path["DescriptionShortDescriptionPatch.cs"].functional_family == "description_effect_text"
    assert by_path["GameObjectMoveTranslationPatch.cs"].functional_family == "game_event_text"
    assert by_path["AbilityManagerLineTranslationPatch.cs"].functional_family == "ability_ui"
    assert by_path["InventoryScreenTranslationPatch.cs"].functional_family == "inventory_equipment_ui"
    assert by_path["QuestLogTranslationPatch.cs"].functional_family == "quest_ui"
    assert by_path["FactionsLineTranslationPatch.cs"].functional_family == "factions_ui"
    assert by_path["SkillsAndPowersScreenTranslationPatch.cs"].functional_family == "skills_powers_ui"
    assert by_path["TinkeringStatusScreenTranslationPatch.cs"].functional_family == "tinkering_ui"
    assert by_path["CyberneticsTerminalScreenTranslationPatch.cs"].functional_family == "cybernetics_ui"
    assert by_path["TutorialManagerHighlightTranslationPatch.cs"].functional_family == "tutorial_ui"
    assert by_path["GameSummaryScreenShowTranslationPatch.cs"].functional_family == "game_summary_ui"
    assert by_path["SavesApiReadSaveJsonTranslationPatch.cs"].functional_family == "save_ui"
    assert by_path["BookScreenTranslationPatch.cs"].functional_family == "book_help_ui"
    assert by_path["OptionsLocalizationPatch.cs"].functional_family == "menu_options_ui"
    assert by_path["HistoricStringExpanderPatch.cs"].functional_family == "lore_history_text"


def test_build_patch_surface_inventory_counts_group_markers_without_substring_overlap(tmp_path: Path) -> None:
    """Group marker counts use non-overlapping patterns for route-like names."""
    patch_root = tmp_path / "Patches"
    patch_root.mkdir()
    (patch_root / "PopupPatch.cs").write_text(
        """
using HarmonyLib;

[HarmonyPatch]
public static class PopupPatch
{
    [HarmonyTargetMethod]
    public static MethodBase TargetMethod() => AccessTools.Method("XRL.UI.Popup:Show");
    public static void Prefix(ref string Message) {}
}
""",
        encoding="utf-8",
    )
    (patch_root / "GetDisplayNamePatch.cs").write_text(
        """
public static class GetDisplayNamePatch
{
    public static string Translate(string source) =>
        GetDisplayNameRouteTranslator.TranslatePreservingColors(source, nameof(GetDisplayNamePatch));
}
""",
        encoding="utf-8",
    )
    (patch_root / "GrammarPatch.cs").write_text(
        """
public static class GrammarPatch
{
    public static string Translate(string source) => GrammarPatchHelpers.BuildJapaneseList(new[] { source }, "と");
}
""",
        encoding="utf-8",
    )

    inventory = build_patch_surface_inventory(patch_root)
    groups = {group.group: group for group in inventory.groups}

    assert groups["popup"].marker_count == 2
    assert groups["display_name"].marker_count == 3
    assert groups["grammar"].marker_count == 2


def test_build_patch_surface_inventory_marks_tmp_font_files_no_touch(tmp_path: Path) -> None:
    """TMP/font lifecycle files are classified but protected from implementation work."""
    patch_root = tmp_path / "Patches"
    patch_root.mkdir()
    (patch_root / "TextMeshProUguiFontPatch.cs").write_text(
        """
using HarmonyLib;

[HarmonyPatch]
public static class TextMeshProUguiFontPatch
{
    [HarmonyTargetMethod]
    public static MethodBase TargetMethod() => AccessTools.Method("TextMeshProUGUI:OnEnable");
    public static void Postfix(object __instance) => FontManager.ApplyToText(__instance);
}
""",
        encoding="utf-8",
    )

    inventory = build_patch_surface_inventory(patch_root)
    file = inventory.files[0]

    assert inventory.protected_files == 1
    assert file.source_kind == "harmony_patch"
    assert file.functional_family == "producer_patch"
    assert file.touch_policy == "no_touch"
    assert file.protected is True
    assert file.protection_reasons == ("tmp_font_mesh", "font_patch")


def test_build_patch_surface_inventory_keeps_helper_source_kind_separate(tmp_path: Path) -> None:
    """Helper files keep source_kind=helper even when they reference patch targets."""
    patch_root = tmp_path / "Patches"
    patch_root.mkdir()
    (patch_root / "MessageQueueSemanticPipeline.cs").write_text(
        """
internal static class MessageQueueSemanticPipeline
{
    public static bool TryTranslateQueuedMessage(ref string message, string? color)
    {
        return ZoneManagerTickTranslationPatch.TryTranslateQueuedMessage(ref message, color)
            || ZoneManagerGenerateZoneTranslationPatch.TryTranslateQueuedMessage(ref message, color);
    }
}
""",
        encoding="utf-8",
    )

    inventory = build_patch_surface_inventory(patch_root)
    file = inventory.files[0]

    assert inventory.harmony_patch_files == 0
    assert file.source_kind == "helper"
    assert file.is_harmony_patch is False


def test_real_patch_inventory_marks_known_tmp_font_files_no_touch() -> None:
    """The known TMP/font patch allowlist remains no-touch."""
    project_root = Path(startup_measure.__file__).resolve().parents[1]
    patch_root = project_root / "Mods/QudJP/Assemblies/src/Patches"

    inventory = build_patch_surface_inventory(patch_root)
    protected_paths = {file.path for file in inventory.files if file.touch_policy == "no_touch"}

    assert protected_paths == {
        "BaseLineWithTooltipStartTooltipPatch.cs",
        "EquipmentLineRenderProbePatch.cs",
        "GameSummaryScreenMenuBarsTranslationPatch.cs",
        "InventoryAndEquipmentStatusScreenShowRepairPatch.cs",
        "InventoryLineActiveTextRefreshPatch.cs",
        "InventoryLineRenderProbePatch.cs",
        "InventoryLineTranslationPatch.cs",
        "LegacyUITextFontPatch.cs",
        "LookTooltipContentPatch.cs",
        "TextMeshProFontPatch.cs",
        "TextMeshProUguiFontPatch.cs",
        "TmpInputFieldFontPatch.cs",
        "TooltipDisplayVisibilityPatch.cs",
        "TooltipManagerSetTextAndSizePatch.cs",
    }


def test_patch_inventory_cli_writes_json_and_markdown(tmp_path: Path) -> None:
    """The patch-inventory command writes reviewable static patch surface artifacts."""
    patch_root = tmp_path / "Patches"
    patch_root.mkdir()
    (patch_root / "MessagePatch.cs").write_text(
        """
using HarmonyLib;

[HarmonyPatch]
public static class MessagePatch
{
    [HarmonyTargetMethod]
    public static MethodBase TargetMethod() => AccessTools.Method("XRL.Messages.MessageQueue:AddPlayerMessage");
    public static void Prefix(ref string Message) {}
}
""",
        encoding="utf-8",
    )
    output = tmp_path / "inventory.json"
    markdown = tmp_path / "inventory.md"

    exit_code = main(
        [
            "patch-inventory",
            "--patch-root",
            str(patch_root),
            "--output",
            str(output),
            "--markdown",
            str(markdown),
        ],
    )

    inventory = json.loads(output.read_text(encoding="utf-8"))
    markdown_text = markdown.read_text(encoding="utf-8")
    assert exit_code == 0
    assert inventory["patch_files"] == 1
    assert inventory["source_files"] == 1
    assert inventory["harmony_patch_files"] == 1
    assert inventory["functional_families"][0]["group"] == "message_queue_semantic_pipeline"
    assert inventory["groups"][0]["group"] == "message_queue"
    assert markdown_text.startswith("# Harmony Patch Surface Inventory")
    assert "- Harmony patch files: 1" in markdown_text
    assert "| message_queue_semantic_pipeline | 1 | 0 |" in markdown_text
    assert "| message_queue | 1 | 0 |" in markdown_text


def test_patch_inventory_cli_default_patch_root_is_repo_relative(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    """The default patch root is stable even when the command runs outside the repo."""
    output = tmp_path / "inventory.json"
    monkeypatch.chdir(tmp_path)

    exit_code = main(["patch-inventory", "--output", str(output)])

    inventory = json.loads(output.read_text(encoding="utf-8"))
    assert exit_code == 0
    assert inventory["patch_files"] > 0
    assert inventory["harmony_patch_files"] > 0
    assert inventory["patch_root"] == "Mods/QudJP/Assemblies/src/Patches"


def test_patch_inventory_cli_rejects_missing_patch_root(tmp_path: Path) -> None:
    """A missing patch root is a hard failure instead of an empty inventory."""
    output = tmp_path / "inventory.json"

    with pytest.raises(FileNotFoundError):
        main(
            [
                "patch-inventory",
                "--patch-root",
                str(tmp_path / "missing"),
                "--output",
                str(output),
            ],
        )

    assert not output.exists()


def test_restore_mod_refuses_to_delete_recreated_destination(tmp_path: Path) -> None:
    """Restoring a disabled mod never removes a concurrently recreated destination."""
    mod_dir = tmp_path / "Mods" / "QudJP"
    disabled_dir = tmp_path / ".qudjp-startup-measure-disabled" / "QudJP"
    mod_dir.mkdir(parents=True)
    disabled_dir.mkdir(parents=True)
    (mod_dir / "new-file.txt").write_text("new", encoding="utf-8")
    (disabled_dir / "original-file.txt").write_text("original", encoding="utf-8")

    with pytest.raises(FileExistsError, match="Refusing to restore disabled mod"):
        _restore_mod(mod_dir, disabled_dir)

    assert (mod_dir / "new-file.txt").read_text(encoding="utf-8") == "new"
    assert (disabled_dir / "original-file.txt").read_text(encoding="utf-8") == "original"


def test_disable_mod_requires_explicit_ready_marker() -> None:
    """A disabled QudJP profile cannot wait on QudJP's own completion marker."""
    with pytest.raises(ValueError, match="--disable-mod requires --ready-marker"):
        _resolve_ready_marker(disable_mod=True, ready_marker=None)


def test_wait_for_ready_marker_detects_zero_patch_warning(tmp_path: Path) -> None:
    """Harmony zero-patch logs are treated as failed startup evidence."""
    log_path = tmp_path / "Player.log"
    log_path.write_text("[QudJP] Warning: Harmony patched zero methods.\n", encoding="utf-8")

    status, elapsed = _wait_for_ready_marker(log_path, timeout_seconds=1, ready_marker="never appears")

    assert status == "harmony_failed"
    assert elapsed is not None


def test_wait_for_ready_marker_prefers_zero_patch_warning_over_ready_marker(tmp_path: Path) -> None:
    """Harmony failure markers take precedence over completion markers."""
    log_path = tmp_path / "Player.log"
    log_path.write_text(
        "[QudJP] Harmony patching complete: 0 method(s) patched.\n"
        "[QudJP] Warning: Harmony patched zero methods.\n",
        encoding="utf-8",
    )

    status, elapsed = _wait_for_ready_marker(
        log_path,
        timeout_seconds=1,
        ready_marker="[QudJP] Harmony patching complete:",
    )

    assert status == "harmony_failed"
    assert elapsed is not None


def test_collect_cli_writes_process_output_without_pipes(tmp_path: Path) -> None:
    """The collect command preserves child stdout/stderr via files."""
    log_path = tmp_path / "Player.log"
    launcher = tmp_path / "fake_launch.py"
    launcher.write_text(
        "from pathlib import Path\n"
        "import sys\n"
        f"Path({str(log_path)!r}).write_text('[QudJP] Harmony patching complete:\\n', encoding='utf-8')\n"
        "print('stdout marker')\n"
        "print('stderr marker', file=sys.stderr)\n",
        encoding="utf-8",
    )

    exit_code = main(
        [
            "collect",
            "--profile",
            "enabled",
            "--iterations",
            "1",
            "--artifact-dir",
            str(tmp_path / "artifacts"),
            "--launch-cmd",
            f"{sys.executable} {launcher}",
            "--log",
            str(log_path),
            "--timeout",
            "5",
        ],
    )

    iteration_dir = tmp_path / "artifacts" / "profiles" / "enabled" / "iteration-01"
    assert exit_code == 0
    assert (iteration_dir / "stdout.txt").read_text(encoding="utf-8").strip() == "stdout marker"
    assert (iteration_dir / "stderr.txt").read_text(encoding="utf-8").strip() == "stderr marker"


def test_collect_cli_without_disable_mod_does_not_resolve_default_destination(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    """The collect command only resolves the default mod directory when disabling QudJP."""
    log_path = tmp_path / "Player.log"
    launcher = tmp_path / "fake_launch.py"
    launcher.write_text(
        "from pathlib import Path\n"
        f"Path({str(log_path)!r}).write_text('[QudJP] Harmony patching complete:\\n', encoding='utf-8')\n",
        encoding="utf-8",
    )

    def fail_resolve_default_destination() -> Path:
        raise AssertionError

    monkeypatch.setattr(startup_measure, "resolve_default_destination", fail_resolve_default_destination)

    exit_code = main(
        [
            "collect",
            "--profile",
            "enabled",
            "--iterations",
            "1",
            "--artifact-dir",
            str(tmp_path / "artifacts"),
            "--launch-cmd",
            f"{sys.executable} {launcher}",
            "--log",
            str(log_path),
            "--timeout",
            "5",
        ],
    )

    assert exit_code == 0


def test_collect_cli_stops_waiting_after_launcher_exits(tmp_path: Path) -> None:
    """The collect command records fast launcher exits without waiting for timeout."""
    log_path = tmp_path / "Player.log"
    launcher = tmp_path / "fast_exit.py"
    launcher.write_text("import sys\nsys.exit(3)\n", encoding="utf-8")

    started = time.monotonic()
    exit_code = main(
        [
            "collect",
            "--profile",
            "enabled",
            "--iterations",
            "1",
            "--artifact-dir",
            str(tmp_path / "artifacts"),
            "--launch-cmd",
            f"{sys.executable} {launcher}",
            "--log",
            str(log_path),
            "--timeout",
            "1.25",
        ],
    )
    elapsed = time.monotonic() - started

    metadata_path = tmp_path / "artifacts" / "profiles" / "enabled" / "iteration-01" / "metadata.json"
    metadata = json.loads(metadata_path.read_text(encoding="utf-8"))
    assert exit_code == 0
    assert elapsed < 0.75
    assert metadata["status"] == "process_exited"
    assert metadata["exit_code"] == 3


def _result(profile: str, iteration: int, status: str, timings: dict[str, float]) -> IterationResult:
    parsed = parse_startup_log_text(
        "\n".join(
            f"[QudJP] StartupTiming/v1: phase={phase} elapsed_ms={elapsed}"
            for phase, elapsed in timings.items()
        )
        + "\n[QudJP] Harmony patching complete: 590 method(s) patched.",
    )
    return IterationResult(
        metadata=IterationMetadata(
            profile=profile,
            iteration=iteration,
            command=("scripts/launch_rosetta.sh",),
            started_at="2026-05-09T00:00:00+00:00",
            status=status,
            elapsed_until_ready_ms=1000.0,
            exit_code=0,
        ),
        parsed=parsed,
    )
