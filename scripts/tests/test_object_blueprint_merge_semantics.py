"""Regression tests for object blueprint localization merge semantics."""

from __future__ import annotations

import xml.etree.ElementTree as ET
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
DATA_BLUEPRINTS = REPO_ROOT / "Mods/QudJP/Localization/ObjectBlueprints/Data.jp.xml"
HIDDEN_BLUEPRINTS = (
    REPO_ROOT / "Mods/QudJP/Localization/ObjectBlueprints/HiddenObjects.jp.xml"
)
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


def test_trium_hologram_overlays_merge_conversation_blueprints() -> None:
    """Translated hologram names must preserve base conversations and presentation."""
    root = ET.parse(HIDDEN_BLUEPRINTS).getroot()  # noqa: S314 -- local repository XML
    expected_names = {
        "Barathrum Hologram",
        "Archon Hologram",
        "Rebekah Hologram",
        "Resheph Hologram",
    }
    holograms = [
        obj
        for obj in root.findall("object")
        if obj.get("Name") in expected_names
    ]

    assert len(holograms) == len(expected_names)
    assert {obj.get("Name") for obj in holograms} == expected_names
    for hologram in holograms:
        assert hologram.get("Inherits") == "BaseTriumHologram"
        assert hologram.get("Load") == "Merge"
        assert hologram.get("Replace") is None

        children = list(hologram)
        assert len(children) == 1
        assert children[0].tag == "part"
        assert children[0].get("Name") == "Render"
        assert children[0].get("DisplayName")


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
