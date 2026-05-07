"""Validator for the executable QudJP localization coverage map."""

from __future__ import annotations

import json
from pathlib import Path
from typing import Final, NotRequired, TypedDict, cast

SCHEMA_VERSION: Final = "1.0"
GAME_VERSION: Final = "1.0.4"
MAP_PATH: Final = Path("docs/localization-coverage-map.json")

REQUIRED_STATUSES: Final = {
    "covered_by_validators",
    "classified_queue",
    "inventoried_unclosed",
    "preview_inventory",
    "partial_domain_inventory",
    "partial_tests_no_inventory",
    "runtime_evidence",
    "boundary_observed",
    "legacy_view_only",
}

REQUIRED_CATEGORIES: Final = {
    "asset_validation",
    "csharp_static_inventory",
    "domain_inventory",
    "generated_text_inventory",
    "legacy_view",
    "route_family_tests",
    "runtime_evidence",
}

REQUIRED_SURFACE_IDS: Final = {
    "activated_ability_names",
    "annals_history_patterns",
    "chargen_ui_routes",
    "conversation_routes",
    "description_effect_detail_routes",
    "display_name_composition",
    "hud_status_ability_bar_routes",
    "journal_quest_routes",
    "legacy_candidate_inventory",
    "localization_assets",
    "procedural_cooking_effects",
    "renderer_and_sink_boundaries",
    "runtime_observability_triage",
    "screen_ui_routes",
    "static_producer_messages_popups",
    "ui_text_construction",
    "world_generation_and_zone_names",
}


class CoverageSurface(TypedDict):
    """One localization coverage-map lane."""

    id: str
    label: str
    category: str
    status: str
    target_surfaces: list[str]
    scanner: NotRequired[str]
    inventory_artifact: NotRequired[str]
    summary_artifact: NotRequired[str]
    closure_gate: NotRequired[str]
    tests: NotRequired[list[str]]
    validators: NotRequired[list[str]]
    commands: list[str]
    known_limits: list[str]
    next_actions: list[str]


class CoverageMap(TypedDict):
    """Machine-readable localization coverage map."""

    schema_version: str
    game_version: str
    description: NotRequired[str]
    surfaces: list[CoverageSurface]


REQUIRED_FIELDS: Final = (
    "id",
    "label",
    "category",
    "status",
    "target_surfaces",
    "known_limits",
    "next_actions",
)


def load_map(path: Path = MAP_PATH) -> CoverageMap:
    """Load the localization coverage map."""
    return cast("CoverageMap", json.loads(path.read_text(encoding="utf-8")))


def validate_map(repo_root: Path, path: Path = MAP_PATH) -> list[str]:
    """Validate that the coverage map is complete enough to drive agent work."""
    document = load_map(repo_root / path)
    errors: list[str] = []

    if document.get("schema_version") != SCHEMA_VERSION:
        errors.append(f"unexpected schema_version: {document.get('schema_version')!r}")
    if document.get("game_version") != GAME_VERSION:
        errors.append(f"unexpected game_version: {document.get('game_version')!r}")

    surfaces = document["surfaces"]
    ids = [surface.get("id", "") for surface in surfaces]
    duplicate_ids = sorted({surface_id for surface_id in ids if ids.count(surface_id) > 1})
    errors.extend(f"duplicate surface id: {surface_id}" for surface_id in duplicate_ids)

    missing_required = sorted(REQUIRED_SURFACE_IDS - set(ids))
    errors.extend(f"missing required surface: {surface_id}" for surface_id in missing_required)

    for surface in surfaces:
        errors.extend(_validate_surface(repo_root, surface))

    statuses = {str(surface.get("status", "")) for surface in surfaces}
    if "inventoried_unclosed" not in statuses:
        errors.append("coverage map must explicitly include at least one inventoried_unclosed surface")
    if not statuses <= REQUIRED_STATUSES:
        errors.append("unknown statuses: " + ", ".join(sorted(statuses - REQUIRED_STATUSES)))

    return errors


def _validate_surface(repo_root: Path, surface: CoverageSurface) -> list[str]:
    errors: list[str] = []
    surface_id = surface.get("id", "<missing-id>")

    errors.extend(f"{surface_id}: missing {field}" for field in REQUIRED_FIELDS if field not in surface)
    errors.extend(_validate_surface_enums(surface_id, surface))
    errors.extend(_validate_surface_lists(surface_id, surface))
    errors.extend(_validate_surface_paths(repo_root, surface_id, surface))
    errors.extend(_validate_surface_status_contract(surface_id, surface))

    return errors


def _validate_surface_enums(surface_id: str, surface: CoverageSurface) -> list[str]:
    errors: list[str] = []
    category = surface.get("category", "")
    status = surface.get("status", "")

    if category not in REQUIRED_CATEGORIES:
        errors.append(f"{surface_id}: unknown category {category!r}")
    if status not in REQUIRED_STATUSES:
        errors.append(f"{surface_id}: unknown status {status!r}")

    return errors


def _validate_surface_lists(surface_id: str, surface: CoverageSurface) -> list[str]:
    errors: list[str] = []
    required_lists = (
        ("target_surfaces", surface.get("target_surfaces")),
        ("known_limits", surface.get("known_limits")),
        ("next_actions", surface.get("next_actions")),
    )

    errors.extend(
        f"{surface_id}: {field} must be a non-empty list"
        for field, values in required_lists
        if not _non_empty_list(values)
    )
    if not _non_empty_list(surface.get("commands")):
        errors.append(f"{surface_id}: commands must be a non-empty list")

    return errors


def _validate_surface_paths(repo_root: Path, surface_id: str, surface: CoverageSurface) -> list[str]:
    errors: list[str] = []
    single_paths = (
        ("scanner", surface.get("scanner")),
        ("inventory_artifact", surface.get("inventory_artifact")),
        ("summary_artifact", surface.get("summary_artifact")),
        ("closure_gate", surface.get("closure_gate")),
    )
    list_paths = (
        ("tests", surface.get("tests", [])),
        ("validators", surface.get("validators", [])),
    )

    for field, value in single_paths:
        if value is not None:
            _append_existing_path_error(errors, repo_root, surface_id, field, value)

    for field, values in list_paths:
        for value in values:
            _append_existing_path_error(errors, repo_root, surface_id, field, value)

    return errors


def _validate_surface_status_contract(surface_id: str, surface: CoverageSurface) -> list[str]:
    errors: list[str] = []
    status = surface.get("status", "")
    category = surface.get("category", "")
    if status in {"inventoried_unclosed", "covered_by_validators"} and not (
        surface.get("inventory_artifact") or surface.get("closure_gate") or surface.get("validators")
    ):
        errors.append(f"{surface_id}: status {status} needs inventory_artifact, closure_gate, or validators")

    if status == "legacy_view_only" and category != "legacy_view":
        errors.append(f"{surface_id}: legacy_view_only status must use legacy_view category")

    return errors


def _non_empty_list(value: list[str] | None) -> bool:
    return value is not None and len(value) > 0


def _append_existing_path_error(
    errors: list[str],
    repo_root: Path,
    surface_id: str,
    field: str,
    value: str,
) -> None:
    path = repo_root / value
    if not path.exists():
        errors.append(f"{surface_id}: {field} path does not exist: {value}")
