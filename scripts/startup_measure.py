"""Collect and summarize Caves of Qud startup timing logs."""

from __future__ import annotations

import argparse
import json
import re
import shlex
import shutil
import subprocess
import sys
import time
from dataclasses import asdict, dataclass
from datetime import UTC, datetime
from pathlib import Path
from statistics import mean, median
from typing import TYPE_CHECKING

_PROJECT_ROOT = Path(__file__).resolve().parents[1]
if str(_PROJECT_ROOT) not in sys.path:
    sys.path.insert(0, str(_PROJECT_ROOT))

from scripts.sync_mod import resolve_default_destination  # noqa: E402

if TYPE_CHECKING:
    from collections.abc import Iterable, Sequence

_DEFAULT_LOG = Path.home() / "Library" / "Logs" / "Freehold Games" / "CavesOfQud" / "Player.log"
_DEFAULT_ARTIFACT_DIR = Path(".sisyphus") / "evidence" / f"issue-604-{datetime.now(UTC):%Y%m%dT%H%M%SZ}"
_STARTUP_TIMING_MARKER = "[QudJP] StartupTiming/v1:"
_GAME_LOADING_TASK_PATTERN = re.compile(r"INFO - Finished 'Loading (?P<task>[^']+)' task in (?P<elapsed>\d+)ms")
_BUILD_MARKER = "[QudJP] Build marker:"
_HARMONY_COMPLETE_MARKER = "[QudJP] Harmony patching complete:"
_ERROR_MARKERS = (
    "MissingMethodException",
    "MODWARN",
    "[QudJP] Bootstrap failed",
    "[QudJP] FontManager failed",
)
_PATCH_GROUP_PATTERNS: tuple[tuple[str, re.Pattern[str]], ...] = (
    ("message_queue", re.compile(r"AddPlayerMessage|MessageQueue")),
    ("popup", re.compile(r"Popup(?::\w+|\.\w+)?")),
    ("mod_management", re.compile(r"Mod(?:Info|Management|MenuLine)|SteamWorkshopUploader")),
    ("chargen", re.compile(r"(?:CharGen|Chargen|EmbarkBuilder|CharacterBuilds)")),
    ("journal", re.compile(r"Journal(?!Line)")),
    ("zone_world", re.compile(r"(?:Zone(?:Manager|DisplayName|Wind)|World(?:Creation|Generation))")),
    ("conversation", re.compile(r"Conversation")),
    ("description_effect", re.compile(r"(?:Description|Effect|GivesRep|Cripple)")),
    (
        "game_event",
        re.compile(
            r"(?:GameObject|Combat|Physics|DeathReason|AsleepMessage|AutoAct|ExperienceAwardXp|"
            r"DoorAttemptOpen|PetEitherOrExplode|XrlCoreLostSight|StartReplace)",
        ),
    ),
    ("ability_ui", re.compile(r"(?:AbilityBar|AbilityManager|ActivatedAbility)")),
    ("inventory_equipment_ui", re.compile(r"(?:Inventory|Equipment|PickGameObject|PickItem|TradeLine|TradeScreen)")),
    ("quest_ui", re.compile(r"(?:QuestLog|Quests?|Quest)")),
    ("factions_ui", re.compile(r"Factions")),
    ("skills_powers_ui", re.compile(r"SkillsAndPowers")),
    ("tinkering_ui", re.compile(r"Tinkering")),
    ("cybernetics_ui", re.compile(r"Cybernetics")),
    ("tutorial_ui", re.compile(r"Tutorial")),
    ("game_summary_ui", re.compile(r"GameSummaryScreen")),
    ("save_ui", re.compile(r"(?:SavesApi|SaveManagement)")),
    ("book_help_ui", re.compile(r"(?:Book|Help|XRLManual|HighScores|StatisticGetHelpText)")),
    (
        "menu_options_ui",
        re.compile(
            r"(?:Accessibility|Options|MainMenu|Keybind|LoadingStatus|FilterBar|SaveManagement|"
            r"MessageLog|Achievement)",
        ),
    ),
    (
        "lore_history_text",
        re.compile(r"(?:Village|Historic|MarkovCorpus|BlueprintTemplate|DeployableInfrastructure|NameStyle)"),
    ),
    ("emit_message", re.compile(r"EmitMessage")),
    (
        "ui_update",
        re.compile(
            r'"(?:Update|LateUpdate|AfterRender|Tick|UpdateDescriptions|BeforeShow)"'
            r"|:[A-Za-z0-9_.]*(?:Update|LateUpdate|AfterRender|Tick)\b",
        ),
    ),
    ("ui_set_data", re.compile(r"setData")),
    ("display_name", re.compile(r"GetDisplayName(?:RouteTranslator|ProcessPatch|Patch)?")),
    ("grammar", re.compile(r"Grammar(?:Patch(?:Helpers)?|\.\w+)?")),
    ("font_mesh", re.compile(r"ForceMeshUpdate|TextMeshPro|TmpText|TMP")),
)
_PATCH_PRIMARY_GROUP_PRIORITY = (
    "message_queue_semantic_pipeline",
    "mod_management_ui",
    "popup_pipeline",
    "chargen_producer",
    "journal_producer",
    "zone_world_producer",
    "conversation_text",
    "description_effect_text",
    "game_event_text",
    "ability_ui",
    "inventory_equipment_ui",
    "quest_ui",
    "factions_ui",
    "skills_powers_ui",
    "tinkering_ui",
    "cybernetics_ui",
    "tutorial_ui",
    "game_summary_ui",
    "save_ui",
    "book_help_ui",
    "menu_options_ui",
    "lore_history_text",
    "runtime_ui_text",
    "ui_line_set_data",
    "display_name_route",
    "grammar_route",
    "emit_message_pipeline",
    "producer_patch",
    "support_helper",
)
_PATCH_TOUCH_POLICY_NO_TOUCH = "no_touch"
_PATCH_TOUCH_POLICY_EDITABLE = "editable"
_PATCH_NO_TOUCH_PATHS: frozenset[str] = frozenset(
    {
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
    },
)
_PATCH_RAW_TO_SEMANTIC_GROUP = {
    "message_queue": "message_queue_semantic_pipeline",
    "popup": "popup_pipeline",
    "mod_management": "mod_management_ui",
    "chargen": "chargen_producer",
    "journal": "journal_producer",
    "zone_world": "zone_world_producer",
    "conversation": "conversation_text",
    "description_effect": "description_effect_text",
    "game_event": "game_event_text",
    "ability_ui": "ability_ui",
    "inventory_equipment_ui": "inventory_equipment_ui",
    "quest_ui": "quest_ui",
    "factions_ui": "factions_ui",
    "skills_powers_ui": "skills_powers_ui",
    "tinkering_ui": "tinkering_ui",
    "cybernetics_ui": "cybernetics_ui",
    "tutorial_ui": "tutorial_ui",
    "game_summary_ui": "game_summary_ui",
    "save_ui": "save_ui",
    "book_help_ui": "book_help_ui",
    "menu_options_ui": "menu_options_ui",
    "lore_history_text": "lore_history_text",
    "ui_update": "runtime_ui_text",
    "ui_set_data": "ui_line_set_data",
    "display_name": "display_name_route",
    "grammar": "grammar_route",
    "emit_message": "emit_message_pipeline",
}
_PATCH_NO_TOUCH_REASON_PATTERNS: tuple[tuple[str, re.Pattern[str]], ...] = (
    ("tmp_font_mesh", re.compile(r"TMPro|TextMeshPro|TMP_|TMPInputField|TmpText|ForceMeshUpdate")),
    ("text_shell_renderer", re.compile(r"TextShellReplacementRenderer|TooltipReplacementRenderer")),
    ("tmp_repairer", re.compile(r"TmpTextRepairer|TooltipTextRepairer|[A-Za-z0-9_]*FontFixer")),
    ("inventory_line_active_refresh", re.compile(r"InventoryLineActiveTextRefreshPatch")),
    ("font_patch", re.compile(r"TextMeshProFontPatch|TextMeshProUguiFontPatch|LegacyUITextFontPatch")),
)
_HARMONY_PATCH_ATTRIBUTE_PATTERN = re.compile(r"\[HarmonyPatch(?:\]|\()")
_HARMONY_TARGET_METHOD_ATTRIBUTE_PATTERN = re.compile(r"\[HarmonyTargetMethod(?:\]|\()")
_HARMONY_TARGET_METHODS_ATTRIBUTE_PATTERN = re.compile(r"\[HarmonyTargetMethods(?:\]|\()")
_CLASS_NAME_PATTERN = re.compile(
    r"\b(?:public|internal|private)?\s*(?:static\s+)?class\s+(?P<class_name>[A-Za-z_][A-Za-z0-9_]*)",
)
_PATCH_METHOD_PATTERNS = {
    "prefix": re.compile(r"\b(?:public|private|internal)\s+static\s+[^{;=]+?\bPrefix\s*\("),
    "postfix": re.compile(r"\b(?:public|private|internal)\s+static\s+[^{;=]+?\bPostfix\s*\("),
    "transpiler": re.compile(r"\b(?:public|private|internal)\s+static\s+[^{;=]+?\bTranspiler\s*\("),
    "finalizer": re.compile(r"\b(?:public|private|internal)\s+static\s+[^{;=]+?\bFinalizer\s*\("),
}


