from __future__ import annotations

import json
import xml.etree.ElementTree as ET
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
LOCALIZATION_ROOT = REPO_ROOT / "Mods" / "QudJP" / "Localization"


def test_embark_module_window_names_preserve_runtime_keys() -> None:
    """Window names are runtime panel keys used by tutorial chargen flow."""
    expected_names = [
        "Game Modes",
        "CharTypes",
        "Build Library",
        "Genotypes",
        "Pregens",
        "Subtypes",
        "Subtypes with Category",
        "Mutations",
        "Attributes",
        "Cybernetics",
        "Summary",
        "Customize",
        "Starting Location",
    ]

    tree = ET.parse(LOCALIZATION_ROOT / "EmbarkModules.jp.xml")  # noqa: S314
    actual_names = [
        name.text
        for name in tree.findall("./module/window/name")
    ]

    assert actual_names == expected_names


def test_embark_module_window_runtime_keys_have_display_translations() -> None:
    """Runtime window keys still need dictionary entries for display text."""
    dictionary_paths = [
        LOCALIZATION_ROOT / "Dictionaries" / "ui-chargen.ja.json",
        LOCALIZATION_ROOT / "Dictionaries" / "ui-default.ja.json",
    ]
    expected_keys = {
        "Game Modes",
        "CharTypes",
        "Build Library",
        "Genotypes",
        "Pregens",
        "Subtypes",
        "Subtypes with Category",
        "Mutations",
        "Attributes",
        "Cybernetics",
        "Summary",
        "Customize",
        "Starting Location",
    }

    translated_keys: set[str] = set()
    for dictionary_path in dictionary_paths:
        payload = json.loads(dictionary_path.read_text(encoding="utf-8"))
        translated_keys.update(
            entry["key"]
            for entry in payload["entries"]
            if entry.get("text") and entry.get("key") in expected_keys
        )

    assert expected_keys <= translated_keys
