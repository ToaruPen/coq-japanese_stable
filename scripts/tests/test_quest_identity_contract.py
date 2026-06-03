from __future__ import annotations

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
    root = ET.parse(LOCALIZATION_ROOT / "Quests.jp.xml").getroot()  # noqa: S314 -- local repository XML

    offenders: list[str] = []
    for element in root.iter():
        if element.tag not in {"quest", "step"}:
            continue

        name = element.get("Name")
        if _looks_localized_name(name) and element.get("ID") is None:
            offenders.append(f"{element.tag}:{name}")

    assert offenders == []


def _runtime_id(element: ET.Element) -> str | None:
    return element.get("ID") or element.get("Name")


def _looks_localized_name(name: str | None) -> bool:
    return bool(name) and any(ord(character) > 127 for character in name)
