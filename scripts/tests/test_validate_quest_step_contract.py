from __future__ import annotations

import xml.etree.ElementTree as ET
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from pathlib import Path

    import pytest

from scripts.validate_quest_step_contract import (
    build_step_gameplay_contract,
    compare_quest_step_contracts,
    main,
)


def _xml(value: str) -> ET.Element:
    return ET.fromstring(value)  # noqa: S314 -- controlled test XML


def _write_valid_quests(path: Path) -> None:
    path.write_text('<quests><quest Name="Q"><step Name="S" /></quest></quests>', encoding="utf-8")


def test_translated_display_fields_do_not_change_stable_runtime_contract() -> None:
    """Translated display fields do not alter explicit runtime identities."""
    base = _xml(
        """
        <quests>
          <quest ID="Quest ID" Name="English quest name">
            <step ID="Step ID" Name="English step name" XP="100">
              <text>English instructions.</text>
            </step>
          </quest>
        </quests>
        """
    )
    localized = _xml(
        """
        <quests>
          <quest ID="Quest ID" Name="日本語のクエスト名">
            <step ID="Step ID" Name="日本語のステップ名" XP="100">
              <text>日本語の説明。</text>
            </step>
          </quest>
        </quests>
        """
    )

    assert compare_quest_step_contracts(base, localized) == []


def test_build_contract_applies_quest_loader_semantic_defaults() -> None:
    """Missing attributes normalize to the defaults used by QuestLoader."""
    root = _xml(
        """
        <quests>
          <quest Name="Quest ID">
            <step Name="First step"><text>Display text</text></step>
            <step Name="Second step" />
          </quest>
        </quests>
        """
    )

    assert build_step_gameplay_contract(root) == {
        "Quest ID": (
            {
                "ID": "First step",
                "Value": None,
                "XP": 0,
                "Optional": False,
                "Ordinal": 0,
                "Collapse": True,
                "Awarded": False,
                "Failed": False,
                "Hidden": False,
                "Base": False,
            },
            {
                "ID": "Second step",
                "Value": None,
                "XP": 0,
                "Optional": False,
                "Ordinal": 1,
                "Collapse": True,
                "Awarded": False,
                "Failed": False,
                "Hidden": False,
                "Base": False,
            },
        )
    }


def test_build_contract_normalizes_every_explicit_quest_loader_semantic() -> None:
    """Every non-display step attribute read by QuestLoader is normalized."""
    root = _xml(
        """
        <quests>
          <quest ID="Quest ID" Name="Visible quest name">
            <step ID="Step ID" Name="Visible step name" Value="marker" XP="250"
                  Optional="TRUE" Ordinal="9" Collapse="false" Awarded="true"
                  Failed="true" Hidden="true" Base="true">
              <text>Visible step text.</text>
            </step>
          </quest>
        </quests>
        """
    )

    assert build_step_gameplay_contract(root) == {
        "Quest ID": (
            {
                "ID": "Step ID",
                "Value": "marker",
                "XP": 250,
                "Optional": True,
                "Ordinal": 9,
                "Collapse": False,
                "Awarded": True,
                "Failed": True,
                "Hidden": True,
                "Base": True,
            },
        )
    }


def test_build_contract_matches_dotnet_int32_and_bool_parse_fallbacks() -> None:
    """Invalid .NET Int32 and Boolean syntax falls back to QuestLoader defaults."""
    root = _xml(
        """
        <quests>
          <quest Name="Quest ID">
            <step Name="Underscore" XP="1_000" Optional="yes" Collapse="yes" />
            <step Name="Malformed" XP="not-an-int" Ordinal="also-invalid" />
            <step Name="Positive overflow" XP="2147483648" Ordinal="-2147483649" />
            <step Name="Negative overflow" XP="-2147483649" />
            <step Name="Boundaries" XP="+2147483647" Ordinal="-2147483648" />
          </quest>
        </quests>
        """
    )

    steps = build_step_gameplay_contract(root)["Quest ID"]

    assert [
        (step["XP"], step["Ordinal"], step["Optional"], step["Collapse"])
        for step in steps
    ] == [
        (0, 0, False, True),
        (0, 1, False, True),
        (0, 2, False, True),
        (0, 3, False, True),
        (2_147_483_647, -2_147_483_648, False, True),
    ]


def test_compare_rejects_duplicate_base_quest_runtime_ids() -> None:
    """Duplicate base quest IDs are rejected before comparison."""
    base = _xml(
        '<quests><quest Name="Duplicate" /><quest ID="Duplicate" Name="Display" /></quests>'
    )

    assert compare_quest_step_contracts(base, _xml("<quests />")) == [
        "base quests: duplicate quest runtime ID 'Duplicate'"
    ]


