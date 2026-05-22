from __future__ import annotations

import re
import xml.etree.ElementTree as ET
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
SPARKING_BAETYL_REWARDS_XML = REPO_ROOT / "Mods" / "QudJP" / "Localization" / "SparkingBaetyls.jp.xml"

EXPECTED_REWARD_DESCRIPTIONS = {
    "MeleeWeapon": "強大なる武器",
    "Armor": "華麗なる衣装",
    "MissileWeapon": "燃え盛る砲",
    "Artifact": "奇妙な仕掛け",
    "AttributePoints": "能力向上",
    "MutationPoints": "能力向上",
    "SkillPoints": "技能向上",
    "Reputation": "大いなる名声",
}


def test_sparking_baetyl_reward_descriptions_cover_base_rewards() -> None:
    """Issue #762: every stock sparking baetyl reward has a localized description."""
    root = ET.parse(SPARKING_BAETYL_REWARDS_XML).getroot()  # noqa: S314 -- local repository XML
    rewards = root.findall("./rewards/reward")

    descriptions = {reward.attrib["Name"]: reward.attrib.get("Description", "") for reward in rewards}

    assert descriptions == EXPECTED_REWARD_DESCRIPTIONS


def test_sparking_baetyl_reward_descriptions_have_no_english_residue() -> None:
    """Issue #762: baetyl reward descriptions do not preserve English display text."""
    root = ET.parse(SPARKING_BAETYL_REWARDS_XML).getroot()  # noqa: S314 -- local repository XML
    descriptions = {
        reward.attrib["Name"]: reward.attrib.get("Description", "")
        for reward in root.findall("./rewards/reward")
    }

    offenders = [
        f"{name}={description}"
        for name, description in descriptions.items()
        if not description or re.search(r"[A-Za-z]{2,}", description)
    ]

    assert offenders == []
