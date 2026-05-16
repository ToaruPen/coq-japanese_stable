from __future__ import annotations

import json
from typing import TYPE_CHECKING

from scripts.audit_function_word_residue import audit_source_tree, audit_test_tree, main

if TYPE_CHECKING:
    from pathlib import Path


def test_source_audit_flags_owner_route_function_word_risks(tmp_path: Path) -> None:
    """Source audit flags owner-route shapes that can leak English function words."""
    source_root = tmp_path / "decompiled"
    source_file = source_root / "XRL.World.Effects" / "Demo.cs"
    source_file.parent.mkdir(parents=True)
    source_file.write_text(
        """IComponent<GameObject>.AddPlayerMessage(Object.Does("were") + " cracked.", 'R');
Popup.Show("You discover " + item.an() + "!");
text.SetText(Grammar.MakePossessive(GO.DisplayName) + " Skills");
IComponent<GameObject>.AddPlayerMessage("You fly to the east.");
E.AddTag("[swimming]");
""",
        encoding="utf-8",
    )

    entries = audit_source_tree(source_root)
    categories = {entry["category"] for entry in entries}
    classifications = {entry["classification"] for entry in entries}

    assert "does_message_frame_composition" in categories
    assert "generated_article_call" in categories
    assert "grammar_make_possessive" in categories
    assert "direction_phrase" in categories
    assert "bracketed_state_suffix" in categories
    assert "owner_route_candidate" in classifications
    assert "generated_display_name_candidate" in classifications
    assert "display_name_state_candidate" in classifications
    assert {entry["path"] for entry in entries} == {"XRL.World.Effects/Demo.cs"}


def test_source_audit_reads_verbatim_and_raw_csharp_string_literals(tmp_path: Path) -> None:
    """Source audit sees function words in nonstandard C# string literal forms."""
    source_root = tmp_path / "decompiled"
    source_file = source_root / "XRL.World.Parts" / "Demo.cs"
    source_file.parent.mkdir(parents=True)
    source_file.write_text(
        '''Popup.Show(@"You pass by a ""web"".");
Popup.Show("""You pass by the web.""");
''',
        encoding="utf-8",
    )

    entries = audit_source_tree(source_root)

    assert [entry["category"] for entry in entries] == [
        "visible_string_function_word",
        "visible_string_function_word",
    ]


def test_test_audit_flags_localized_expectations_with_residue(tmp_path: Path) -> None:
    """Test audit flags localized expectations that still preserve English residue."""
    tests_root = tmp_path / "QudJP.Tests"
    test_file = tests_root / "L1" / "DemoTests.cs"
    test_file.parent.mkdir(parents=True)
    test_file.write_text(
        """[TestCase("The 熊 hits.", "熊はthe bearを殴った")]
Assert.That(translated, Is.EqualTo("{{B|濡れた}}グロウフィッシュ [swimming]"));
Assert.That(translated, Is.EqualTo("熊はto the eastへ飛んだ"));
Assert.That(translated, Is.EqualTo("熊は東側へ飛んだ"));
""",
        encoding="utf-8",
    )

    entries = audit_test_tree(tests_root)
    categories = [entry["category"] for entry in entries]
    classifications = [entry["classification"] for entry in entries]

    assert "test_expectation_function_word" in categories
    assert "test_expectation_bracketed_state" in categories
    assert "test_expectation_direction" in categories
    assert "stale_test_particle_boundary_candidate" in classifications
    assert "stale_test_display_state_candidate" in classifications
    assert "stale_test_owner_route_candidate" in classifications
    assert all(entry["path"] == "L1/DemoTests.cs" for entry in entries)
    assert all("東側へ飛んだ" not in entry["excerpt"] for entry in entries)


def test_test_audit_classifies_intentional_english_expectations(tmp_path: Path) -> None:
    """Test audit separates intentional UI tokens and proper nouns from fix candidates."""
    tests_root = tmp_path / "QudJP.Tests"
    test_file = tests_root / "L2" / "DemoTests.cs"
    test_file.parent.mkdir(parents=True)
    test_file.write_text(
        """Assert.That(translated, Is.EqualTo("[{{W|A}}] 上昇"));
Assert.That(translated, Is.EqualTo("クエスト「{{W|What's Eating the Watervine?}}」を完了した!"));
Assert.That(translated, Is.EqualTo("Mehmetの代名詞はhe/him/his。"));
""",
        encoding="utf-8",
    )

    entries = audit_test_tree(tests_root)

    assert entries
    assert {entry["classification"] for entry in entries} == {"intentional_english_allow"}
    assert {entry["risk"] for entry in entries} == {"intentional"}


def test_cli_writes_json_summary(tmp_path: Path) -> None:
    """CLI writes a JSON summary for machine-readable audit results."""
    tests_root = tmp_path / "tests"
    tests_root.mkdir()
    (tests_root / "DemoTests.cs").write_text(
        'Assert.That(translated, Is.EqualTo("熊はyour armorを見た"));\n',
        encoding="utf-8",
    )
    output = tmp_path / "audit.json"

    exit_code = main(["--tests-root", str(tests_root), "--output", str(output), "--format", "json"])

    payload = json.loads(output.read_text(encoding="utf-8"))
    assert exit_code == 0
    assert payload["summary"]["total"] == 1
    assert payload["summary"]["by_classification"] == {"stale_test_particle_boundary_candidate": 1}
    assert payload["summary"]["by_domain"] == {"test": 1}
    assert payload["summary"]["by_risk"] == {"high": 1}
