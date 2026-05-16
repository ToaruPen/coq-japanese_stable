"""Classify Roslyn text-construction surfaces by localization value."""

from __future__ import annotations

import json
import sys
from argparse import ArgumentParser
from pathlib import Path
from typing import Final, Literal, TypedDict, cast

Classification = Literal[
    "player_visible_api",
    "player_visible_owner_candidate",
    "candidate_only",
    "non_target",
]
OutputFormat = Literal["text", "json", "lanes-json"]
ClosureStatus = Literal["action_required", "covered_by_owner_route"]
ClosureLane = Literal[
    "activated_ability_names",
    "combat_message_frame_does",
    "description_effect_detail",
    "display_name_composition",
    "history_generated_text",
    "journal_quest_routes",
    "producer_message_popup",
    "screen_ui_direct_text",
    "other_owner_candidate",
]


class ClosureOverlayEntry(TypedDict):
    """Reviewed closure evidence for one text-construction family."""

    closure_status: ClosureStatus
    closure_evidence: list[str]

PLAYER_VISIBLE_API_SURFACES: Final = {
    "ActivatedAbility",
    "AddPlayerMessage",
    "Description",
    "DescriptionReturn",
    "DisplayNameReturn",
    "DisplayTextReturn",
    "Does",
    "EffectDescriptionReturn",
    "EmitMessage",
    "GetDisplayName",
    "HistoricStringExpander",
    "JournalAPI",
    "MessageFrame",
    "Popup",
    "TutorialManagerPopup",
}
CONTEXTUAL_OWNER_SURFACES: Final = {
    "DescriptionAssignment",
    "DirectTextAssignment",
    "DisplayNameAssignment",
    "SetText",
}
CONSTRUCTION_ONLY_SURFACES: Final = {
    "Assignment",
    "ReplaceBuilder",
    "ReplaceChain",
    "Return",
    "StringBuilderAppend",
    "StringFormat",
}
NON_TARGET_SURFACES: Final = {
    "Attribute",
    "Initializer",
    "Other",
    "OtherInvocation",
}
UI_OWNER_FILE_PREFIXES: Final = (
    "Qud.UI/",
    "XRL.UI/",
    "XRL.CharacterBuilds.Qud.UI/",
)
GAMEPLAY_OWNER_FILE_PREFIXES: Final = (
    "Qud.API/",
    "XRL.Annals/",
    "XRL.World/",
    "XRL.World.Effects/",
    "XRL.World.Parts/",
    "XRL.World.Parts.Mutation/",
    "XRL.World.Skills.Cooking/",
    "XRL.World.ZoneBuilders/",
)
NON_PLAYER_FILE_PREFIXES: Final = (
    "Overlay.MapEditor/",
    "UnityStandardAssets.",
    "XRL.Wish/",
)
NON_PLAYER_FILE_NAMES: Final = (
    "UAP_",
    "uGUI",
)
LOW_VALUE_FILE_PARTS: Final = (
    "/ObjectFinderTests.cs",
    "/StatWishHandler.cs",
    "/WishMenu.cs",
    "/Wishing.cs",
    "Debug",
    "MetricsManager.cs",
    "Test",
    "WorkshopUploader",
)
CLASSIFICATION_ORDER: Final = {
    "player_visible_api": 0,
    "player_visible_owner_candidate": 1,
    "candidate_only": 2,
    "non_target": 3,
}
VALUABLE_CLASSIFICATIONS: Final = frozenset({"player_visible_api", "player_visible_owner_candidate"})
LANE_ORDER: Final = {
    "combat_message_frame_does": 0,
    "history_generated_text": 1,
    "screen_ui_direct_text": 2,
    "display_name_composition": 3,
    "description_effect_detail": 4,
    "journal_quest_routes": 5,
    "activated_ability_names": 6,
    "producer_message_popup": 7,
    "other_owner_candidate": 8,
}
COMBAT_MELEE_ATTACK_FAMILY_ID: Final = (
    "XRL.World.Parts/Combat.cs::Combat.MeleeAttackWithWeaponInternal("
    "GameObject,GameObject,GameObject,BodyPart,string,int,int,int,int,int,bool,bool)"
)
TEXT_CONSTRUCTION_CLOSURE_OVERLAY: Final[dict[str, ClosureOverlayEntry]] = {
    COMBAT_MELEE_ATTACK_FAMILY_ID: {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            "Mods/QudJP/Assemblies/QudJP.Tests/L1/DoesVerbFamilyTests.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L1/MessagePatternTranslatorTests.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
        ],
    },
}


