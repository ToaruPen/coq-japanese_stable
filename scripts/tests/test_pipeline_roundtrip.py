"""End-to-end round-trip: extract → manually inject ja_template → merge → JSON loadable."""
# ruff: noqa: S603

from __future__ import annotations

import json
import shutil
import subprocess
import sys
from pathlib import Path

import pytest

FIXTURES = Path("scripts/tests/fixtures/annals")
ROOT = Path.cwd()


@pytest.mark.skipif(not shutil.which("dotnet"), reason="dotnet SDK not available")
def test_pipeline_roundtrip_simple_concat(tmp_path: Path) -> None:
    """Extract → validate → inject ja_template → re-validate → merge must all succeed."""
    candidates_path = tmp_path / "candidates.json"
    journal_path = tmp_path / "journal-patterns.ja.json"
    annals_path = tmp_path / "annals.json"
    conflicts_path = tmp_path / "conflicts.json"

    # Seed the journal so collision check has a baseline
    journal_path.write_text(
        json.dumps(
            {
                "entries": [],
                "patterns": [],
            }
        ),
        encoding="utf-8",
    )

    # Extract
    result = subprocess.run(
        [
            sys.executable,
            "scripts/extract_annals_patterns.py",
            "--source-root",
            str(FIXTURES),
            "--include",
            "simple_concat.cs",
            "--output",
            str(candidates_path),
        ],
        capture_output=True,
        text=True,
        check=False,
    )
    assert result.returncode == 0, result.stderr

    # Validate
    result = subprocess.run(
        [sys.executable, "scripts/validate_candidate_schema.py", str(candidates_path)],
        capture_output=True,
        text=True,
        check=False,
    )
    assert result.returncode == 0, result.stderr

    # Manually inject ja_template (mocking the Codex translate stage)
    doc = json.loads(candidates_path.read_text(encoding="utf-8"))
    for c in doc["candidates"]:
        if c["status"] == "pending":
            c["status"] = "accepted"
            # Use a template referencing only valid capture indices
            captures = c["extracted_pattern"].count("(")
            placeholders = "".join(f"{{t{i}}}" for i in range(captures))
            c["ja_template"] = placeholders + "テスト用日本語"
    candidates_path.write_text(json.dumps(doc, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    # Re-validate after edits
    result = subprocess.run(
        [sys.executable, "scripts/validate_candidate_schema.py", str(candidates_path)],
        capture_output=True,
        text=True,
        check=False,
    )
    assert result.returncode == 0, result.stderr

    # Merge
    result = subprocess.run(
        [
            sys.executable,
            "scripts/merge_annals_patterns.py",
            str(candidates_path),
            "--journal",
            str(journal_path),
            "--annals-output",
            str(annals_path),
            "--conflicts-output",
            str(conflicts_path),
        ],
        capture_output=True,
        text=True,
        check=False,
    )
    assert result.returncode == 0, result.stderr

    # Final assertion: the round-tripped output is a valid pattern dictionary
    final = json.loads(annals_path.read_text(encoding="utf-8"))
    assert "entries" in final
    assert final["entries"] == []
    assert "patterns" in final
    assert all("pattern" in p for p in final["patterns"])
    assert all("template" in p for p in final["patterns"])
    assert all(p.get("route") == "annals" for p in final["patterns"])
