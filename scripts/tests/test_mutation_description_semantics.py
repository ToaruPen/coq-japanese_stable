from __future__ import annotations

import json
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
MUTATION_DESCRIPTIONS = (
    REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries" / "mutation-descriptions.ja.json"
)


def _mutation_descriptions() -> dict[str, str]:
    data = json.loads(MUTATION_DESCRIPTIONS.read_text(encoding="utf-8"))
    return {entry["key"]: entry["text"] for entry in data["entries"]}


def test_mutation_description_semantic_audit_fixes_remain_in_place() -> None:
    """Guard source-verified mutation description corrections from regressing."""
    entries = _mutation_descriptions()

    assert entries["mutation:Unstable Genome"].startswith("購入するたびに余分な突然変異を1つ得る")
    assert "突然変異ポイントを余計" not in entries["mutation:Unstable Genome"]

    assert "{{rules|+1}} 自我" in entries["mutation:Beak"]
    assert "意力" not in entries["mutation:Beak"]

    assert "突然変異ランクか自我修正の高い方" in entries["mutation:Beguiling"]
    assert "突然変異ランクか自我修正の高い方" in entries["mutation:Domination"]

    assert "予備ターンを得て、多くの工匠系および一部の儀式系シフラで役立つ" in entries["mutation:Psychometry"]
    assert "シフラではほとんどの工匠系で1ターン、ハッキング系で2ターン失う" in entries["mutation:Dystechnia"]