class TextConstructionFamily(TypedDict):
    """Family record emitted by TextConstructionInventory."""

    family_id: str
    file: str
    namespace: str | None
    type_name: str
    member_name: str
    member_signature: str
    member_kind: str
    member_start_line: int
    text_construction_count: int
    shape_counts: dict[str, int]
    context_counts: dict[str, int]
    surface_counts: dict[str, int]
    first_lines: list[int]


class TextConstructionInventory(TypedDict):
    """TextConstructionInventory JSON payload."""

    schema_version: str
    game_version: str
    totals: dict[str, object]
    families: list[TextConstructionFamily]


class SurfaceQueueEntry(TypedDict):
    """One classified text-construction family for localization planning."""

    classification: Classification
    closure_lane: ClosureLane
    closure_status: ClosureStatus
    closure_evidence: list[str]
    family_id: str
    source_file: str
    type_name: str
    member_name: str
    member_signature: str
    member_start_line: int
    text_construction_count: int
    player_visible_surfaces: list[str]
    contextual_surfaces: list[str]
    construction_only_surfaces: list[str]
    non_target_surfaces: list[str]
    first_lines: list[int]
    reason: str
    action: str


class SurfaceQueuePayload(TypedDict):
    """Serialized classified surface queue."""

    schema_version: str
    inventory: str
    counts: dict[str, int]
    lane_counts: dict[str, int]
    entries: list[SurfaceQueueEntry]


class LaneSummary(TypedDict):
    """Aggregated closure-lane handoff summary."""

    entry_count: int
    text_construction_count: int
    closure_status_counts: dict[str, int]
    top_entries: list[SurfaceQueueEntry]


class LaneSummaryPayload(TypedDict):
    """Serialized closure-lane summary."""

    schema_version: str
    inventory: str
    lane_counts: dict[str, int]
    lanes: dict[str, LaneSummary]


class ClassifiedSurface(TypedDict):
    """Internal classification result."""

    classification: Classification
    player_visible_surfaces: list[str]
    contextual_surfaces: list[str]
    construction_only_surfaces: list[str]
    non_target_surfaces: list[str]
    reason: str
    action: str


def load_inventory(path: Path) -> TextConstructionInventory:
    """Load a TextConstructionInventory JSON payload."""
    return cast("TextConstructionInventory", json.loads(path.read_text(encoding="utf-8")))


