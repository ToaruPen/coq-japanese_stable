from __future__ import annotations

from collections import Counter
from pathlib import Path

from scripts.generate_static_producer_semantic_batches import (
    DEFAULT_JSON_OUTPUT,
    DEFAULT_MARKDOWN_OUTPUT,
    EXPECTED_QUEUE_TOTALS,
    OutputPaths,
    build_payload,
    check_outputs,
)

REPO_ROOT = Path(__file__).resolve().parents[2]
TRACKED_INVENTORY = REPO_ROOT / "docs" / "static-producer-inventory.json"


def test_semantic_batches_assign_every_owner_queue_family_once() -> None:
    """The issue #576 planning artifact must be a complete, duplicate-free partition."""
    payload = build_payload(TRACKED_INVENTORY)

    assigned_family_ids = [
        family_id
        for batch in payload["batches"]
        for family_id in batch["producer_family_ids"]
    ]

    assert payload["queue_totals"]["family_count"] == EXPECTED_QUEUE_TOTALS["family_count"]
    assert payload["queue_totals"]["callsite_count"] == EXPECTED_QUEUE_TOTALS["callsite_count"]
    assert payload["queue_totals"]["text_argument_count"] == EXPECTED_QUEUE_TOTALS["text_argument_count"]
    assert payload["assignment_check"]["unique_family_count"] == EXPECTED_QUEUE_TOTALS["family_count"]
    assert payload["assignment_check"]["duplicate_family_ids"] == []
    assert payload["assignment_check"]["missing_family_ids"] == []
    assert len(assigned_family_ids) == len(set(assigned_family_ids))


def test_semantic_batch_counts_reconcile_to_family_rows() -> None:
    """Batch summary counts must equal the assigned family rows."""
    payload = build_payload(TRACKED_INVENTORY)

    for batch in payload["batches"]:
        families = batch["families"]
        closure_statuses: Counter[str] = Counter()
        surfaces: Counter[str] = Counter()
        for family in families:
            closure_statuses.update(family["closure_status_counts"])
            surfaces.update(family["surface_counts"])

        assert batch["counts"]["family_count"] == len(families)
        assert batch["counts"]["callsite_count"] == sum(family["callsite_count"] for family in families)
        assert batch["counts"]["text_argument_count"] == sum(family["text_argument_count"] for family in families)
        assert batch["closure_status_mix"] == dict(sorted(closure_statuses.items()))
        assert batch["surface_mix"] == dict(sorted(surfaces.items()))


def test_semantic_batch_outputs_are_current() -> None:
    """Tracked generated planning artifacts must match the deterministic generator."""
    payload = build_payload(TRACKED_INVENTORY)

    errors = check_outputs(
        payload,
        OutputPaths(DEFAULT_JSON_OUTPUT, DEFAULT_MARKDOWN_OUTPUT),
    )

    assert errors == []
