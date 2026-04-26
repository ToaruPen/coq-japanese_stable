"""Assert that scripts/_artifacts/annals/ is gitignored."""

from __future__ import annotations

from pathlib import Path


def test_gitignore_lists_annals_artifact_directory() -> None:
    """scripts/_artifacts/annals/ must be present in .gitignore."""
    text = Path(".gitignore").read_text(encoding="utf-8")
    assert "scripts/_artifacts/annals/" in text, (
        "scripts/_artifacts/annals/ must be gitignored to prevent accidental "
        "commit of candidate JSON, conflict reports, and .bak backups."
    )
