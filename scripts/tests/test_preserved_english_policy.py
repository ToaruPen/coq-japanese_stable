"""Tests for the preserved-English policy."""

from __future__ import annotations

import json
from pathlib import Path

from scripts.triage.preserved_policy import load_preserved_english_policy

REPO_ROOT = Path(__file__).resolve().parents[2]
DICTIONARY_DIR = REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"


def test_policy_loads_route_aware_rules() -> None:
    """The preserved-English policy is machine readable and route-aware."""
    policy = load_preserved_english_policy()
    rule = next(rule for rule in policy.rules if rule.id == "character_attribute_line_short_labels")
    assert policy.version == 1
    assert rule.matches_route("CharacterAttributeLineTranslationPatch")
    assert rule.matches_text("+1 STR")
    assert not rule.matches_route("InventoryAndEquipmentStatusScreenTranslationPatch")


def test_policy_protected_dictionary_entries_are_not_registered() -> None:
    """Dictionary assets must not re-register tokens protected by policy contexts."""
    policy = load_preserved_english_policy()
    protected_entries = {
        (context, text)
        for rule in policy.rules
        for context in rule.dictionary_contexts
        for text in rule.texts
    }
    offenders: list[tuple[str, str, str]] = []

    for dictionary_path in sorted(DICTIONARY_DIR.glob("*.ja.json")):
        payload = json.loads(dictionary_path.read_text(encoding="utf-8"))
        for entry in payload.get("entries", ()):
            key = entry.get("key")
            context = entry.get("context")
            if (context, key) in protected_entries:
                offenders.append((dictionary_path.name, str(context), str(key)))

    assert offenders == []