@dataclass(frozen=True)
class StartupTimingEntry:
    """A single `StartupTiming/v1` timing entry from Player.log."""

    phase: str
    elapsed_ms: float
    detail: str | None
    line_number: int


@dataclass(frozen=True)
class ParsedStartupLog:
    """Structured timing and marker data extracted from one Player.log."""

    timings: tuple[StartupTimingEntry, ...]
    build_marker_seen: bool
    harmony_complete_seen: bool
    error_lines: tuple[str, ...]


@dataclass(frozen=True)
class IterationMetadata:
    """Runner-side metadata for one startup iteration."""

    profile: str
    iteration: int
    command: tuple[str, ...]
    started_at: str
    status: str
    elapsed_until_ready_ms: float | None
    exit_code: int | None


@dataclass(frozen=True)
class IterationResult:
    """Combined runner metadata and parsed log data for one iteration."""

    metadata: IterationMetadata
    parsed: ParsedStartupLog


@dataclass(frozen=True)
class PhaseSummary:
    """Aggregate timing statistics for one phase in one profile."""

    phase: str
    count: int
    mean_ms: float
    median_ms: float
    min_ms: float
    max_ms: float


@dataclass(frozen=True)
class ProfileSummary:
    """Aggregate timing statistics for one measurement profile."""

    profile: str
    iterations: int
    ready_iterations: int
    phases: tuple[PhaseSummary, ...]


@dataclass(frozen=True)
class PhaseComparison:
    """Comparison for a phase shared by two profiles."""

    phase: str
    baseline_median_ms: float
    candidate_median_ms: float
    delta_median_ms: float


