"""Tests for translate_annals_patterns.py."""
# ruff: noqa: D103, ANN401

from __future__ import annotations

import json
import sys
from pathlib import Path
from typing import Any
from unittest.mock import MagicMock, patch

# Importable as a module
sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
import translate_annals_patterns as tap


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
        "ja_template": "",
        "review_notes": "",
        "route": "annals",
        "en_template_hash": "",  # filled in by test setup
    }
    base.update(overrides)
    base["en_template_hash"] = tap.compute_en_template_hash(base)
    return base


def test_compute_en_template_hash_is_deterministic() -> None:
    c = _candidate()
    h1 = tap.compute_en_template_hash(c)
    h2 = tap.compute_en_template_hash(c)
    assert h1 == h2
    assert h1.startswith("sha256:")


def test_compute_en_template_hash_changes_on_pattern_change() -> None:
    c1 = _candidate()
    c2 = _candidate(extracted_pattern=r"^Resheph born$")
    h1 = tap.compute_en_template_hash(c1)
    h2 = tap.compute_en_template_hash(c2)
    assert h1 != h2


def test_compute_en_template_hash_ignores_status_and_ja_template() -> None:
    c1 = _candidate(status="pending", ja_template="X")
    c2 = _candidate(status="accepted", ja_template="Y")
    # status/ja_template are excluded from the hash per spec §3.6
    assert tap.compute_en_template_hash(c1) == tap.compute_en_template_hash(c2)


def test_select_pending_skips_already_translated_with_matching_hash() -> None:
    c = _candidate(ja_template="既に翻訳済み")
    pending = tap.select_pending_candidates([c])
    assert pending == []


def test_select_pending_picks_up_stale_translation() -> None:
    c = _candidate(ja_template="既に翻訳済み")
    # Simulate human edit that changes the pattern but not the cached hash
    c["extracted_pattern"] = r"^Different pattern$"
    pending = tap.select_pending_candidates([c])
    assert len(pending) == 1


def test_select_pending_picks_up_empty_template() -> None:
    c = _candidate(ja_template="")
    pending = tap.select_pending_candidates([c])
    assert len(pending) == 1


def test_select_pending_skips_non_accepted_status() -> None:
    c1 = _candidate(status="pending", ja_template="")
    c2 = _candidate(id="B", status="needs_manual", ja_template="")
    c3 = _candidate(id="C", status="skip", ja_template="")
    assert tap.select_pending_candidates([c1, c2, c3]) == []


def test_chunk_candidates_groups_5_to_8() -> None:
    cands = [_candidate(id=f"R#{i}") for i in range(13)]
    chunks = tap.chunk_candidates(cands)
    # 13 items at default chunk_size=8 -> [8, 5]
    assert [len(c) for c in chunks] == [8, 5]


def test_validate_chunk_response_rejects_missing_id() -> None:
    cands = [_candidate(id="A"), _candidate(id="B")]
    response = [{"id": "A", "ja_template": "X"}]  # missing B
    valid, errors = tap.validate_chunk_response(cands, response)
    assert not valid
    assert any("missing" in e.lower() or "B" in e for e in errors)


def test_validate_chunk_response_rejects_unknown_id() -> None:
    cands = [_candidate(id="A")]
    response = [{"id": "A", "ja_template": "X"}, {"id": "PHANTOM", "ja_template": "Y"}]
    valid, _ = tap.validate_chunk_response(cands, response)
    assert not valid


def test_validate_chunk_response_rejects_empty_ja_template() -> None:
    cands = [_candidate(id="A")]
    response = [{"id": "A", "ja_template": ""}]
    valid, _ = tap.validate_chunk_response(cands, response)
    assert not valid


def test_validate_chunk_response_rejects_placeholder_out_of_range() -> None:
    # pattern has 1 capture; response uses {t1} which is index 1, exceeds 1 capture
    cands = [_candidate(id="A", extracted_pattern=r"^Resheph (.+?)\.$")]
    response = [{"id": "A", "ja_template": "{t1}OK"}]
    valid, _ = tap.validate_chunk_response(cands, response)
    assert not valid


def test_validate_chunk_response_accepts_valid() -> None:
    cands = [_candidate(id="A")]
    response = [{"id": "A", "ja_template": "{t0}でレシェフが生まれた。"}]
    valid, errors = tap.validate_chunk_response(cands, response)
    assert valid, errors


@patch("subprocess.run")
def test_invoke_codex_returns_json_array_on_success(mock_run: MagicMock, tmp_path: Path) -> None:  # noqa: ARG001
    payload = json.dumps([{"id": "A", "ja_template": "{t0}OK"}])
    mock_run.return_value.returncode = 0
    mock_run.return_value.stdout = payload
    mock_run.return_value.stderr = ""

    result = tap.invoke_codex_translation([_candidate(id="A")], glossary="dummy")
    assert result == [{"id": "A", "ja_template": "{t0}OK"}]


@patch("subprocess.run")
def test_invoke_codex_returns_none_on_unparseable(mock_run: MagicMock) -> None:
    mock_run.return_value.returncode = 0
    mock_run.return_value.stdout = "not json"
    mock_run.return_value.stderr = ""
    result = tap.invoke_codex_translation([_candidate(id="A")], glossary="dummy")
    assert result is None


@patch("subprocess.run")
def test_invoke_codex_returns_none_on_nonzero_exit(mock_run: MagicMock) -> None:
    mock_run.return_value.returncode = 1
    mock_run.return_value.stdout = ""
    mock_run.return_value.stderr = "auth error"
    result = tap.invoke_codex_translation([_candidate(id="A")], glossary="dummy")
    assert result is None


def test_save_partial_progress_updates_in_place(tmp_path: Path) -> None:
    candidates = [_candidate(id="A"), _candidate(id="B")]
    doc = {"schema_version": "1", "candidates": candidates}
    p = tmp_path / "candidates.json"
    p.write_text(json.dumps(doc), encoding="utf-8")

    # Apply translation only for A
    candidates[0]["ja_template"] = "{t0}translated A"
    candidates[0]["en_template_hash"] = tap.compute_en_template_hash(candidates[0])
    tap.save_progress(p, doc)

    on_disk = json.loads(p.read_text(encoding="utf-8"))
    assert on_disk["candidates"][0]["ja_template"] == "{t0}translated A"
    assert on_disk["candidates"][1]["ja_template"] == ""
