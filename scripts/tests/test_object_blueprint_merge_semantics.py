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


def test_replaced_item_blueprints_do_not_erase_known_player_visible_text() -> None:
    """Known Replace overlays must not blank names, descriptions, or food messages."""
    root = ET.parse(ITEM_BLUEPRINTS).getroot()  # noqa: S314 -- local repository XML
    objects = {obj.get("Name"): obj for obj in root.findall("object")}
    required_fields = {
        "SalthopperMandible": (("Render", "DisplayName"), ("Description", "Short")),
        "TarBones": (("Render", "DisplayName"), ("Description", "Short"), ("Food", "Message")),
        "Bones": (("Render", "DisplayName"), ("Description", "Short")),
        "ExileCorpse": (("Render", "DisplayName"), ("Description", "Short"), ("Food", "Message")),
        "JoppaCorpse": (("Render", "DisplayName"), ("Description", "Short")),
        "CaverCorpse": (("Render", "DisplayName"), ("Food", "Message")),
        "FactionDeed": (("Render", "DisplayName"), ("Description", "Short")),
        "LuminousInfection": (("Render", "DisplayName"), ("Description", "Short")),
        "PuffInfection": (("Render", "DisplayName"), ("Description", "Short")),
        "MumblesInfection": (("Render", "DisplayName"), ("Description", "Short")),
        "WaxInfection": (("Render", "DisplayName"), ("Description", "Short")),
        "PaxInfection": (("Render", "DisplayName"), ("Description", "Short")),
        "Red Security Card": (("Render", "DisplayName"),),
        "Green Security Card": (("Render", "DisplayName"),),
        "Blue Security Card": (("Render", "DisplayName"),),
        "Purple Security Card": (("Render", "DisplayName"),),
        "Copper Trollking Key": (("Render", "DisplayName"), ("Description", "Short")),
        "Silver Trollking Key": (("Render", "DisplayName"), ("Description", "Short")),
        "Gold Trollking Key": (("Description", "Short"),),
        "BarathrumKey": (("Render", "DisplayName"), ("Description", "Short")),
        "GritGateGridKey": (("Render", "DisplayName"), ("Description", "Short")),
        "CrystalKey": (("Render", "DisplayName"), ("Description", "Short")),
    }

    blank_fields: list[str] = []
    for object_name, fields in required_fields.items():
        blueprint = objects[object_name]
        parts = {part.get("Name"): part for part in blueprint.findall("part")}
        for part_name, attribute_name in fields:
            value = parts[part_name].get(attribute_name, "")
            if not value.strip():
                blank_fields.append(f"{object_name}/{part_name}/@{attribute_name}")

    assert blank_fields == []


def test_security_card_and_trollking_key_names_preserve_function_and_material() -> None:
    """Card access classes and key materials must remain visible in Japanese."""
    root = ET.parse(ITEM_BLUEPRINTS).getroot()  # noqa: S314 -- local repository XML
    expected_names = {
        "Red Security Card": "{{r|労働者用セキュリティカード}}",
        "Yellow Security Card": "{{W|保守用セキュリティカード}}",
        "Green Security Card": "{{G|緊急サービス用セキュリティカード}}",
        "Blue Security Card": "{{B|法執行機関用セキュリティカード}}",
        "Purple Security Card": "{{M|軍用セキュリティカード}}",
        "Copper Trollking Key": "{{w|青銅}}の鍵",
        "Silver Trollking Key": "{{silvery|銀}}の鍵",
        "Gold Trollking Key": "{{W|金}}の鍵",
    }
    actual_names = {
        obj.get("Name"): next(
            part.get("DisplayName")
            for part in obj.findall("part")
            if part.get("Name") == "Render"
        )
        for obj in root.findall("object")
        if obj.get("Name") in expected_names
    }

    assert actual_names == expected_names