@dataclass(frozen=True)
class PatchSurfaceFile:
    """Static Harmony patch shape for one C# patch file."""

    path: str
    source_kind: str
    is_harmony_patch: bool
    class_name: str | None
    harmony_patch_attributes: int
    harmony_target_method_attributes: int
    harmony_target_methods_attributes: int
    prefix_methods: int
    postfix_methods: int
    transpiler_methods: int
    finalizer_methods: int
    functional_family: str
    semantic_groups: tuple[str, ...]
    touch_policy: str
    protected: bool
    protection_reasons: tuple[str, ...]
    groups: tuple[str, ...]


@dataclass(frozen=True)
class PatchSurfaceGroup:
    """A semantic patch surface group derived from static markers."""

    group: str
    file_count: int
    marker_count: int
    protected_file_count: int
    files: tuple[str, ...]


@dataclass(frozen=True)
class PatchFunctionalFamily:
    """A primary implementation bucket for patch maintenance planning."""

    group: str
    file_count: int
    protected_file_count: int
    files: tuple[str, ...]


@dataclass(frozen=True)
class PatchSurfaceInventory:
    """Static Harmony patch surface inventory for review and trend tracking."""

    patch_root: str
    patch_files: int
    harmony_patch_attributes: int
    harmony_target_method_attributes: int
    harmony_target_methods_attributes: int
    prefix_methods: int
    postfix_methods: int
    transpiler_methods: int
    finalizer_methods: int
    protected_files: int
    source_files: int
    harmony_patch_files: int
    functional_families: tuple[PatchFunctionalFamily, ...]
    groups: tuple[PatchSurfaceGroup, ...]
    files: tuple[PatchSurfaceFile, ...]


def parse_startup_log_text(text: str) -> ParsedStartupLog:
    """Parse QudJP startup timing markers from Player.log text."""
    timings: list[StartupTimingEntry] = []
    error_lines: list[str] = []
    build_marker_seen = False
    harmony_complete_seen = False

    for index, line in enumerate(text.splitlines(), start=1):
        if _BUILD_MARKER in line:
            build_marker_seen = True
        if _HARMONY_COMPLETE_MARKER in line:
            harmony_complete_seen = True
        if any(marker in line for marker in _ERROR_MARKERS):
            error_lines.append(line)
        marker_index = line.find(_STARTUP_TIMING_MARKER)
        if marker_index != -1:
            fields = _parse_timing_fields(line[marker_index + len(_STARTUP_TIMING_MARKER) :].strip())
            phase = fields.get("phase")
            elapsed_ms = fields.get("elapsed_ms")
            if phase is None or elapsed_ms is None:
                continue
            try:
                elapsed = float(elapsed_ms)
            except ValueError:
                continue
            timings.append(
                StartupTimingEntry(
                    phase=phase,
                    elapsed_ms=elapsed,
                    detail=fields.get("detail"),
                    line_number=index,
                ),
            )
            continue

        game_loading_match = _GAME_LOADING_TASK_PATTERN.search(line)
        if game_loading_match is not None:
            timings.append(
                StartupTimingEntry(
                    phase="game.loading." + _normalize_phase_fragment(game_loading_match.group("task")),
                    elapsed_ms=float(game_loading_match.group("elapsed")),
                    detail=game_loading_match.group("task"),
                    line_number=index,
                ),
            )

    return ParsedStartupLog(
        timings=tuple(timings),
        build_marker_seen=build_marker_seen,
        harmony_complete_seen=harmony_complete_seen,
        error_lines=tuple(error_lines),
    )


def summarize_iterations(results: Iterable[IterationResult]) -> tuple[ProfileSummary, ...]:
    """Build per-profile phase summaries from iteration results."""
    grouped: dict[str, list[IterationResult]] = {}
    for result in results:
        grouped.setdefault(result.metadata.profile, []).append(result)

    summaries: list[ProfileSummary] = []
    for profile, profile_results in sorted(grouped.items()):
        phase_values: dict[str, list[float]] = {}
        for result in profile_results:
            if result.metadata.elapsed_until_ready_ms is not None:
                phase_values.setdefault("runner.elapsed_until_ready", []).append(result.metadata.elapsed_until_ready_ms)
            for timing in result.parsed.timings:
                phase_values.setdefault(timing.phase, []).append(timing.elapsed_ms)
        phases = tuple(
            PhaseSummary(
                phase=phase,
                count=len(values),
                mean_ms=round(mean(values), 3),
                median_ms=round(median(values), 3),
                min_ms=round(min(values), 3),
                max_ms=round(max(values), 3),
            )
            for phase, values in sorted(phase_values.items())
        )
        summaries.append(
            ProfileSummary(
                profile=profile,
                iterations=len(profile_results),
                ready_iterations=sum(1 for result in profile_results if result.metadata.status == "ready"),
                phases=phases,
            ),
        )
    return tuple(summaries)


def compare_profiles(
    summaries: Sequence[ProfileSummary],
    *,
    baseline_profile: str,
    candidate_profile: str,
) -> tuple[PhaseComparison, ...]:
    """Compare median phase timings between two profiles."""
    by_profile = {summary.profile: summary for summary in summaries}
    baseline = by_profile[baseline_profile]
    candidate = by_profile[candidate_profile]
    baseline_phases = {phase.phase: phase for phase in baseline.phases}
    candidate_phases = {phase.phase: phase for phase in candidate.phases}

    comparisons: list[PhaseComparison] = []
    for phase in sorted(set(baseline_phases) & set(candidate_phases)):
        baseline_median = baseline_phases[phase].median_ms
        candidate_median = candidate_phases[phase].median_ms
        comparisons.append(
            PhaseComparison(
                phase=phase,
                baseline_median_ms=baseline_median,
                candidate_median_ms=candidate_median,
                delta_median_ms=round(candidate_median - baseline_median, 3),
            ),
        )
    return tuple(comparisons)


