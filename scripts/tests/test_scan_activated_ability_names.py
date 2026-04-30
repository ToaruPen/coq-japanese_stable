from __future__ import annotations

import json
from pathlib import Path
from typing import Protocol

from scripts.scan_activated_ability_names import main

FIXTURE_ROOT = Path(__file__).parent / "fixtures" / "activated_abilities"


class _CapturedOutput(Protocol):
    out: str
    err: str


class _Capsys(Protocol):
    def readouterr(self) -> _CapturedOutput: ...


def test_cli_writes_deterministic_inventory_for_fixture(tmp_path: Path) -> None:
    """The CLI emits stable schema fields and classifications from fixture sources."""
    output_path = tmp_path / "ability_inventory.json"

    result = main(["--source-root", str(FIXTURE_ROOT), "--output", str(output_path)])

    assert result == 0
    payload = json.loads(output_path.read_text(encoding="utf-8"))
    assert payload["schema_version"] == "1.0"
    assert payload["source_root"] == str(FIXTURE_ROOT.resolve())

    items = payload["items"]
    assert [item["file"] for item in items] == ["Demo/ActivatedAbilityCases.cs"] * 24
    assert [(item["line"], item["method"], item["classification"]) for item in items] == [
        (12, "AddActivatedAbility", "static_leaf"),
        (13, "AddMyActivatedAbility", "static_leaf"),
        (14, "AddActivatedAbility", "display_name_source"),
        (15, "AddMyActivatedAbility", "display_name_source"),
        (16, "AddActivatedAbility", "dynamic_composition"),
        (17, "AddActivatedAbility", "dynamic_composition"),
        (18, "AddMyActivatedAbility", "dynamic_composition"),
        (19, "SetActivatedAbilityDisplayName", "dynamic_composition"),
        (20, "SetActivatedAbilityDisplayName", "display_name_source"),
        (21, "AddActivatedAbility", "static_leaf"),
        (22, "AddActivatedAbility", "dynamic_composition"),
        (23, "AddActivatedAbility", "dynamic_composition"),
        (24, "AddActivatedAbility", "static_leaf"),
        (25, "AddMyActivatedAbility", "dynamic_composition"),
        (26, "AddActivatedAbility", "static_leaf"),
        (28, "AddDynamicCommand", "static_leaf"),
        (29, "AddDynamicCommand", "static_leaf"),
        (30, "AddDynamicCommand", "static_leaf"),
        (31, "SetMyActivatedAbilityDisplayName", "static_leaf"),
        (32, "SetMyActivatedAbilityDisplayName", "display_name_source"),
        (33, "AddAbility", "static_leaf"),
        (34, "AddAbility", "static_leaf"),
        (35, "DisplayNameAssignment", "static_leaf"),
        (36, "DisplayNameAssignment", "dynamic_composition"),
    ]

    assert items[0]["name"] == "Static Punch"
    assert items[1]["name"] == 'Static "Blink"'
    assert items[2]["expression"] == "parent.GetDisplayName()"
    assert items[3]["expression"] == "(GetDisplayName())"
    assert items[4]["expression"] == '"Phase " + mutationName'
    assert items[5]["expression"] == '$"{parent.GetDisplayName()} Beam"'
    assert items[6]["expression"] == "abilityName"
    assert items[7]["expression"] == "BuildAbilityName(parent)"
    assert items[8]["expression"] == "parent.GetDisplayName()"
    assert items[9]["name"] == "Named Static"
    assert items[10]["expression"] == "GetDisplayName() + Other()"
    assert items[11]["expression"] == "GetDisplayName().Strip()"
    assert items[12]["name"] == "Named Name Static"
    assert items[13]["expression"] == "BuildAbilityName(parent)"
    assert items[14]["name"] == "Lambda Static"
    assert items[15]["name"] == "Recomposite"
    assert items[16]["name"] == "Named Dynamic Command"
    assert items[17]["name"] == "Mixed Positional Name"
    assert items[18]["name"] == "Set My Static"
    assert items[19]["expression"] == "parent.GetDisplayName()"
    assert items[20]["name"] == "Eject"
    assert items[21]["name"] == "Direct Named AddAbility"
    assert items[22]["name"] == "Assignment Static"
    assert items[23]["expression"] == '"Recoil to " + zoneName'


def test_missing_source_root_exits_1_with_explicit_stderr(tmp_path: Path, capsys: _Capsys) -> None:
    """A missing source root returns exit 1 without creating an output file."""
    output_path = tmp_path / "ability_inventory.json"
    missing_root = tmp_path / "missing-source"

    result = main(["--source-root", str(missing_root), "--output", str(output_path)])
    captured = capsys.readouterr()

    assert result == 1
    assert "source root does not exist or is not a directory" in captured.err
    assert str(missing_root) in captured.err
    assert captured.out == ""
    assert not output_path.exists()
