from __future__ import annotations

import json
import xml.etree.ElementTree as ET
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
LOCALIZATION_ROOT = REPO_ROOT / "Mods" / "QudJP" / "Localization"


def test_authored_quest_runtime_ids_survive_localized_display_names() -> None:
    """Quest and step runtime IDs stay English even when visible names are localized."""
    root = ET.parse(LOCALIZATION_ROOT / "Quests.jp.xml").getroot()  # noqa: S314 -- local repository XML
    quests_by_id = {_runtime_id(quest): quest for quest in root.findall("quest")}

    expected_steps = {
        "The Earl of Omonporch": {"Travel to Omonporch", "Secure the Spindle"},
        "Tomb of the Eaters": {"Recover the Mark of Death", "Inscribe the Mark"},
        "Find Eskhind": {"Find Eskhind", "Speak to Warden Neelahind"},
        "Return to the Hydropon": {"Return to the Hydropon"},
    }

    for quest_id, step_ids in expected_steps.items():
        assert quest_id in quests_by_id
        steps_by_id = {_runtime_id(step): step for step in quests_by_id[quest_id].findall("step")}
        for step_id in step_ids:
            assert step_id in steps_by_id


def test_localized_authored_quest_names_have_explicit_runtime_ids() -> None:
    """Japanese quest/step display names must not replace implicit runtime IDs."""
    localized_names = _localized_quest_dictionary_texts()
    root = ET.parse(LOCALIZATION_ROOT / "Quests.jp.xml").getroot()  # noqa: S314 -- local repository XML

    offenders: list[str] = []
    for element in root.iter():
        if element.tag not in {"quest", "step"}:
            continue

        name = element.get("Name")
        if name in localized_names and element.get("ID") is None:
            offenders.append(f"{element.tag}:{name}")

    assert offenders == []


def _runtime_id(element: ET.Element) -> str | None:
    return element.get("ID") or element.get("Name")


def _localized_quest_dictionary_texts() -> set[str]:
    path = LOCALIZATION_ROOT / "Dictionaries" / "ui-quests.ja.json"
    payload = json.loads(path.read_text(encoding="utf-8"))
    return {
        entry["text"]
        for entry in payload["entries"]
        if isinstance(entry.get("key"), str)
        and isinstance(entry.get("text"), str)
        and entry["key"] != entry["text"]
    }