def write_summary_markdown(summaries: Sequence[ProfileSummary], path: Path) -> None:
    """Write a compact Markdown summary table."""
    lines = ["# Startup Measurement Summary", ""]
    for summary in summaries:
        lines.extend(
            [
                f"## {summary.profile}",
                "",
                f"- iterations: {summary.iterations}",
                f"- ready iterations: {summary.ready_iterations}",
                "",
                "| phase | count | median ms | mean ms | min ms | max ms |",
                "| --- | ---: | ---: | ---: | ---: | ---: |",
            ],
        )
        lines.extend(
            f"| {phase.phase} | {phase.count} | {phase.median_ms:.3f} | {phase.mean_ms:.3f} | "
            f"{phase.min_ms:.3f} | {phase.max_ms:.3f} |"
            for phase in summary.phases
        )
        lines.append("")
    path.write_text("\n".join(lines), encoding="utf-8")


def write_comparison_markdown(comparisons: Sequence[PhaseComparison], path: Path) -> None:
    """Write a compact Markdown comparison table."""
    lines = [
        "# Startup Measurement Comparison",
        "",
        "| phase | baseline median ms | candidate median ms | delta median ms |",
        "| --- | ---: | ---: | ---: |",
    ]
    lines.extend(
        f"| {item.phase} | {item.baseline_median_ms:.3f} | {item.candidate_median_ms:.3f} | "
        f"{item.delta_median_ms:.3f} |"
        for item in comparisons
    )
    lines.append("")
    path.write_text("\n".join(lines), encoding="utf-8")


def write_top_phases_markdown(
    summaries: Sequence[ProfileSummary],
    path: Path,
    *,
    prefix: str,
    limit: int,
) -> None:
    """Write the highest-median phases matching a prefix for each profile."""
    lines = ["# Startup Measurement Top Phases", "", f"- prefix: `{prefix}`", f"- limit: {limit}", ""]
    for summary in summaries:
        matching = [phase for phase in summary.phases if phase.phase.startswith(prefix)]
        matching.sort(key=lambda phase: phase.median_ms, reverse=True)
        lines.extend(
            [
                f"## {summary.profile}",
                "",
                "| rank | phase | count | median ms | mean ms | min ms | max ms |",
                "| ---: | --- | ---: | ---: | ---: | ---: | ---: |",
            ],
        )
        if not matching:
            lines.append("|  | _no matching phases_ |  |  |  |  |  |")
        else:
            lines.extend(
                f"| {rank} | {phase.phase} | {phase.count} | {phase.median_ms:.3f} | "
                f"{phase.mean_ms:.3f} | {phase.min_ms:.3f} | {phase.max_ms:.3f} |"
                for rank, phase in enumerate(matching[:limit], start=1)
            )
        lines.append("")
    path.write_text("\n".join(lines), encoding="utf-8")


def build_patch_surface_inventory(patch_root: Path) -> PatchSurfaceInventory:
    """Build a deterministic static inventory of Harmony patch surfaces."""
    patch_files = sorted(path for path in patch_root.rglob("*.cs") if path.is_file())
    file_entries: list[PatchSurfaceFile] = []
    group_files: dict[str, set[str]] = {}
    group_protected_files: dict[str, set[str]] = {}
    group_marker_counts: dict[str, int] = {}

    for path in patch_files:
        text = path.read_text(encoding="utf-8")
        relative_path = path.relative_to(patch_root).as_posix()
        file_groups: list[str] = []
        harmony_patch_attributes = len(_HARMONY_PATCH_ATTRIBUTE_PATTERN.findall(text))
        is_harmony_patch = harmony_patch_attributes > 0
        source_kind = _classify_patch_source_kind(relative_path, text, is_harmony_patch=is_harmony_patch)
        class_name = _extract_patch_class_name(text)
        protection_reasons = _find_patch_protection_reasons(relative_path, text)
        protected = bool(protection_reasons)
        for group, pattern in _PATCH_GROUP_PATTERNS:
            marker_count = len(pattern.findall(text))
            if marker_count <= 0:
                continue
            file_groups.append(group)
            group_files.setdefault(group, set()).add(relative_path)
            if protected:
                group_protected_files.setdefault(group, set()).add(relative_path)
            group_marker_counts[group] = group_marker_counts.get(group, 0) + marker_count

        raw_groups = tuple(sorted(file_groups))
        semantic_groups = _classify_patch_semantic_groups(
            groups=raw_groups,
            harmony_patch_attributes=harmony_patch_attributes,
        )
        functional_family = _classify_patch_functional_family(semantic_groups)
        file_entries.append(
            PatchSurfaceFile(
                path=relative_path,
                source_kind=source_kind,
                is_harmony_patch=is_harmony_patch,
                class_name=class_name,
                harmony_patch_attributes=harmony_patch_attributes,
                harmony_target_method_attributes=len(_HARMONY_TARGET_METHOD_ATTRIBUTE_PATTERN.findall(text)),
                harmony_target_methods_attributes=len(_HARMONY_TARGET_METHODS_ATTRIBUTE_PATTERN.findall(text)),
                prefix_methods=len(_PATCH_METHOD_PATTERNS["prefix"].findall(text)),
                postfix_methods=len(_PATCH_METHOD_PATTERNS["postfix"].findall(text)),
                transpiler_methods=len(_PATCH_METHOD_PATTERNS["transpiler"].findall(text)),
                finalizer_methods=len(_PATCH_METHOD_PATTERNS["finalizer"].findall(text)),
                functional_family=functional_family,
                semantic_groups=semantic_groups,
                touch_policy=_PATCH_TOUCH_POLICY_NO_TOUCH if protected else _PATCH_TOUCH_POLICY_EDITABLE,
                protected=protected,
                protection_reasons=protection_reasons,
                groups=raw_groups,
            ),
        )

    groups = tuple(
        PatchSurfaceGroup(
            group=group,
            file_count=len(files),
            marker_count=group_marker_counts[group],
            protected_file_count=len(group_protected_files.get(group, set())),
            files=tuple(sorted(files)),
        )
        for group, files in sorted(group_files.items())
    )
    functional_families = _build_patch_functional_family_summaries(file_entries)

    return PatchSurfaceInventory(
        patch_root=_format_patch_root(patch_root),
        patch_files=len(patch_files),
        harmony_patch_attributes=sum(entry.harmony_patch_attributes for entry in file_entries),
        harmony_target_method_attributes=sum(entry.harmony_target_method_attributes for entry in file_entries),
        harmony_target_methods_attributes=sum(entry.harmony_target_methods_attributes for entry in file_entries),
        prefix_methods=sum(entry.prefix_methods for entry in file_entries),
        postfix_methods=sum(entry.postfix_methods for entry in file_entries),
        transpiler_methods=sum(entry.transpiler_methods for entry in file_entries),
        finalizer_methods=sum(entry.finalizer_methods for entry in file_entries),
        protected_files=sum(1 for entry in file_entries if entry.protected),
        source_files=len(file_entries),
        harmony_patch_files=sum(1 for entry in file_entries if entry.is_harmony_patch),
        functional_families=functional_families,
        groups=groups,
        files=tuple(file_entries),
    )


