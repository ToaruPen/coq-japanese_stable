from __future__ import annotations

import json
from pathlib import Path
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from collections.abc import Iterator, Mapping

REPO_ROOT = Path(__file__).resolve().parents[2]
DICTIONARIES_ROOT = REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"


def iter_dictionary_entries(path: Path) -> Iterator[tuple[int, Mapping[str, object]]]:
    """Yield 1-based dictionary entry indexes with object entries from a JSON asset."""
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as exc:
        rel = path.relative_to(REPO_ROOT) if path.is_relative_to(REPO_ROOT) else path
        msg = f"Failed to read/parse {rel}: {exc}"
        raise ValueError(msg) from exc

    raw_entries = data.get("entries", []) if isinstance(data, dict) else data
    if not isinstance(raw_entries, list):
        return

    for index, entry in enumerate(raw_entries, start=1):
        if isinstance(entry, dict):
            yield index, entry
