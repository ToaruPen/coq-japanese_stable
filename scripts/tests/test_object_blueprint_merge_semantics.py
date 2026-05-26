"""Regression tests for object blueprint localization merge semantics."""

from __future__ import annotations

import xml.etree.ElementTree as ET
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
DATA_BLUEPRINTS = REPO_ROOT / "Mods/QudJP/Localization/ObjectBlueprints/Data.jp.xml"
ITEM_BLUEPRINTS = REPO_ROOT / "Mods/QudJP/Localization/ObjectBlueprints/Items.jp.xml"


def test_procedural_cooking_ingredient_overlays_merge_base_blueprints() -> None:
    """Cooking ingredient descriptions must not replace unit/effect metadata."""
    root = ET.parse(DATA_BLUEPRINTS).getroot()  # noqa: S314 -- local repository XML

    cooking_ingredients = [
        obj
        for obj in root.findall("object")
        if (obj.get("Name") or "").startswith("ProceduralCookingIngredient_")
    ]
    missing_merge = [obj.get("Name") for obj in cooking_ingredients if obj.get("Load") != "Merge"]

    assert cooking_ingredients
    assert missing_merge == []


def test_refract_light_verb_attributes_remain_message_frame_tokens() -> None:
    """RefractLight Verb attributes feed MessageFrame grammar, not visible XML text."""
    root = ET.parse(ITEM_BLUEPRINTS).getroot()  # noqa: S314 -- local repository XML

    verb_by_object = {
        obj.get("Name"): part.get("Verb")
        for obj in root.findall("object")
        for part in obj.findall("part")
        if part.get("Name") == "RefractLight" and part.get("Verb")
    }

    assert verb_by_object.get("Mirrorshades") == "reflect"