def _classify_patch_source_kind(relative_path: str, text: str, *, is_harmony_patch: bool) -> str:
    if is_harmony_patch:
        return "harmony_patch"
    if "Translator" in relative_path or re.search(r"\bclass\s+[A-Za-z0-9_]*Translator\b", text):
        return "translator"
    return "helper"


def _extract_patch_class_name(text: str) -> str | None:
    match = _CLASS_NAME_PATTERN.search(text)
    return None if match is None else match.group("class_name")


def _find_patch_protection_reasons(relative_path: str, text: str) -> tuple[str, ...]:
    path_haystack = relative_path
    content_haystack = relative_path + "\n" + text
    reasons: list[str] = []
    if relative_path in _PATCH_NO_TOUCH_PATHS:
        reasons.append("tmp_font_mesh")
    for reason, pattern in _PATCH_NO_TOUCH_REASON_PATTERNS:
        haystack = path_haystack if reason == "tmp_font_mesh" else content_haystack
        if reason not in reasons and pattern.search(haystack):
            reasons.append(reason)
    return tuple(reasons)


def _format_patch_root(patch_root: Path) -> str:
    resolved = patch_root.resolve()
    try:
        return resolved.relative_to(_PROJECT_ROOT).as_posix()
    except ValueError:
        return patch_root.as_posix()


def _classify_patch_functional_family(semantic_groups: Sequence[str]) -> str:
    return min(semantic_groups, key=_PATCH_PRIMARY_GROUP_PRIORITY.index)


def _classify_patch_semantic_groups(
    *,
    groups: tuple[str, ...],
    harmony_patch_attributes: int,
) -> tuple[str, ...]:
    candidates = [
        semantic_group
        for raw_group, semantic_group in _PATCH_RAW_TO_SEMANTIC_GROUP.items()
        if raw_group in groups
    ]
    if harmony_patch_attributes > 0:
        candidates.append("producer_patch")
    if not candidates:
        candidates.append("support_helper")

    deduplicated: list[str] = []
    for group in candidates:
        if group not in deduplicated:
            deduplicated.append(group)
    return tuple(deduplicated)


def _build_patch_functional_family_summaries(files: Sequence[PatchSurfaceFile]) -> tuple[PatchFunctionalFamily, ...]:
    grouped: dict[str, list[PatchSurfaceFile]] = {}
    for file in files:
        grouped.setdefault(file.functional_family, []).append(file)

    return tuple(
        PatchFunctionalFamily(
            group=group,
            file_count=len(group_files),
            protected_file_count=sum(1 for file in group_files if file.protected),
            files=tuple(file.path for file in sorted(group_files, key=lambda item: item.path)),
        )
        for group, group_files in sorted(
            grouped.items(),
            key=lambda item: _PATCH_PRIMARY_GROUP_PRIORITY.index(item[0]),
        )
    )


