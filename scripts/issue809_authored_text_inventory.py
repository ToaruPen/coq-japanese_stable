"""Build the tracked authored-text inventory for issue #809."""

# pyright: reportAny=false, reportExplicitAny=false, reportUnusedCallResult=false

from __future__ import annotations

import argparse
import json
import re
import xml.etree.ElementTree as ET
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any

SCHEMA_VERSION = "1.0"
MAX_TERM_EXAMPLES = 3
LOCALIZATION_ROOT = Path("Mods") / "QudJP" / "Localization"
CONVERSATION_FILES = ("Conversations.jp.xml", "HiddenConversations.jp.xml")
QUEST_FILE = "Quests.jp.xml"
RUNTIME_TERM_PATTERN = re.compile(r"=([A-Za-z0-9_.:\*\-]+)=")
QUEST_TEXT_ATTRIBUTES = {
    "Accomplishment",
    "Gospel",
    "Hagiograph",
    "MessageComplete",
    "MessageLeaving",
}
QUEST_STRUCTURAL_ATTRIBUTES = {
    "Achievement",
    "Factions",
    "HagiographCategory",
    "Level",
    "Name",
    "Reputation",
}


def build_inventory(repo_root: Path) -> dict[str, Any]:
    """Build an issue #809 inventory from tracked localization XML assets."""
    conversation_files = [
        _build_conversation_file_inventory(repo_root, LOCALIZATION_ROOT / file_name)
        for file_name in CONVERSATION_FILES
    ]
    quest_file = _build_quest_file_inventory(repo_root, LOCALIZATION_ROOT / QUEST_FILE)

    return {
        "schema_version": SCHEMA_VERSION,
        "issue": 809,
        "description": (
            "Tracked authored conversation and quest text inventory for issue #809. "
            "Quest titles are cataloged only to document their explicit exclusion from translation targets."
        ),
        "conversation_files": conversation_files,
        "quest_file": quest_file,
        "totals": {
            "conversation_files": len(conversation_files),
            "conversation_texts": sum(entry["counts"]["texts"] for entry in conversation_files),
            "conversation_runtime_expansion_terms": sum(
                len(entry["runtime_expansion_terms"]) for entry in conversation_files
            ),
            "conversation_choice_parts": sum(entry["counts"]["choice_parts"] for entry in conversation_files),
            "quests": quest_file["counts"]["quests"],
            "quest_steps": quest_file["counts"]["quest_steps"],
            "quest_step_texts": quest_file["counts"]["quest_step_texts"],
        },
    }


def _build_conversation_file_inventory(repo_root: Path, relative_path: Path) -> dict[str, Any]:
    root = _load_xml(repo_root / relative_path)
    text_values = [_element_text(text) for text in root.findall(".//text")]
    runtime_terms = _collect_runtime_terms(text_values, "conversation_text")
    choice_parts = _collect_choice_parts(root)

    return {
        "path": _posix(relative_path),
        "surface": "conversation_authored_xml",
        "counts": {
            "conversations": len(root.findall(".//conversation")),
            "starts": len(root.findall(".//start")),
            "nodes": len(root.findall(".//node")),
            "choices": len(root.findall(".//choice")),
            "texts": len(text_values),
            "texts_with_runtime_expansions": sum(1 for value in text_values if RUNTIME_TERM_PATTERN.search(value)),
            "choice_parts": sum(entry["count"] for entry in choice_parts),
        },
        "runtime_expansion_terms": runtime_terms,
        "choice_parts": choice_parts,
    }


def _build_quest_file_inventory(repo_root: Path, relative_path: Path) -> dict[str, Any]:
    root = _load_xml(repo_root / relative_path)
    quests = root.findall(".//quest")
    steps = root.findall(".//step")
    metadata_texts = _collect_quest_metadata_texts(quests)
    handler_parts = _collect_named_parts(root)

    return {
        "path": _posix(relative_path),
        "surface": "quest_authored_xml",
        "quest_titles_translation_policy": "excluded",
        "counts": {
            "quests": len(quests),
            "quest_titles_excluded": sum(1 for quest in quests if _has_value(quest.get("Name"))),
            "quest_metadata_texts": len(metadata_texts),
            "quest_steps": len(steps),
            "quest_step_names": sum(1 for step in steps if _has_value(step.get("Name"))),
            "quest_step_texts": len([text for step in steps for text in step.findall(".//text")]),
            "quest_handler_parts": sum(entry["count"] for entry in handler_parts),
        },
        "quest_metadata_text_attributes": _counted_attribute_entries(metadata_texts, "quest_metadata_text"),
        "quest_handler_parts": handler_parts,
        "known_structural_attributes": sorted(QUEST_STRUCTURAL_ATTRIBUTES),
    }


def _collect_runtime_terms(text_values: list[str], surface: str) -> list[dict[str, Any]]:
    examples: dict[str, list[str]] = defaultdict(list)
    for value in text_values:
        for match in RUNTIME_TERM_PATTERN.finditer(value):
            term = match.group(1)
            if len(examples[term]) < MAX_TERM_EXAMPLES:
                examples[term].append(value)

    return [
        {"term": term, "surface": surface, "examples": examples[term]}
        for term in sorted(examples)
    ]


def _collect_choice_parts(root: ET.Element) -> list[dict[str, Any]]:
    counter: Counter[str] = Counter()
    for choice in root.findall(".//choice"):
        for part in choice.findall(".//part"):
            name = part.get("Name")
            if name is not None and name.strip():
                counter[name] += 1

    return _counted_name_entries(counter, "conversation_choice_part")


def _collect_named_parts(root: ET.Element) -> list[dict[str, Any]]:
    counter: Counter[str] = Counter()
    for part in root.findall(".//part"):
        name = part.get("Name")
        if name is not None and name.strip():
            counter[name] += 1

    return _counted_name_entries(counter, "quest_handler_part")


def _collect_quest_metadata_texts(quests: list[ET.Element]) -> Counter[str]:
    counter: Counter[str] = Counter()
    for quest in quests:
        for attribute in QUEST_TEXT_ATTRIBUTES:
            if _has_value(quest.get(attribute)):
                counter[attribute] += 1

    return counter


def _counted_name_entries(counter: Counter[str], surface: str) -> list[dict[str, Any]]:
    return [
        {"name": name, "surface": surface, "count": counter[name]}
        for name in sorted(counter)
    ]


def _counted_attribute_entries(counter: Counter[str], surface: str) -> list[dict[str, Any]]:
    return [
        {"attribute": attribute, "surface": surface, "count": counter[attribute]}
        for attribute in sorted(counter)
    ]


def _load_xml(path: Path) -> ET.Element:
    return ET.parse(path).getroot()  # noqa: S314 - input is repo-tracked localization XML.


def _element_text(element: ET.Element) -> str:
    return "".join(element.itertext()).strip()


def _has_value(value: str | None) -> bool:
    return value is not None and value.strip() != ""


def _posix(path: Path) -> str:
    return path.as_posix()


def _parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--repo-root",
        type=Path,
        default=Path.cwd(),
        help="Repository root containing Mods/QudJP/Localization.",
    )
    parser.add_argument(
        "--output",
        type=Path,
        default=Path("docs") / "issue-809-authored-text-inventory.json",
        help="Path to write the inventory JSON.",
    )
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    """Write the issue #809 authored-text inventory."""
    args = _parse_args(argv)
    repo_root = args.repo_root.resolve()
    output_path = args.output if args.output.is_absolute() else repo_root / args.output
    inventory = build_inventory(repo_root)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(json.dumps(inventory, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