def classify_family(family: TextConstructionFamily) -> ClassifiedSurface:
    """Classify whether a family is a valuable localization surface."""
    surfaces = set(family["surface_counts"])
    player_visible_surfaces = sorted(surfaces & PLAYER_VISIBLE_API_SURFACES)
    contextual_surfaces = sorted(surfaces & CONTEXTUAL_OWNER_SURFACES)
    construction_only_surfaces = sorted(surfaces & CONSTRUCTION_ONLY_SURFACES)
    non_target_surfaces = sorted(surfaces & NON_TARGET_SURFACES)

    if player_visible_surfaces and _is_low_value_source_file(family["file"]):
        return {
            "classification": "candidate_only",
            "player_visible_surfaces": player_visible_surfaces,
            "contextual_surfaces": contextual_surfaces,
            "construction_only_surfaces": construction_only_surfaces,
            "non_target_surfaces": non_target_surfaces,
            "reason": "debug, test, metrics, workshop, wish, or tool-like source is not normal gameplay coverage",
            "action": "promote only if runtime evidence shows this route matters to ordinary player localization",
        }

    if player_visible_surfaces:
        return {
            "classification": "player_visible_api",
            "player_visible_surfaces": player_visible_surfaces,
            "contextual_surfaces": contextual_surfaces,
            "construction_only_surfaces": construction_only_surfaces,
            "non_target_surfaces": non_target_surfaces,
            "reason": "known player-visible API or route-return surface",
            "action": "trace owner route, add or extend owner translator, and test the route",
        }

    if contextual_surfaces and _is_player_visible_owner_candidate(family):
        return {
            "classification": "player_visible_owner_candidate",
            "player_visible_surfaces": [],
            "contextual_surfaces": contextual_surfaces,
            "construction_only_surfaces": construction_only_surfaces,
            "non_target_surfaces": non_target_surfaces,
            "reason": "text assignment occurs in a likely player-visible owner class or method",
            "action": "confirm screen/owner field, then patch the screen-specific route if needed",
        }

    if contextual_surfaces or construction_only_surfaces:
        return {
            "classification": "candidate_only",
            "player_visible_surfaces": [],
            "contextual_surfaces": contextual_surfaces,
            "construction_only_surfaces": construction_only_surfaces,
            "non_target_surfaces": non_target_surfaces,
            "reason": "string construction exists, but player visibility is not proven by this surface",
            "action": "promote only if a visible owner route or runtime evidence proves player exposure",
        }

    return {
        "classification": "non_target",
        "player_visible_surfaces": [],
        "contextual_surfaces": [],
        "construction_only_surfaces": [],
        "non_target_surfaces": non_target_surfaces,
        "reason": "attribute, initializer, generic invocation, or other non-surface text",
        "action": "do not use for localization coverage unless another route promotes it",
    }


def build_surface_queue(inventory: TextConstructionInventory) -> list[SurfaceQueueEntry]:
    """Classify every text-construction family and return a stable queue."""
    return sorted(
        (_queue_entry(family) for family in inventory["families"]),
        key=lambda entry: (
            CLASSIFICATION_ORDER[entry["classification"]],
            -entry["text_construction_count"],
            entry["source_file"],
            entry["member_start_line"],
            entry["family_id"],
        ),
    )


def valuable_surface_queue(inventory: TextConstructionInventory) -> list[SurfaceQueueEntry]:
    """Return only families worth considering for localization ownership."""
    return [
        entry
        for entry in build_surface_queue(inventory)
        if entry["classification"] in VALUABLE_CLASSIFICATIONS
    ]


def queue_payload(
    inventory: TextConstructionInventory,
    *,
    inventory_path: Path,
    include: str = "valuable",
) -> SurfaceQueuePayload:
    """Build a JSON-serializable classified queue payload."""
    entries = _filter_entries(build_surface_queue(inventory), include)
    counts: dict[str, int] = {}
    lane_counts: dict[str, int] = {}
    for entry in entries:
        counts[entry["classification"]] = counts.get(entry["classification"], 0) + 1
        lane_counts[entry["closure_lane"]] = lane_counts.get(entry["closure_lane"], 0) + 1

    return {
        "schema_version": "1.0",
        "inventory": str(inventory_path),
        "counts": counts,
        "lane_counts": dict(sorted(lane_counts.items(), key=lambda item: LANE_ORDER[item[0]])),
        "entries": entries,
    }


def lane_summary_payload(
    inventory: TextConstructionInventory,
    *,
    inventory_path: Path,
    include: str = "valuable",
    top_per_lane: int = 5,
) -> LaneSummaryPayload:
    """Build an actionable closure-lane summary with representative top families."""
    payload = queue_payload(inventory, inventory_path=inventory_path, include=include)
    lanes: dict[str, LaneSummary] = {}
    for entry in payload["entries"]:
        lane = entry["closure_lane"]
        summary = lanes.setdefault(
            lane,
            {
                "entry_count": 0,
                "text_construction_count": 0,
                "closure_status_counts": {},
                "top_entries": [],
            },
        )
        summary["entry_count"] += 1
        summary["text_construction_count"] += entry["text_construction_count"]
        closure_status = entry["closure_status"]
        summary["closure_status_counts"][closure_status] = summary["closure_status_counts"].get(closure_status, 0) + 1
        if top_per_lane > 0:
            summary["top_entries"] = sorted(
                [*summary["top_entries"], entry],
                key=lambda top_entry: (
                    -top_entry["text_construction_count"],
                    top_entry["source_file"],
                    top_entry["member_name"],
                ),
            )[:top_per_lane]

    return {
        "schema_version": "1.0",
        "inventory": str(inventory_path),
        "lane_counts": payload["lane_counts"],
        "lanes": dict(sorted(lanes.items(), key=lambda item: LANE_ORDER[item[0]])),
    }