def write_patch_surface_inventory_markdown(inventory: PatchSurfaceInventory, path: Path) -> None:
    """Write a compact Markdown patch surface inventory."""
    lines = [
        "# Harmony Patch Surface Inventory",
        "",
        f"- patch root: `{inventory.patch_root}`",
        f"- patch files: {inventory.patch_files}",
        f"- `[HarmonyPatch]`: {inventory.harmony_patch_attributes}",
        f"- `[HarmonyTargetMethod]`: {inventory.harmony_target_method_attributes}",
        f"- `[HarmonyTargetMethods]`: {inventory.harmony_target_methods_attributes}",
        f"- source files: {inventory.source_files}",
        f"- Harmony patch files: {inventory.harmony_patch_files}",
        f"- patch methods: Prefix {inventory.prefix_methods}, Postfix {inventory.postfix_methods}, "
        f"Transpiler {inventory.transpiler_methods}, Finalizer {inventory.finalizer_methods}",
        f"- protected no-touch files: {inventory.protected_files}",
        "",
        "## Functional Families",
        "",
        "| functional family | files | protected files |",
        "| --- | ---: | ---: |",
    ]
    lines.extend(
        f"| {group.group} | {group.file_count} | {group.protected_file_count} |"
        for group in inventory.functional_families
    )
    lines.extend(
        [
            "",
            "## Marker Groups",
            "",
            "| group | files | protected files | markers |",
            "| --- | ---: | ---: | ---: |",
        ],
    )
    lines.extend(
        f"| {group.group} | {group.file_count} | {group.protected_file_count} | {group.marker_count} |"
        for group in inventory.groups
    )
    lines.extend(
        [
            "",
            "## Files",
            "",
            (
                "| file | kind | functional family | touch policy | protected | protection reasons | groups | "
                "patch attrs | target attrs | "
                "target plural attrs | methods |"
            ),
            "| --- | --- | --- | --- | --- | --- | --- | ---: | ---: | ---: | --- |",
        ],
    )
    lines.extend(
        (
            "| {path} | {kind} | {family} | {touch} | {protected} | {protection_reasons} | {groups} | "
            "{patch} | {target} | {targets} | "
            "P:{prefix} Po:{postfix} T:{transpiler} F:{finalizer} |"
        ).format(
            path=file.path,
            kind=file.source_kind,
            family=file.functional_family,
            touch=file.touch_policy,
            protected=str(file.protected).lower(),
            protection_reasons=", ".join(file.protection_reasons) if file.protection_reasons else "",
            groups=", ".join(file.groups) if file.groups else "",
            patch=file.harmony_patch_attributes,
            target=file.harmony_target_method_attributes,
            targets=file.harmony_target_methods_attributes,
            prefix=file.prefix_methods,
            postfix=file.postfix_methods,
            transpiler=file.transpiler_methods,
            finalizer=file.finalizer_methods,
        )
        for file in inventory.files
    )
    lines.append("")
    path.write_text("\n".join(lines), encoding="utf-8")


def _parse_timing_fields(text: str) -> dict[str, str]:
    fields: dict[str, str] = {}
    for token in _split_escaped_tokens(text):
        if "=" not in token:
            continue
        key, value = token.split("=", maxsplit=1)
        fields[key] = value
    return fields


def _split_escaped_tokens(text: str) -> list[str]:
    tokens: list[str] = []
    current: list[str] = []
    escaped = False
    for char in text:
        if escaped:
            current.append(char)
            escaped = False
            continue
        if char == "\\":
            escaped = True
            continue
        if char == " ":
            if current:
                tokens.append("".join(current))
                current = []
            continue
        current.append(char)
    if escaped:
        current.append("\\")
    if current:
        tokens.append("".join(current))
    return tokens


def _normalize_phase_fragment(value: str) -> str:
    normalized = re.sub(r"[^a-z0-9]+", "_", value.lower()).strip("_")
    return normalized or "unknown"


def _as_jsonable(value: object) -> object:
    if isinstance(value, tuple):
        return [_as_jsonable(item) for item in value]
    if isinstance(value, list):
        return [_as_jsonable(item) for item in value]
    if hasattr(value, "__dataclass_fields__"):
        return {key: _as_jsonable(item) for key, item in asdict(value).items()}
    if isinstance(value, dict):
        return {key: _as_jsonable(item) for key, item in value.items()}
    return value


