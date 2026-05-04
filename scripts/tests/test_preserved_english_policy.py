"""Tests for the preserved-English policy."""

from __future__ import annotations

from scripts.tests._common import DICTIONARIES_ROOT, iter_dictionary_entries
from scripts.triage.preserved_policy import load_preserved_english_policy


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

    for dictionary_path in sorted(DICTIONARIES_ROOT.glob("*.ja.json")):
        for _, entry in iter_dictionary_entries(dictionary_path):
            key = entry.get("key")
            context = entry.get("context")
            if (context, key) in protected_entries:
                offenders.append((dictionary_path.name, str(context), str(key)))

    assert offenders == []
