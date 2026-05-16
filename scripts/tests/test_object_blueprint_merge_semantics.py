"""Regression tests for object blueprint localization merge semantics."""

from __future__ import annotations

import xml.etree.ElementTree as ET
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
DATA_BLUEPRINTS = REPO_ROOT / "Mods/QudJP/Localization/ObjectBlueprints/Data.jp.xml"


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
