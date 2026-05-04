from __future__ import annotations

import xml.etree.ElementTree as ET

from scripts.tests._common import DICTIONARIES_ROOT, iter_dictionary_entries

LOCALIZATION_ROOT = DICTIONARIES_ROOT.parent


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
        translated_keys.update(
            key
            for _, entry in iter_dictionary_entries(dictionary_path)
            if isinstance((key := entry.get("key")), str)
            if entry.get("text") and key in expected_keys
        )

    assert expected_keys <= translated_keys
