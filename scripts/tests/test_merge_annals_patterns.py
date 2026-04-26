"""Tests for merge_annals_patterns.py."""
# ruff: noqa: D103, ANN401

from __future__ import annotations

import importlib.util
import json
from pathlib import Path
from typing import Any

_REPO_ROOT = Path(__file__).resolve().parents[2]
_spec = importlib.util.spec_from_file_location(
    "merge_annals_patterns", _REPO_ROOT / "scripts" / "merge_annals_patterns.py"
)
assert _spec is not None
assert _spec.loader is not None
mer = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(mer)  # type: ignore[attr-defined]


def _candidate(**overrides: Any) -> dict[str, Any]:
    base = {
        "id": "ReshephIsBorn#default",
        "source_file": "ReshephIsBorn.cs",
        "annal_class": "ReshephIsBorn",
        "switch_case": "default",
        "event_property": "gospel",
        "sample_source": "Resheph was born in the salt marsh.",
        "extracted_pattern": r"^Resheph was born in (.+?)\.$",
        "slots": [{"index": 0, "type": "spice", "raw": "<spice...>", "default": "{t0}"}],
        "status": "accepted",
        "reason": "",
        "ja_template": "{t0}でレシェフが生まれた。",
        "review_notes": "",
        "route": "annals",
        "en_template_hash": "sha256:abc",
    }
    base.update(overrides)
    return base


def _doc(*candidates: dict[str, Any]) -> dict[str, Any]:
    return {"schema_version": "1", "candidates": list(candidates)}


def _journal_patterns(*entries: dict[str, str]) -> dict[str, Any]:
    return {"entries": [], "patterns": list(entries)}


def test_merge_filters_only_accepted_with_template(tmp_path: Path) -> None:
    candidates_path = tmp_path / "candidates.json"
    candidates_path.write_text(
        json.dumps(
            _doc(
                _candidate(id="A", status="accepted", ja_template="{t0}A"),
                _candidate(id="B", status="needs_manual", ja_template="ignored"),
                _candidate(id="C", status="skip", ja_template="ignored"),
                _candidate(id="D", status="pending", ja_template=""),
                _candidate(id="E", status="pending", ja_template="ignored"),
            )
        ),
        encoding="utf-8",
    )

    journal_path = tmp_path / "journal-patterns.ja.json"
    journal_path.write_text(json.dumps(_journal_patterns()), encoding="utf-8")
    annals_path = tmp_path / "annals-patterns.ja.json"
    conflicts_path = tmp_path / "merge_conflicts.json"

    code = mer.run_merge(
        candidates_path=candidates_path,
        existing_journal=journal_path,
        annals_output=annals_path,
        conflicts_output=conflicts_path,
    )
    assert code == 0

    on_disk = json.loads(annals_path.read_text(encoding="utf-8"))
    assert on_disk["entries"] == []
    # The shipped schema is {pattern, template, route} only; just count
    assert len(on_disk["patterns"]) == 1


def test_merge_emits_empty_patterns_when_no_accepted(tmp_path: Path) -> None:
    candidates_path = tmp_path / "candidates.json"
    candidates_path.write_text(
        json.dumps(
            _doc(
                _candidate(id="A", status="needs_manual", ja_template=""),
            )
        ),
        encoding="utf-8",
    )

    journal_path = tmp_path / "journal-patterns.ja.json"
    journal_path.write_text(json.dumps(_journal_patterns()), encoding="utf-8")
    annals_path = tmp_path / "annals-patterns.ja.json"
    conflicts_path = tmp_path / "merge_conflicts.json"

    code = mer.run_merge(
        candidates_path=candidates_path,
        existing_journal=journal_path,
        annals_output=annals_path,
        conflicts_output=conflicts_path,
    )
    assert code == 0
    on_disk = json.loads(annals_path.read_text(encoding="utf-8"))
    assert on_disk == {"entries": [], "patterns": []}


def test_merge_detects_raw_collision(tmp_path: Path) -> None:
    candidates_path = tmp_path / "candidates.json"
    candidates_path.write_text(
        json.dumps(
            _doc(
                _candidate(id="A", sample_source="Resheph was born in the salt marsh."),
            )
        ),
        encoding="utf-8",
    )
    journal_path = tmp_path / "journal-patterns.ja.json"
    # Existing pattern that would already swallow the candidate's sample
    journal_path.write_text(
        json.dumps(
            _journal_patterns({"pattern": r"^Resheph was born in (.+?)\.$", "template": "old", "route": "journal"})
        ),
        encoding="utf-8",
    )

    annals_path = tmp_path / "annals-patterns.ja.json"
    conflicts_path = tmp_path / "merge_conflicts.json"

    code = mer.run_merge(
        candidates_path=candidates_path,
        existing_journal=journal_path,
        annals_output=annals_path,
        conflicts_output=conflicts_path,
    )
    assert code == 1
    assert conflicts_path.exists()
    conflict_doc = json.loads(conflicts_path.read_text(encoding="utf-8"))
    assert conflict_doc["schema_version"] == "1"
    assert len(conflict_doc["conflicts"]) >= 1
    assert conflict_doc["conflicts"][0]["conflict_type"] == "raw"
    assert "skip_candidate" in conflict_doc["conflicts"][0]["resolution_options"]


