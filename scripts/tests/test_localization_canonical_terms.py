from __future__ import annotations

from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
LOCALIZATION_ROOT = REPO_ROOT / "Mods" / "QudJP" / "Localization"


def test_snapjaw_visible_assets_use_canonical_long_vowel() -> None:
    """Visible localization assets must use スナップジョー, not stale snapjaw variants."""
    stale_occurrences: list[str] = []
    for path in sorted(LOCALIZATION_ROOT.rglob("*")):
        if not path.is_file() or path.suffix not in {".json", ".xml", ".txt"}:
            continue
        text = path.read_text(encoding="utf-8")
        if "スナップジョウ" not in text:
            continue
        relative_path = path.relative_to(LOCALIZATION_ROOT).as_posix()
        stale_occurrences.append(relative_path)

    assert stale_occurrences == []