def test_compare_rejects_duplicate_base_step_runtime_ids() -> None:
    """Duplicate base step IDs are rejected before comparison."""
    base = _xml(
        """
        <quests><quest Name="Q">
          <step Name="Duplicate" /><step ID="Duplicate" Name="Display" />
        </quest></quests>
        """
    )
    localized = _xml('<quests><quest Name="Q"><step Name="Duplicate" /></quest></quests>')

    assert compare_quest_step_contracts(base, localized) == [
        "base quests: quest 'Q': duplicate step runtime ID 'Duplicate'"
    ]


def test_compare_rejects_duplicate_localized_quest_runtime_ids() -> None:
    """Duplicate localized quest IDs are rejected before comparison."""
    localized = _xml(
        '<quests><quest Name="Duplicate" /><quest ID="Duplicate" Name="表示" /></quests>'
    )

    assert compare_quest_step_contracts(_xml("<quests />"), localized) == [
        "localized quests: duplicate quest runtime ID 'Duplicate'"
    ]


def test_compare_rejects_duplicate_localized_step_runtime_ids() -> None:
    """Duplicate localized step IDs are rejected before comparison."""
    base = _xml('<quests><quest Name="Q"><step Name="Duplicate" /></quest></quests>')
    localized = _xml(
        """
        <quests><quest Name="Q">
          <step Name="Duplicate" /><step ID="Duplicate" Name="表示" />
        </quest></quests>
        """
    )

    assert compare_quest_step_contracts(base, localized) == [
        "localized quests: quest 'Q': duplicate step runtime ID 'Duplicate'"
    ]


def test_compare_reports_xp_and_optional_mismatches_in_field_order() -> None:
    """Semantic diagnostics identify XP and Optional drift deterministically."""
    base = _xml(
        """
        <quests>
          <quest Name="A Quest">
            <step Name="A Step" XP="1500" Optional="true" />
          </quest>
        </quests>
        """
    )
    localized = _xml(
        """
        <quests>
          <quest Name="A Quest">
            <step Name="A Step" XP="0" />
          </quest>
        </quests>
        """
    )

    assert compare_quest_step_contracts(base, localized) == [
        "quest 'A Quest', step 'A Step': XP mismatch (base=1500, localized=0)",
        "quest 'A Quest', step 'A Step': Optional mismatch (base=true, localized=false)",
    ]


def test_compare_reports_missing_and_extra_runtime_step_ids() -> None:
    """Step membership drift identifies missing and extra runtime IDs."""
    base = _xml(
        """
        <quests>
          <quest Name="A Quest">
            <step Name="Kept" />
            <step Name="Missing" />
          </quest>
        </quests>
        """
    )
    localized = _xml(
        """
        <quests>
          <quest Name="A Quest">
            <step Name="Kept" />
            <step Name="Extra" />
          </quest>
        </quests>
        """
    )

    assert compare_quest_step_contracts(base, localized) == [
        "quest 'A Quest': missing localized step IDs: ['Missing']",
        "quest 'A Quest': extra localized step IDs: ['Extra']",
    ]


def test_compare_reports_reordered_runtime_step_ids() -> None:
    """Step order drift reports both expected and localized runtime ID order."""
    base = _xml(
        """
        <quests>
          <quest Name="A Quest">
            <step Name="First" />
            <step Name="Second" />
          </quest>
        </quests>
        """
    )
    localized = _xml(
        """
        <quests>
          <quest Name="A Quest">
            <step Name="Second" />
            <step Name="First" />
          </quest>
        </quests>
        """
    )

    assert compare_quest_step_contracts(base, localized) == [
        "quest 'A Quest': reordered localized step IDs "
        "(base=['First', 'Second'], localized=['Second', 'First'])"
    ]


def test_compare_reports_semantic_drift_alongside_reordered_step_ids() -> None:
    """Common steps retain semantic diagnostics without default-Ordinal noise."""
    base = _xml(
        """
        <quests>
          <quest Name="A Quest">
            <step Name="First" />
            <step Name="Second" XP="1500" Optional="true" />
          </quest>
        </quests>
        """
    )
    localized = _xml(
        """
        <quests>
          <quest Name="A Quest">
            <step Name="Second" />
            <step Name="First" />
          </quest>
        </quests>
        """
    )

    assert compare_quest_step_contracts(base, localized) == [
        "quest 'A Quest': reordered localized step IDs "
        "(base=['First', 'Second'], localized=['Second', 'First'])",
        "quest 'A Quest', step 'Second': XP mismatch (base=1500, localized=0)",
        "quest 'A Quest', step 'Second': Optional mismatch (base=true, localized=false)",
    ]


def test_compare_preserves_explicit_ordinal_drift_with_a_missing_step() -> None:
    """An explicit Ordinal remains semantic drift during structural mismatch."""
    base = _xml(
        """
        <quests><quest Name="A Quest">
          <step Name="Missing" />
          <step Name="Kept" Ordinal="1" />
        </quest></quests>
        """
    )
    localized = _xml('<quests><quest Name="A Quest"><step Name="Kept" /></quest></quests>')

    assert compare_quest_step_contracts(base, localized) == [
        "quest 'A Quest': missing localized step IDs: ['Missing']",
        "quest 'A Quest', step 'Kept': Ordinal mismatch (base=1, localized=0)",
    ]