def format_surface_queue(
    inventory: TextConstructionInventory,
    *,
    inventory_path: Path,
    include: str = "valuable",
    limit: int | None = 50,
) -> str:
    """Format classified player-visible surface candidates for agent handoff."""
    payload = queue_payload(inventory, inventory_path=inventory_path, include=include)
    entries = payload["entries"] if limit is None else payload["entries"][:limit]
    total_entries = len(payload["entries"])
    lines = [
        f"text construction surface queue: {total_entries} entries; counts={_format_counter(payload['counts'])}",
        f"closure lanes: {_format_counter(payload['lane_counts'])}",
    ]

    for index, entry in enumerate(entries, start=1):
        surfaces = (
            entry["player_visible_surfaces"]
            or entry["contextual_surfaces"]
            or entry["construction_only_surfaces"]
        )
        lines.append(
            "".join(
                (
                    f"{index}. [{entry['classification']}/{entry['closure_lane']}] {entry['source_file']}:",
                    f"{entry['member_start_line']} {entry['type_name']}.{entry['member_signature']} ",
                    f"surfaces={','.join(surfaces)} ",
                    f"count={entry['text_construction_count']} ",
                    f"closure={entry['closure_status']}",
                )
            )
        )
        lines.append(f"   reason: {entry['reason']}")
        lines.append(f"   action: {entry['action']}")

    if limit is not None and total_entries > limit:
        lines.append(f"... {total_entries - limit} more entries omitted")

    return "\n".join(lines)


def main(argv: list[str] | None = None) -> int:
    """Classify a TextConstructionInventory JSON file."""
    parser = ArgumentParser(description="Classify player-visible text-construction surfaces.")
    _ = parser.add_argument("--inventory", type=Path, required=True)
    _ = parser.add_argument("--format", choices=("text", "json", "lanes-json"), default="text")
    _ = parser.add_argument(
        "--include",
        choices=("valuable", "all", "candidate-only", "non-target"),
        default="valuable",
    )
    _ = parser.add_argument("--limit", type=int, default=50, help="maximum text rows; 0 means all")
    args = parser.parse_args(argv)

    inventory_path = cast("Path", args.inventory)
    inventory = load_inventory(inventory_path)
    include = cast("str", args.include)
    output_format = cast("OutputFormat", args.format)
    limit_arg = cast("int", args.limit)
    limit = None if limit_arg == 0 else limit_arg

    if output_format == "json":
        payload = queue_payload(inventory, inventory_path=inventory_path, include=include)
        _ = sys.stdout.write(json.dumps(payload, ensure_ascii=False, indent=2, sort_keys=True) + "\n")
        return 0

    if output_format == "lanes-json":
        payload = lane_summary_payload(inventory, inventory_path=inventory_path, include=include)
        _ = sys.stdout.write(json.dumps(payload, ensure_ascii=False, indent=2, sort_keys=True) + "\n")
        return 0

    text = format_surface_queue(inventory, inventory_path=inventory_path, include=include, limit=limit)
    _ = sys.stdout.write(text + "\n")
    return 0


def _queue_entry(family: TextConstructionFamily) -> SurfaceQueueEntry:
    classified = classify_family(family)
    closure_lane = _closure_lane(family, classified)
    closure_status, closure_evidence = _closure_overlay(family["family_id"])
    return {
        "classification": classified["classification"],
        "closure_lane": closure_lane,
        "closure_status": closure_status,
        "closure_evidence": closure_evidence,
        "family_id": family["family_id"],
        "source_file": family["file"],
        "type_name": family["type_name"],
        "member_name": family["member_name"],
        "member_signature": family["member_signature"],
        "member_start_line": family["member_start_line"],
        "text_construction_count": family["text_construction_count"],
        "player_visible_surfaces": classified["player_visible_surfaces"],
        "contextual_surfaces": classified["contextual_surfaces"],
        "construction_only_surfaces": classified["construction_only_surfaces"],
        "non_target_surfaces": classified["non_target_surfaces"],
        "first_lines": family["first_lines"],
        "reason": classified["reason"],
        "action": classified["action"],
    }


