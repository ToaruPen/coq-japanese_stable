from __future__ import annotations

import hashlib
import json
import xml.etree.ElementTree as ET
from pathlib import Path

from scripts.validate_quest_step_contract import (
    QuestStepGameplayContract,
    build_step_gameplay_contract,
)

REPO_ROOT = Path(__file__).resolve().parents[2]
LOCALIZATION_ROOT = REPO_ROOT / "Mods" / "QudJP" / "Localization"

# Caves of Qud 1.0.5 Base/Quests.xml, filtered to the quests owned by Quests.jp.xml.
COQ_1_0_5_STEP_GAMEPLAY_CONTRACT_SHA256 = (
    "3c37fe6f016cf239d2ae8610898971740c4a60560e4b82f83f1d66aa1d40b845"
)


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


def test_localized_step_gameplay_contract_matches_caves_of_qud_1_0_5() -> None:
    """Localized steps preserve the shipped 1.0.5 gameplay contract."""
    root = ET.parse(LOCALIZATION_ROOT / "Quests.jp.xml").getroot()  # noqa: S314 -- local repository XML
    contract = build_step_gameplay_contract(root)

    assert _step_gameplay_contract_sha256(contract) == COQ_1_0_5_STEP_GAMEPLAY_CONTRACT_SHA256


def test_step_gameplay_contract_digest_ignores_quest_mapping_order() -> None:
    """Equivalent quest mappings produce the same compact contract digest."""
    contract: QuestStepGameplayContract = {
        "Quest A": ({"ID": "Step A"},),
        "Quest B": ({"ID": "Step B"},),
    }
    reordered_contract = dict(reversed(contract.items()))

    assert _step_gameplay_contract_sha256(contract) == _step_gameplay_contract_sha256(
        reordered_contract
    )


def test_six_day_stilt_pilgrimage_awards_1500_xp() -> None:
    """The Six Day Stilt pilgrimage keeps its shipped quest-step XP reward."""
    root = ET.parse(LOCALIZATION_ROOT / "Quests.jp.xml").getroot()  # noqa: S314 -- local repository XML
    contract = build_step_gameplay_contract(root)
    quest_steps = contract["O Glorious Shekhinah!"]
    pilgrimage = next(
        step for step in quest_steps if step["ID"] == "Make a Pilgrimage to the Six Day Stilt"
    )

    assert pilgrimage["XP"] == 1500


def _runtime_id(element: ET.Element) -> str | None:
    return element.get("ID") or element.get("Name")


def _step_gameplay_contract_sha256(contract: QuestStepGameplayContract) -> str:
    payload = json.dumps(
        contract,
        ensure_ascii=False,
        separators=(",", ":"),
        sort_keys=True,
    ).encode()
    return hashlib.sha256(payload).hexdigest()


def _looks_localized_name(name: str | None) -> bool:
    return bool(name) and any(ord(character) > 127 for character in name)