def test_compare_is_localized_owned_and_deterministic() -> None:
    """Only localized-owned quests are checked in stable document order."""
    base = _xml(
        """
        <quests>
          <quest Name="Localized first">
            <step Name="Step" XP="10" />
          </quest>
          <quest Name="Base only">
            <step Name="Ignored" XP="20" />
          </quest>
          <quest Name="Localized second">
            <step Name="Step" XP="30" />
          </quest>
        </quests>
        """
    )
    localized = _xml(
        """
        <quests>
          <quest Name="Localized first">
            <step Name="Step" XP="1" />
          </quest>
          <quest Name="Localized second">
            <step Name="Step" XP="3" />
          </quest>
        </quests>
        """
    )

    expected = [
        "quest 'Localized first', step 'Step': XP mismatch (base=10, localized=1)",
        "quest 'Localized second', step 'Step': XP mismatch (base=30, localized=3)",
    ]
    assert compare_quest_step_contracts(base, localized) == expected
    assert compare_quest_step_contracts(base, localized) == expected


def test_cli_prints_one_actionable_mismatch_per_line_and_exits_nonzero(
    tmp_path: Path,
    capsys: pytest.CaptureFixture[str],
) -> None:
    """The CLI emits one diagnostic per line and fails when drift exists."""
    base_path = tmp_path / "base.xml"
    localized_path = tmp_path / "localized.xml"
    base_path.write_text(
        '<quests><quest Name="Q"><step Name="S" XP="10" Optional="true" /></quest></quests>',
        encoding="utf-8",
    )
    localized_path.write_text(
        '<quests><quest Name="Q"><step Name="S" /></quest></quests>',
        encoding="utf-8",
    )

    exit_code = main([str(base_path), str(localized_path)])

    assert exit_code == 1
    assert capsys.readouterr().out.splitlines() == [
        "quest 'Q', step 'S': XP mismatch (base=10, localized=0)",
        "quest 'Q', step 'S': Optional mismatch (base=true, localized=false)",
    ]


def test_cli_reports_missing_base_input_to_stderr(
    tmp_path: Path,
    capsys: pytest.CaptureFixture[str],
) -> None:
    """A missing base XML returns the input-error code with its role and path."""
    missing_path = tmp_path / "missing-base.xml"
    localized_path = tmp_path / "localized.xml"
    _write_valid_quests(localized_path)

    exit_code = main([str(missing_path), str(localized_path)])

    captured = capsys.readouterr()
    assert exit_code == 2
    assert captured.out == ""
    assert captured.err == (
        f"error: cannot read base quests XML {str(missing_path)!r}: No such file or directory\n"
    )


def test_cli_reports_missing_localized_input_to_stderr(
    tmp_path: Path,
    capsys: pytest.CaptureFixture[str],
) -> None:
    """A missing localized XML returns the input-error code with its role and path."""
    base_path = tmp_path / "base.xml"
    missing_path = tmp_path / "missing-localized.xml"
    _write_valid_quests(base_path)

    exit_code = main([str(base_path), str(missing_path)])

    captured = capsys.readouterr()
    assert exit_code == 2
    assert captured.out == ""
    assert captured.err == (
        f"error: cannot read localized quests XML {str(missing_path)!r}: No such file or directory\n"
    )


def test_cli_reports_malformed_base_input_to_stderr(
    tmp_path: Path,
    capsys: pytest.CaptureFixture[str],
) -> None:
    """Malformed base XML is distinguished from an unreadable input."""
    base_path = tmp_path / "base.xml"
    localized_path = tmp_path / "localized.xml"
    base_path.write_text("<quests><quest></quests>", encoding="utf-8")
    _write_valid_quests(localized_path)

    exit_code = main([str(base_path), str(localized_path)])

    captured = capsys.readouterr()
    assert exit_code == 2
    assert captured.out == ""
    assert captured.err.startswith(f"error: malformed base quests XML {str(base_path)!r}: mismatched tag:")


def test_cli_reports_malformed_localized_input_to_stderr(
    tmp_path: Path,
    capsys: pytest.CaptureFixture[str],
) -> None:
    """Malformed localized XML is distinguished from an unreadable input."""
    base_path = tmp_path / "base.xml"
    localized_path = tmp_path / "localized.xml"
    _write_valid_quests(base_path)
    localized_path.write_text("<quests><quest></quests>", encoding="utf-8")

    exit_code = main([str(base_path), str(localized_path)])

    captured = capsys.readouterr()
    assert exit_code == 2
    assert captured.out == ""
    assert captured.err.startswith(
        f"error: malformed localized quests XML {str(localized_path)!r}: mismatched tag:"
    )
