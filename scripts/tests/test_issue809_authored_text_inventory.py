from __future__ import annotations

# pyright: reportAny=false, reportUnusedCallResult=false
import json
from pathlib import Path

from scripts.issue809_authored_text_inventory import build_inventory

REPO_ROOT = Path(__file__).resolve().parents[2]


def test_build_inventory_classifies_conversation_runtime_terms_and_choice_parts(tmp_path: Path) -> None:
    """The builder classifies authored conversation variables, choices, and quest text."""
    localization = tmp_path / "Mods" / "QudJP" / "Localization"
    localization.mkdir(parents=True)
    (localization / "Conversations.jp.xml").write_text(
        """<?xml version="1.0" encoding="utf-8"?>
<conversations Load="Merge">
  <conversation ID="BaseConversation">
    <choice ID="Greeting">
      <text>生きて飲め、=player.formalAddressTerm=。</text>
      <part Name="WaterRitualBegin" />
    </choice>
    <choice ID="Action">
      <text>それを=verb:give=して、=bodypart:*=を差し出せ。</text>
    </choice>
  </conversation>
</conversations>
""",
        encoding="utf-8",
    )
    (localization / "HiddenConversations.jp.xml").write_text(
        """<?xml version="1.0" encoding="utf-8"?>
<conversations Load="Merge">
  <conversation ID="Hidden">
    <node ID="Body"><text>また会おう。</text></node>
  </conversation>
</conversations>
""",
        encoding="utf-8",
    )
    (localization / "Quests.jp.xml").write_text(
        """<?xml version="1.0" encoding="utf-8"?>
<quests>
  <quest Name="Quest Title" Accomplishment="成し遂げた。">
    <step ID="Find the Thing" Name="物を探す"><text>物を見つける。</text></step>
  </quest>
</quests>
""",
        encoding="utf-8",
    )

    inventory = build_inventory(tmp_path)

    conversation = inventory["conversation_files"][0]
    assert conversation["path"] == "Mods/QudJP/Localization/Conversations.jp.xml"
    assert conversation["counts"]["texts"] == 2
    assert conversation["counts"]["choice_parts"] == 1
    assert conversation["runtime_expansion_terms"] == [
        {
            "term": "bodypart:*",
            "surface": "conversation_text",
            "examples": ["それを=verb:give=して、=bodypart:*=を差し出せ。"],
        },
        {
            "term": "player.formalAddressTerm",
            "surface": "conversation_text",
            "examples": ["生きて飲め、=player.formalAddressTerm=。"],
        },
        {
            "term": "verb:give",
            "surface": "conversation_text",
            "examples": ["それを=verb:give=して、=bodypart:*=を差し出せ。"],
        },
    ]
    assert conversation["choice_parts"] == [
        {
            "name": "WaterRitualBegin",
            "surface": "conversation_choice_part",
            "count": 1,
        }
    ]

    quest = inventory["quest_file"]
    assert quest["counts"]["quests"] == 1
    assert quest["counts"]["quest_titles_excluded"] == 1
    assert quest["counts"]["quest_metadata_texts"] == 1
    assert quest["counts"]["quest_steps"] == 1
    assert quest["counts"]["quest_step_texts"] == 1
    assert quest["quest_titles_translation_policy"] == "excluded"


def test_tracked_issue809_authored_text_inventory_covers_current_assets() -> None:
    """The tracked inventory covers current issue #809 authored XML assets."""
    inventory_path = REPO_ROOT / "docs" / "issue-809-authored-text-inventory.json"
    inventory = json.loads(inventory_path.read_text(encoding="utf-8"))

    assert inventory["issue"] == 809
    assert inventory["schema_version"] == "1.0"
    assert {entry["path"] for entry in inventory["conversation_files"]} == {
        "Mods/QudJP/Localization/Conversations.jp.xml",
        "Mods/QudJP/Localization/HiddenConversations.jp.xml",
    }
    assert inventory["quest_file"]["path"] == "Mods/QudJP/Localization/Quests.jp.xml"
    assert inventory["quest_file"]["quest_titles_translation_policy"] == "excluded"
    assert inventory["quest_file"]["counts"]["quest_titles_excluded"] > 0
    assert inventory["totals"]["conversation_runtime_expansion_terms"] > 0


def test_tracked_issue809_authored_text_inventory_matches_builder_output() -> None:
    """The tracked issue #809 authored-text inventory is not stale."""
    inventory_path = REPO_ROOT / "docs" / "issue-809-authored-text-inventory.json"
    inventory = json.loads(inventory_path.read_text(encoding="utf-8"))

    assert inventory == build_inventory(REPO_ROOT)


def test_issue809_authored_xml_has_no_tracked_english_residue() -> None:
    """Tracked authored-text residues are translated in visible XML surfaces."""
    localization = REPO_ROOT / "Mods" / "QudJP" / "Localization"
    residues_by_file = {
        "Conversations.jp.xml": [
            "kicksoft",
            "crungle",
            "tink\uff09",
            "sunslag",
            "mush room",
            "make-me",
        ],
        "HiddenConversations.jp.xml": [
            "eeehw",
            "Tremble in fear",
            "{{R|EXIT}}",
            "Spoken Ionic Through Covalency Heart",
            "Star Orchid Temple",
        ],
        "Quests.jp.xml": [
            "\u5f7c\uff0f\u5f7c\u5973\uff0fx",
            "\u5f7c\uff0f\u5f7c\u5973\uff0fey",
            "-else化",
        ],
    }

    failures = []
    for file_name, residues in residues_by_file.items():
        text = (localization / file_name).read_text(encoding="utf-8")
        failures.extend(
            f"{file_name}: {residue}"
            for residue in residues
            if residue in text
        )

    assert failures == []