def _write_json(path: Path, value: object) -> None:
    path.write_text(json.dumps(_as_jsonable(value), ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def _read_iteration_result(iteration_dir: Path) -> IterationResult:
    metadata = json.loads((iteration_dir / "metadata.json").read_text(encoding="utf-8"))
    parsed = parse_startup_log_text((iteration_dir / "Player.log").read_text(encoding="utf-8"))
    return IterationResult(
        metadata=IterationMetadata(
            profile=metadata["profile"],
            iteration=int(metadata["iteration"]),
            command=tuple(metadata["command"]),
            started_at=metadata["started_at"],
            status=metadata["status"],
            elapsed_until_ready_ms=metadata["elapsed_until_ready_ms"],
            exit_code=metadata["exit_code"],
        ),
        parsed=parsed,
    )


def _find_iteration_dirs(input_dir: Path) -> list[Path]:
    return sorted(path for path in input_dir.glob("profiles/*/iteration-*") if (path / "metadata.json").exists())


def _read_profile_summaries(summary_path: Path) -> tuple[ProfileSummary, ...]:
    summaries = json.loads(summary_path.read_text(encoding="utf-8"))
    return tuple(
        ProfileSummary(
            profile=summary["profile"],
            iterations=summary["iterations"],
            ready_iterations=summary["ready_iterations"],
            phases=tuple(PhaseSummary(**phase) for phase in summary["phases"]),
        )
        for summary in summaries
    )


def _wait_for_ready_marker(
    log_path: Path,
    timeout_seconds: float,
    ready_marker: str,
    process: subprocess.Popen[bytes] | None = None,
) -> tuple[str, float | None]:
    started = time.monotonic()
    deadline = started + timeout_seconds
    while time.monotonic() < deadline:
        if log_path.exists():
            text = log_path.read_text(encoding="utf-8", errors="replace")
            if "Harmony patched zero methods" in text or "mprotect returned EACCES" in text:
                return "harmony_failed", time.monotonic() - started
            if ready_marker in text:
                return "ready", time.monotonic() - started
        if process is not None and process.poll() is not None:
            return "process_exited", time.monotonic() - started
        time.sleep(0.25)
    return "timeout", None


def _stop_process(process: subprocess.Popen[bytes]) -> None:
    if process.poll() is not None:
        return
    process.terminate()
    try:
        process.wait(timeout=5)
    except subprocess.TimeoutExpired:
        process.kill()
        process.wait(timeout=5)


def _disable_mod(mod_dir: Path) -> Path | None:
    if not mod_dir.exists():
        return None
    disabled_root = mod_dir.parent.parent / ".qudjp-startup-measure-disabled"
    disabled_dir = disabled_root / mod_dir.name
    if disabled_dir.exists():
        msg = f"Refusing to overwrite existing disabled mod directory: {disabled_dir}"
        raise FileExistsError(msg)
    disabled_root.mkdir(parents=True, exist_ok=True)
    mod_dir.rename(disabled_dir)
    return disabled_dir


def _restore_mod(mod_dir: Path, disabled_dir: Path | None) -> None:
    if disabled_dir is None:
        return
    if mod_dir.exists():
        msg = (
            f"Refusing to restore disabled mod over existing directory: {mod_dir}. "
            f"The disabled copy remains at: {disabled_dir}"
        )
        raise FileExistsError(msg)
    mod_dir.parent.mkdir(parents=True, exist_ok=True)
    disabled_dir.rename(mod_dir)


def _resolve_ready_marker(*, disable_mod: bool, ready_marker: str | None) -> str:
    if ready_marker is not None:
        return ready_marker
    if disable_mod:
        msg = (
            "--disable-mod requires --ready-marker because the default QudJP Harmony "
            "completion marker will not appear while QudJP is disabled."
        )
        raise ValueError(msg)
    return _HARMONY_COMPLETE_MARKER


def _collect(args: argparse.Namespace) -> int:
    command = tuple(shlex.split(args.launch_cmd))
    artifact_dir = args.artifact_dir
    ready_marker = _resolve_ready_marker(disable_mod=args.disable_mod, ready_marker=args.ready_marker)
    disabled_dirs: list[tuple[Path, Path | None]] = []
    try:
        if args.disable_mod:
            mod_dir = args.mod_dir or resolve_default_destination()
            disabled_dirs.append((mod_dir, _disable_mod(mod_dir)))
        disabled_dirs.extend((extra_mod_dir, _disable_mod(extra_mod_dir)) for extra_mod_dir in args.disable_mod_dir)
        results: list[IterationResult] = []
        for iteration in range(1, args.iterations + 1):
            iteration_dir = artifact_dir / "profiles" / args.profile / f"iteration-{iteration:02d}"
            iteration_dir.mkdir(parents=True, exist_ok=True)
            previous_log = iteration_dir / "Player.before.log"
            if args.log.exists():
                shutil.copy2(args.log, previous_log)
                args.log.unlink()
            started_at = datetime.now(UTC).isoformat()
            with (iteration_dir / "stdout.txt").open("wb") as stdout_file, (iteration_dir / "stderr.txt").open(
                "wb",
            ) as stderr_file:
                process = subprocess.Popen(  # noqa: S603 -- command is explicit CLI input for local measurement.
                    command,
                    stdout=stdout_file,
                    stderr=stderr_file,
                )
                status, elapsed = _wait_for_ready_marker(args.log, args.timeout, ready_marker, process)
                if status == "ready" and args.settle_seconds > 0:
                    time.sleep(args.settle_seconds)
                _stop_process(process)
            if args.log.exists():
                shutil.copy2(args.log, iteration_dir / "Player.log")
            else:
                (iteration_dir / "Player.log").write_text("", encoding="utf-8")

            metadata = IterationMetadata(
                profile=args.profile,
                iteration=iteration,
                command=command,
                started_at=started_at,
                status=status,
                elapsed_until_ready_ms=None if elapsed is None else round(elapsed * 1000, 3),
                exit_code=process.returncode,
            )
            parsed = parse_startup_log_text((iteration_dir / "Player.log").read_text(encoding="utf-8"))
            result = IterationResult(metadata=metadata, parsed=parsed)
            results.append(result)
            _write_json(iteration_dir / "metadata.json", metadata)
            _write_json(iteration_dir / "parsed.json", parsed)

        summaries = summarize_iterations(results)
        artifact_dir.mkdir(parents=True, exist_ok=True)
        _write_json(artifact_dir / f"{args.profile}-summary.json", summaries)
        write_summary_markdown(summaries, artifact_dir / f"{args.profile}-summary.md")
        return 0
    finally:
        for enabled_dir, disabled_dir in reversed(disabled_dirs):
            _restore_mod(enabled_dir, disabled_dir)


def _parse(args: argparse.Namespace) -> int:
    results = [_read_iteration_result(path) for path in _find_iteration_dirs(args.input_dir)]
    summaries = summarize_iterations(results)
    _write_json(args.output, summaries)
    if args.markdown is not None:
        write_summary_markdown(summaries, args.markdown)
    return 0


def _compare(args: argparse.Namespace) -> int:
    profile_summaries = _read_profile_summaries(args.summary)
    comparisons = compare_profiles(
        profile_summaries,
        baseline_profile=args.baseline,
        candidate_profile=args.candidate,
    )
    _write_json(args.output, comparisons)
    if args.markdown is not None:
        write_comparison_markdown(comparisons, args.markdown)
    return 0


def _top(args: argparse.Namespace) -> int:
    profile_summaries = _read_profile_summaries(args.summary)
    selected_profiles = set(args.profile)
    if selected_profiles:
        profile_summaries = tuple(summary for summary in profile_summaries if summary.profile in selected_profiles)

    top_by_profile: dict[str, list[PhaseSummary]] = {}
    for summary in profile_summaries:
        matching = [phase for phase in summary.phases if phase.phase.startswith(args.prefix)]
        matching.sort(key=lambda phase: phase.median_ms, reverse=True)
        top_by_profile[summary.profile] = matching[: args.limit]

    _write_json(args.output, top_by_profile)
    if args.markdown is not None:
        write_top_phases_markdown(profile_summaries, args.markdown, prefix=args.prefix, limit=args.limit)
    return 0


def _patch_inventory(args: argparse.Namespace) -> int:
    patch_root = _resolve_patch_inventory_root(args.patch_root)
    inventory = build_patch_surface_inventory(patch_root)
    _write_json(args.output, inventory)
    if args.markdown is not None:
        write_patch_surface_inventory_markdown(inventory, args.markdown)
    return 0


def _resolve_patch_inventory_root(patch_root: Path) -> Path:
    resolved = patch_root if patch_root.is_absolute() else _PROJECT_ROOT / patch_root
    if not resolved.is_dir():
        message = f"Patch root does not exist: {resolved}"
        raise FileNotFoundError(message)

    return resolved


def _build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest="command", required=True)

    collect = subparsers.add_parser("collect", help="Launch the game repeatedly and preserve Player.log artifacts.")
    collect.add_argument("--profile", required=True, help="Profile name, for example qudjp-enabled or qudjp-disabled.")
    collect.add_argument("--iterations", type=int, default=3, help="Number of launch iterations.")
    collect.add_argument("--artifact-dir", type=Path, default=_DEFAULT_ARTIFACT_DIR, help="Artifact output directory.")
    collect.add_argument(
        "--launch-cmd",
        default="scripts/launch_rosetta.sh",
        help="Command used to launch Caves of Qud.",
    )
    collect.add_argument("--log", type=Path, default=_DEFAULT_LOG, help="Path to Player.log.")
    collect.add_argument("--timeout", type=float, default=90.0, help="Seconds to wait for the ready marker.")
    collect.add_argument(
        "--settle-seconds",
        type=float,
        default=0.0,
        help="Seconds to keep the game running after the ready marker before preserving Player.log.",
    )
    collect.add_argument(
        "--ready-marker",
        default=None,
        help=(
            "Player.log marker that ends an iteration. Defaults to the QudJP Harmony "
            "completion marker unless --disable-mod is used."
        ),
    )
    collect.add_argument("--mod-dir", type=Path, default=None, help="Mods/QudJP directory to rename for --disable-mod.")
    collect.add_argument(
        "--disable-mod-dir",
        type=Path,
        action="append",
        default=[],
        help="Additional mod directory to temporarily rename during collection.",
    )
    collect.add_argument("--disable-mod", action="store_true", help="Temporarily disable QudJP for this profile.")
    collect.set_defaults(func=_collect)

    parse = subparsers.add_parser("parse", help="Parse preserved iteration artifacts into a summary.")
    parse.add_argument(
        "--input-dir",
        type=Path,
        required=True,
        help="Artifact directory containing profiles/*/iteration-*.",
    )
    parse.add_argument("--output", type=Path, required=True, help="JSON summary output path.")
    parse.add_argument("--markdown", type=Path, default=None, help="Optional Markdown summary output path.")
    parse.set_defaults(func=_parse)

    compare = subparsers.add_parser("compare", help="Compare two profiles from a summary JSON file.")
    compare.add_argument("--summary", type=Path, required=True, help="JSON summary from the parse command.")
    compare.add_argument("--baseline", required=True, help="Baseline profile name.")
    compare.add_argument("--candidate", required=True, help="Candidate profile name.")
    compare.add_argument("--output", type=Path, required=True, help="JSON comparison output path.")
    compare.add_argument("--markdown", type=Path, default=None, help="Optional Markdown comparison output path.")
    compare.set_defaults(func=_compare)

    top = subparsers.add_parser("top", help="Report highest-median phases matching a prefix.")
    top.add_argument("--summary", type=Path, required=True, help="JSON summary from the parse command.")
    top.add_argument("--prefix", required=True, help="Phase prefix to include, for example harmony.patch_apply.")
    top.add_argument("--limit", type=int, default=20, help="Maximum rows per profile.")
    top.add_argument("--profile", action="append", default=[], help="Optional profile to include; may be repeated.")
    top.add_argument("--output", type=Path, required=True, help="JSON top phases output path.")
    top.add_argument("--markdown", type=Path, default=None, help="Optional Markdown top phases output path.")
    top.set_defaults(func=_top)

    patch_inventory = subparsers.add_parser(
        "patch-inventory",
        help="Build a static Harmony patch surface inventory for review.",
    )
    patch_inventory.add_argument(
        "--patch-root",
        type=Path,
        default=Path("Mods/QudJP/Assemblies/src/Patches"),
        help="Root containing Harmony patch C# files.",
    )
    patch_inventory.add_argument("--output", type=Path, required=True, help="JSON inventory output path.")
    patch_inventory.add_argument("--markdown", type=Path, default=None, help="Optional Markdown inventory output path.")
    patch_inventory.set_defaults(func=_patch_inventory)

    return parser


def main(argv: Sequence[str] | None = None) -> int:
    """Run the startup measurement CLI."""
    parser = _build_parser()
    args = parser.parse_args(argv)
    return args.func(args)


if __name__ == "__main__":
    sys.exit(main())
