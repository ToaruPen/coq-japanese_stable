"""Assert that transient Annals artifacts are gitignored."""

from __future__ import annotations

from pathlib import Path

_GITIGNORE_PATH = Path(".gitignore")


def _gitignore_lines() -> set[str]:
    return {
        line.strip()
        for line in _GITIGNORE_PATH.read_text(encoding="utf-8").splitlines()
        if line.strip() and not line.lstrip().startswith("#")
    }


def test_gitignore_lists_transient_annals_artifacts_without_hiding_review_source() -> None:
    """Only transient Annals artifacts should be ignored."""
    text = _GITIGNORE_PATH.read_text(encoding="utf-8")
    lines = _gitignore_lines()
    assert "scripts/_artifacts/annals/*.bak" in lines
    assert "scripts/_artifacts/annals/*.bak-*" in lines
    assert "scripts/_artifacts/annals/merge_conflicts.json" in lines
    assert "scripts/_artifacts/annals/candidates_pending.json" not in lines
    assert "scripts/_artifacts/annals/" not in lines
    assert "scripts/_artifacts/annals/*" not in lines
    assert "scripts/_artifacts/annals/**" not in lines
    assert "candidates_pending.json IS committed" in text


def test_gitignore_lists_local_workshop_state_directories() -> None:
    """Local Workshop inbox state contains raw comments and must not be committed."""
    lines = _gitignore_lines()
    assert ".coq-japanese_workshop/state/*" in lines
    assert ".coq-japanese_workshop/backups/*" in lines
    assert ".coq-japanese_workshop/exports/*" in lines
    assert "!.coq-japanese_workshop/README.md" in lines
    assert "!.coq-japanese_workshop/state/.gitkeep" in lines
    assert "!.coq-japanese_workshop/backups/.gitkeep" in lines
    assert "!.coq-japanese_workshop/exports/.gitkeep" in lines
