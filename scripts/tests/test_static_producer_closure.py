from __future__ import annotations

from pathlib import Path

from scripts.static_producer_closure import (
    COVERED_BY_OWNER_PATCH,
    COVERED_OWNER_FAMILIES,
    covered_family_ids,
    family_closure_status,
    format_owner_action_queue,
    load_inventory,
    owner_action_queue,
    owner_action_queue_by_file,
    validate_covered_owner_families,
)

REPO_ROOT = Path(__file__).resolve().parents[2]
TRACKED_INVENTORY = REPO_ROOT / "docs" / "static-producer-inventory.json"


def test_covered_owner_registry_has_unique_family_ids() -> None:
    """Covered-family registry entries must be unique."""
    family_ids = [family.family_id for family in COVERED_OWNER_FAMILIES]

    assert len(family_ids) == len(set(family_ids))


def test_covered_owner_families_have_current_source_and_test_evidence() -> None:
    """Covered families must point at current source and test evidence."""
    inventory = load_inventory(TRACKED_INVENTORY)

    errors = validate_covered_owner_families(inventory, REPO_ROOT)

    assert errors == []


def _declaring_type_from_family_id(family_id: str) -> str:
    return family_id.split("::", maxsplit=1)[1].rsplit(".", maxsplit=1)[0]


def _member_name_from_family_id(family_id: str) -> str:
    return family_id.rsplit(".", maxsplit=1)[1]


def _is_owner_target_resolution_token(
    token: str, *, declaring_type: str, member_name: str
) -> bool:
    return "|" in token and declaring_type in token and member_name in token


def test_owner_patch_l2g_evidence_is_family_specific() -> None:
    """Owner-patch L2G evidence must identify the upstream owner method."""
    problems: list[str] = []

    for family in COVERED_OWNER_FAMILIES:
        if family.inventory_statuses != ("owner_patch_required",):
            continue

        declaring_type = _declaring_type_from_family_id(family.family_id)
        member_name = _member_name_from_family_id(family.family_id)
        for evidence in family.evidence_files:
            if not evidence.path.endswith("L2G/TargetMethodResolutionTests.cs"):
                continue

            has_full_target_token = any(
                _is_owner_target_resolution_token(
                    token, declaring_type=declaring_type, member_name=member_name
                )
                for token in evidence.required_substrings
            )
            has_declaring_type = any(
                declaring_type in token for token in evidence.required_substrings
            )
            has_member_name = any(member_name in token for token in evidence.required_substrings)

            if not has_full_target_token and not (has_declaring_type and has_member_name):
                problem = (
                    f"{family.family_id}: L2G evidence must include a full target signature "
                    f"or both the upstream declaring type and member name"
                )
                problems.append(problem)

    assert problems == []


def test_covered_owner_families_are_removed_from_owner_action_queue() -> None:
    """Covered owner families must not remain in the owner implementation queue."""
    inventory = load_inventory(TRACKED_INVENTORY)
    raw_families = {family["producer_family_id"]: family for family in inventory["families"]}

    for family_id in covered_family_ids():
        assert raw_families[family_id]["family_closure_status"] in {
            "owner_patch_required",
            "needs_family_review",
        }
        assert family_closure_status(raw_families[family_id]) == COVERED_BY_OWNER_PATCH

    queued_family_ids = {family["producer_family_id"] for family in owner_action_queue(inventory)}

    assert queued_family_ids.isdisjoint(covered_family_ids())


def test_game_object_heal_owner_family_is_closed_by_current_owner_tests() -> None:
    """GameObject.Heal has current owner-route evidence and must stay out of the queue."""
    inventory = load_inventory(TRACKED_INVENTORY)
    raw_families = {family["producer_family_id"]: family for family in inventory["families"]}
    family_id = "XRL.World/GameObject.cs::XRL.World.GameObject.Heal"

    assert raw_families[family_id]["family_closure_status"] == "owner_patch_required"
    assert family_closure_status(raw_families[family_id]) == COVERED_BY_OWNER_PATCH
    assert family_id not in {
        family["producer_family_id"]
        for family in owner_action_queue(inventory)
    }


def test_uncovered_high_volume_owner_family_remains_in_owner_action_queue() -> None:
    """Uncovered high-volume owner families must stay actionable."""
    inventory = load_inventory(TRACKED_INVENTORY)
    queued_family_ids = {family["producer_family_id"] for family in owner_action_queue(inventory)}

    assert "XRL.World.Parts/Combat.cs::XRL.World.Parts.Combat.MeleeAttackWithWeaponInternal" in queued_family_ids


def test_owner_action_queue_groups_actionable_work_by_source_file() -> None:
    """Static producer work queue must expose class-file starting points."""
    inventory = load_inventory(TRACKED_INVENTORY)
    source_entries = owner_action_queue_by_file(inventory)
    combat_entry = next(entry for entry in source_entries if entry["source_file"] == "XRL.World.Parts/Combat.cs")

    assert source_entries == sorted(
        source_entries,
        key=lambda entry: (
            -entry["family_count"],
            -entry["text_argument_count"],
            -entry["callsite_count"],
            entry["source_file"],
        ),
    )
    assert combat_entry["family_count"] > 0
    assert combat_entry["text_argument_count"] > 0
    assert any(
        family["producer_family_id"]
        == "XRL.World.Parts/Combat.cs::XRL.World.Parts.Combat.MeleeAttackWithWeaponInternal"
        for family in combat_entry["families"]
    )


def test_owner_action_queue_text_summary_names_source_files_and_methods() -> None:
    """Text output must be useful as an agent handoff queue."""
    inventory = load_inventory(TRACKED_INVENTORY)

    summary = format_owner_action_queue(inventory, limit=5)

    assert "owner action queue:" in summary
    assert ".cs:" in summary
    assert "surfaces=" in summary
    assert "line " in summary