def test_merge_detects_normalized_collision(tmp_path: Path) -> None:
    """An existing broad pattern that swallows the slot-normalized form."""
    candidates_path = tmp_path / "candidates.json"
    candidates_path.write_text(
        json.dumps(
            _doc(
                _candidate(
                    id="A",
                    sample_source="Resheph was born in the salt marsh.",
                    slots=[{"index": 0, "type": "spice", "raw": "the salt marsh", "default": "{t0}"}],
                    extracted_pattern=r"^Resheph was born in (.+?)\.$",
                ),
            )
        ),
        encoding="utf-8",
    )
    journal_path = tmp_path / "journal-patterns.ja.json"
    # Existing pattern matches the normalized form (where 'the salt marsh' became 'SLOT0')
    journal_path.write_text(
        json.dumps(
            _journal_patterns({"pattern": r"^Resheph was born in SLOT0\.$", "template": "old", "route": "journal"})
        ),
        encoding="utf-8",
    )

    annals_path = tmp_path / "annals-patterns.ja.json"
    conflicts_path = tmp_path / "merge_conflicts.json"

    code = mer.run_merge(
        candidates_path=candidates_path,
        existing_journal=journal_path,
        annals_output=annals_path,
        conflicts_output=conflicts_path,
    )
    assert code == 1
    conflict_doc = json.loads(conflicts_path.read_text(encoding="utf-8"))
    assert any(c["conflict_type"] == "normalized" for c in conflict_doc["conflicts"])
    assert any("narrow_candidate_pattern" in c["resolution_options"] for c in conflict_doc["conflicts"])


def test_merge_calls_validate_and_rejects_invalid_candidate(tmp_path: Path) -> None:
    candidates_path = tmp_path / "candidates.json"
    candidates_path.write_text(
        json.dumps(
            _doc(
                _candidate(id="A", status="accepted", ja_template="", extracted_pattern=r"^.+$"),
                # accepted with empty ja_template is invalid per spec §3.3
            )
        ),
        encoding="utf-8",
    )
    journal_path = tmp_path / "journal-patterns.ja.json"
    journal_path.write_text(json.dumps(_journal_patterns()), encoding="utf-8")

    annals_path = tmp_path / "annals-patterns.ja.json"
    conflicts_path = tmp_path / "merge_conflicts.json"
    code = mer.run_merge(
        candidates_path=candidates_path,
        existing_journal=journal_path,
        annals_output=annals_path,
        conflicts_output=conflicts_path,
    )
    assert code == 1


def test_merge_clean_run_deletes_stale_conflicts_artifact(tmp_path: Path) -> None:
    candidates_path = tmp_path / "candidates.json"
    candidates_path.write_text(json.dumps(_doc(_candidate(id="A"))), encoding="utf-8")
    journal_path = tmp_path / "journal-patterns.ja.json"
    journal_path.write_text(json.dumps(_journal_patterns()), encoding="utf-8")
    annals_path = tmp_path / "annals-patterns.ja.json"
    conflicts_path = tmp_path / "merge_conflicts.json"

    # Pre-create a stale conflicts artifact
    conflicts_path.write_text(json.dumps({"schema_version": "1", "conflicts": [{"old": "stale"}]}), encoding="utf-8")
    assert conflicts_path.exists()

    code = mer.run_merge(
        candidates_path=candidates_path,
        existing_journal=journal_path,
        annals_output=annals_path,
        conflicts_output=conflicts_path,
    )
    assert code == 0
    assert not conflicts_path.exists()


def test_merge_output_is_deterministic(tmp_path: Path) -> None:
    candidates_path = tmp_path / "candidates.json"
    candidates_path.write_text(
        json.dumps(
            _doc(
                _candidate(id="ZZZ", extracted_pattern=r"^Z (.+?)$", ja_template="{t0}Z"),
                _candidate(id="AAA", extracted_pattern=r"^A (.+?)$", ja_template="{t0}A"),
            )
        ),
        encoding="utf-8",
    )
    journal_path = tmp_path / "journal-patterns.ja.json"
    journal_path.write_text(json.dumps(_journal_patterns()), encoding="utf-8")
    annals_path1 = tmp_path / "annals1.json"
    annals_path2 = tmp_path / "annals2.json"
    conflicts_path = tmp_path / "merge_conflicts.json"

    mer.run_merge(candidates_path, journal_path, annals_path1, conflicts_path)
    mer.run_merge(candidates_path, journal_path, annals_path2, conflicts_path)

    assert annals_path1.read_text(encoding="utf-8") == annals_path2.read_text(encoding="utf-8")


def test_merge_rejects_malformed_existing_annals(tmp_path: Path) -> None:
    candidates_path = tmp_path / "candidates.json"
    candidates_path.write_text(json.dumps(_doc(_candidate(id="A"))), encoding="utf-8")
    journal_path = tmp_path / "journal-patterns.ja.json"
    journal_path.write_text(json.dumps(_journal_patterns()), encoding="utf-8")
    annals_path = tmp_path / "annals-patterns.ja.json"
    conflicts_path = tmp_path / "merge_conflicts.json"

    # Pre-existing malformed annals (missing patterns array)
    annals_path.write_text(json.dumps({"entries": []}), encoding="utf-8")
    code = mer.run_merge(candidates_path, journal_path, annals_path, conflicts_path)
    assert code == 1
