"""Schema validation for annals candidate JSON."""
# ruff: noqa: ANN401, S603, D103

from __future__ import annotations

import json
import subprocess
import sys
from pathlib import Path
from typing import Any

SCRIPT = Path("scripts/validate_candidate_schema.py")


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
        "status": "pending",
        "reason": "",
        "ja_template": "",
        "review_notes": "",
        "route": "annals",
        "en_template_hash": "sha256:abc",
    }
    base.update(overrides)
    return base


def _doc(*candidates: dict[str, Any], schema_version: str = "1") -> dict[str, Any]:
    return {"schema_version": schema_version, "candidates": list(candidates)}


def _run(tmp_path: Path, doc: dict[str, Any]) -> subprocess.CompletedProcess[str]:
    p = tmp_path / "candidates.json"
    p.write_text(json.dumps(doc), encoding="utf-8")
    return subprocess.run(
        [sys.executable, str(SCRIPT), str(p)],
        capture_output=True,
        text=True,
        check=False,
    )


def test_validate_passes_for_minimal_doc(tmp_path: Path) -> None:
    result = _run(tmp_path, _doc(_candidate()))
    assert result.returncode == 0, result.stderr


def test_validate_passes_for_accepted_with_template(tmp_path: Path) -> None:
    result = _run(tmp_path, _doc(_candidate(status="accepted", ja_template="{t0}でレシェフが生まれた。")))
    assert result.returncode == 0, result.stderr


def test_validate_fails_when_schema_version_missing(tmp_path: Path) -> None:
    # Bypasses _run() intentionally: the doc is missing "schema_version" entirely,
    # so it cannot be constructed via _doc() which always injects schema_version.
    p = tmp_path / "candidates.json"
    p.write_text(json.dumps({"candidates": [_candidate()]}), encoding="utf-8")
    result = subprocess.run([sys.executable, str(SCRIPT), str(p)], capture_output=True, text=True, check=False)
    assert result.returncode == 1
    assert "schema_version" in result.stderr.lower()


def test_validate_fails_when_schema_version_wrong(tmp_path: Path) -> None:
    result = _run(tmp_path, _doc(_candidate(), schema_version="2"))
    assert result.returncode == 1
    assert "schema_version" in result.stderr.lower()


def test_validate_fails_when_unknown_top_level_field(tmp_path: Path) -> None:
    doc = _doc(_candidate())
    doc["unknown_top"] = "boom"
    result = _run(tmp_path, doc)
    assert result.returncode == 1
    assert "unknown" in result.stderr.lower()


def test_validate_fails_for_invalid_status(tmp_path: Path) -> None:
    result = _run(tmp_path, _doc(_candidate(status="bogus")))
    assert result.returncode == 1
    assert "status" in result.stderr.lower()


def test_validate_fails_when_accepted_has_empty_ja_template(tmp_path: Path) -> None:
    result = _run(tmp_path, _doc(_candidate(status="accepted", ja_template="")))
    assert result.returncode == 1
    assert "ja_template" in result.stderr.lower()


def test_validate_fails_when_extracted_pattern_invalid_regex(tmp_path: Path) -> None:
    result = _run(tmp_path, _doc(_candidate(extracted_pattern="(unclosed")))
    assert result.returncode == 1
    assert "regex" in result.stderr.lower() or "compile" in result.stderr.lower()


def test_validate_fails_when_placeholder_index_out_of_range(tmp_path: Path) -> None:
    # extracted_pattern has 1 capture; ja_template references {t1}, which is index 1 (i.e. 2nd capture).
    doc = _doc(
        _candidate(
            status="accepted",
            ja_template="{t1}foo",
            extracted_pattern=r"^Resheph was born in (.+?)\.$",
        )
    )
    result = _run(tmp_path, doc)
    assert result.returncode == 1
    assert "placeholder" in result.stderr.lower() or "index" in result.stderr.lower()


def test_validate_fails_when_id_duplicate(tmp_path: Path) -> None:
    result = _run(tmp_path, _doc(_candidate(id="A"), _candidate(id="A")))
    assert result.returncode == 1
    assert "duplicate" in result.stderr.lower() or "unique" in result.stderr.lower()


def test_validate_fails_when_required_field_missing(tmp_path: Path) -> None:
    bad = _candidate()
    del bad["sample_source"]
    result = _run(tmp_path, _doc(bad))
    assert result.returncode == 1
    assert "sample_source" in result.stderr.lower() or "missing" in result.stderr.lower()


def test_validate_passes_with_review_notes_allowlisted(tmp_path: Path) -> None:
    # review_notes is allowlisted per spec §3.3 even though it's not strictly required
    c = _candidate(review_notes="reviewed by foo on 2026-04-26")
    result = _run(tmp_path, _doc(c))
    assert result.returncode == 0