def _closure_overlay(family_id: str) -> tuple[ClosureStatus, list[str]]:
    overlay = TEXT_CONSTRUCTION_CLOSURE_OVERLAY.get(family_id)
    if overlay is None:
        return "action_required", []
    return overlay["closure_status"], list(overlay["closure_evidence"])


def _closure_lane(family: TextConstructionFamily, classified: ClassifiedSurface) -> ClosureLane:
    surfaces = set(classified["player_visible_surfaces"]) | set(classified["contextual_surfaces"])
    file_path = family["file"]

    if surfaces & {"MessageFrame", "Does"}:
        lane: ClosureLane = "combat_message_frame_does"
    elif "HistoricStringExpander" in surfaces:
        lane = "history_generated_text"
    elif surfaces & {"SetText", "DirectTextAssignment"} and _has_prefix(file_path, UI_OWNER_FILE_PREFIXES):
        lane = "screen_ui_direct_text"
    elif surfaces & {"DisplayNameAssignment", "DisplayNameReturn", "DisplayTextReturn", "GetDisplayName"}:
        lane = "display_name_composition"
    elif surfaces & {"Description", "DescriptionAssignment", "DescriptionReturn", "EffectDescriptionReturn"}:
        lane = "description_effect_detail"
    elif "JournalAPI" in surfaces:
        lane = "journal_quest_routes"
    elif "ActivatedAbility" in surfaces:
        lane = "activated_ability_names"
    elif surfaces & {"AddPlayerMessage", "EmitMessage", "Popup", "TutorialManagerPopup"}:
        lane = "producer_message_popup"
    else:
        lane = "other_owner_candidate"
    return lane


def _is_player_visible_owner_candidate(family: TextConstructionFamily) -> bool:
    file_path = family["file"]
    if file_path.startswith(NON_PLAYER_FILE_NAMES) or _is_low_value_source_file(file_path):
        return False
    if _has_prefix(file_path, UI_OWNER_FILE_PREFIXES):
        return True
    if _has_prefix(file_path, GAMEPLAY_OWNER_FILE_PREFIXES):
        return _member_name_suggests_visible_text(family["member_name"]) or _has_semantic_assignment_surface(family)
    return False


def _has_semantic_assignment_surface(family: TextConstructionFamily) -> bool:
    surfaces = set(family["surface_counts"])
    return bool(surfaces & {"DescriptionAssignment", "DisplayNameAssignment"})


def _member_name_suggests_visible_text(member_name: str) -> bool:
    visible_terms = (
        "Description",
        "DisplayName",
        "DisplayText",
        "GetDetails",
        "GetTabString",
        "Render",
        "SetData",
        "Show",
        "UpdateView",
    )
    return any(term in member_name for term in visible_terms)


def _filter_entries(entries: list[SurfaceQueueEntry], include: str) -> list[SurfaceQueueEntry]:
    match include:
        case "valuable":
            return [entry for entry in entries if entry["classification"] in VALUABLE_CLASSIFICATIONS]
        case "candidate-only":
            return [entry for entry in entries if entry["classification"] == "candidate_only"]
        case "non-target":
            return [entry for entry in entries if entry["classification"] == "non_target"]
        case "all":
            return entries
        case _:
            msg = f"unsupported include mode: {include}"
            raise ValueError(msg)


def _has_prefix(value: str, prefixes: tuple[str, ...]) -> bool:
    return any(value.startswith(prefix) for prefix in prefixes)


def _is_low_value_source_file(file_path: str) -> bool:
    return _has_prefix(file_path, NON_PLAYER_FILE_PREFIXES) or any(part in file_path for part in LOW_VALUE_FILE_PARTS)


def _format_counter(counter: dict[str, int]) -> str:
    return ",".join(f"{key}:{counter[key]}" for key in sorted(counter))


if __name__ == "__main__":
    raise SystemExit(main())
