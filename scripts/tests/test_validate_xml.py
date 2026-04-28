import xml.etree.ElementTree as ET
from pathlib import Path

import pytest

from scripts.validate_xml import main, validate_xml_file


def _write_xml(path: Path, content: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content, encoding="utf-8")


def test_valid_xml_file_passes(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    """A well-formed XML file exits successfully."""
    xml_path = tmp_path / "valid.xml"
    _write_xml(xml_path, "<root><text>OK</text></root>")

    result = main([str(xml_path)])
    captured = capsys.readouterr()

    assert result == 0
    assert "OK" in captured.out


def test_invalid_xml_reports_error(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    """Malformed XML reports an error and exits non-zero."""
    xml_path = tmp_path / "broken.xml"
    _write_xml(xml_path, "<root><text>broken</root>")

    result = main([str(xml_path)])
    captured = capsys.readouterr()

    assert result == 1
    assert "ERROR" in captured.out
    assert "XML parse failed" in captured.out


def test_unbalanced_color_code_reports_warning(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    """Unbalanced ``{{`` and ``}}`` markup is reported as warning."""
    xml_path = tmp_path / "colors.xml"
    _write_xml(xml_path, "<root><text>{{W|missing close</text></root>")

    result = main([str(xml_path)])
    captured = capsys.readouterr()

    assert result == 0
    assert "warning" in captured.out
    assert "Unbalanced color code" in captured.out


def test_generic_duplicate_id_no_longer_reported(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    """Duplicate sibling IDs under arbitrary parents are not flagged.

    Only schema-allowlisted (parent_tag, child_tag, key_attribute) triples
    in DUPLICATE_DETECTION_RULES trigger duplicate detection.
    """
    xml_path = tmp_path / "duplicate_id.xml"
    _write_xml(xml_path, '<root><item ID="A"/><item ID="A"/></root>')

    result = main([str(xml_path)])
    captured = capsys.readouterr()

    assert result == 0
    assert "Duplicate sibling" not in captured.out


def test_empty_text_element_reports_warning(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    """Whitespace-only text elements are reported as warning."""
    xml_path = tmp_path / "empty_text.xml"
    _write_xml(xml_path, "<root><text>   </text></root>")

    result = main([str(xml_path)])
    captured = capsys.readouterr()

    assert result == 0
    assert "Empty text in element 'text'" in captured.out


def test_strict_mode_treats_warning_as_error(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    """Strict mode returns non-zero when warnings are present."""
    xml_path = tmp_path / "strict.xml"
    _write_xml(xml_path, "<root><text>{{G|strict mode</text></root>")

    result = main(["--strict", str(xml_path)])
    captured = capsys.readouterr()

    assert result == 1
    assert "WARNING" in captured.out


def test_strict_mode_allows_baselined_warning(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    """Strict mode ignores warnings that are present in a warning baseline."""
    xml_path = tmp_path / "strict.xml"
    baseline_path = tmp_path / "baseline.json"
    _write_xml(xml_path, "<root><text>{{G|strict mode</text></root>")

    write_result = main([str(xml_path), "--write-warning-baseline", str(baseline_path)])
    _ = capsys.readouterr()
    strict_result = main(["--strict", "--warning-baseline", str(baseline_path), str(xml_path)])
    captured = capsys.readouterr()

    assert write_result == 0
    assert strict_result == 0
    assert "NEW WARNING" not in captured.err


def test_strict_mode_fails_on_warning_not_in_baseline(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    """Strict mode still fails when a warning is not present in the baseline."""
    baseline_xml = tmp_path / "baseline.xml"
    changed_xml = tmp_path / "changed.xml"
    baseline_path = tmp_path / "baseline.json"
    _write_xml(baseline_xml, "<root><text>{{G|baseline</text></root>")
    _write_xml(changed_xml, "<root><text></text></root>")

    assert main([str(baseline_xml), "--write-warning-baseline", str(baseline_path)]) == 0
    _ = capsys.readouterr()
    result = main(["--strict", "--warning-baseline", str(baseline_path), str(changed_xml)])
    captured = capsys.readouterr()

    assert result == 1
    assert "NEW WARNING" in captured.err


def test_directory_scanning_finds_nested_xml_files(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    """Directory input recursively scans nested XML files."""
    root_dir = tmp_path / "Localization"
    _write_xml(root_dir / "Top.jp.xml", "<root><text>top</text></root>")
    _write_xml(root_dir / "nested" / "Deep.xml", "<root><text>deep</text></root>")

    result = main([str(root_dir)])
    captured = capsys.readouterr()

    assert result == 0
    assert "Top.jp.xml" in captured.out
    assert "Deep.xml" in captured.out


def test_file_with_no_issues_reports_ok(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    """Issue-free files are reported with an OK status line."""
    xml_path = tmp_path / "clean.xml"
    _write_xml(xml_path, "<root><text>all good</text></root>")

    result = main([str(xml_path)])
    captured = capsys.readouterr()

    assert result == 0
    assert f"Checking {xml_path}... OK" in captured.out


def test_invalid_utf8_reports_error_in_color_scan(tmp_path: Path) -> None:
    """Invalid UTF-8 bytes are reported as validation error, not silently ignored."""
    xml_path = tmp_path / "bad_encoding.xml"
    xml_path.write_bytes(b'<?xml version="1.0" encoding="ISO-8859-1"?>\n<root><text>\xe9</text></root>')

    result = validate_xml_file(xml_path)

    assert len(result.errors) >= 1
    assert any("not valid UTF-8" in e for e in result.errors)


def test_duplicate_siblings_with_distinguishing_attribute_not_flagged(
    tmp_path: Path, capsys: pytest.CaptureFixture[str]
) -> None:
    """Same Name on siblings under non-allowlisted parents is not flagged.

    Worlds.jp.xml uses ``<zone Name="..." Level="..." x="..." y="..."/>`` where
    the Name repeats but the (Level, x, y) tuple differentiates entries. The
    new schema-aware validator must not flag these.
    """
    xml_path = tmp_path / "worlds.xml"
    _write_xml(
        xml_path,
        (
            '<worlds><world Name="JoppaWorld">'
            '<zone Name="Lair" Level="10" x="0" y="0"/>'
            '<zone Name="Lair" Level="11" x="0" y="0"/>'
            "</world></worlds>"
        ),
    )

    result = main([str(xml_path)])
    captured = capsys.readouterr()

    assert result == 0
    assert "Duplicate sibling" not in captured.out


def test_byte_equal_object_siblings_flagged(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    """``<objects>`` parent with same-Name ``<object>`` siblings is flagged.

    This is the regression case for the TombExteriorWall_SW byte-equal
    duplicate that prompted the validator overhaul. Real ObjectBlueprints
    files use root tag ``<objects>`` so ``parent.tag == "objects"`` matches.
    """
    xml_path = tmp_path / "blueprints.xml"
    _write_xml(
        xml_path,
        (
            "<objects>"
            '<object Name="TombExteriorWall_SW" Inherits="Widget" Replace="true"/>'
            '<object Name="TombExteriorWall_SW" Inherits="Widget" Replace="true"/>'
            "</objects>"
        ),
    )

    result = main([str(xml_path)])
    captured = capsys.readouterr()

    assert result == 0
    assert 'Duplicate sibling Name="TombExteriorWall_SW"' in captured.out


def test_duplicate_conditional_nodes_not_flagged(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    """Conditional ``<node>`` siblings sharing an ID are not flagged.

    Conversations.jp.xml uses the same ID with different ``IfHaveState``
    branches to express conditional dialogue paths. These must remain silent.
    """
    xml_path = tmp_path / "conversations.xml"
    _write_xml(
        xml_path,
        (
            '<conversations><conversation ID="X">'
            '<node ID="Greet" IfHaveState="A"/>'
            '<node ID="Greet" IfHaveState="B"/>'
            "</conversation></conversations>"
        ),
    )

    result = main([str(xml_path)])
    captured = capsys.readouterr()

    assert result == 0
    assert "Duplicate sibling" not in captured.out


def test_repeated_naming_entries_not_flagged(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    """Naming.jp.xml weight-style repetition is not flagged.

    ``<prefix Name="ニ"/>`` may legitimately appear multiple times to
    weight that candidate higher in random selection.
    """
    xml_path = tmp_path / "naming.xml"
    _write_xml(
        xml_path,
        ('<naming><prefixes><prefix Name="ニ"/><prefix Name="ニ"/></prefixes></naming>'),
    )

    result = main([str(xml_path)])
    captured = capsys.readouterr()

    assert result == 0
    assert "Duplicate sibling" not in captured.out


def test_empty_text_only_flagged_for_text_tag(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    r"""Whitespace-only body on non-``<text>`` tags is not flagged.

    Inheritance/stub objects like ``<object Inherits="X" Replace="true">\n  </object>``
    legitimately have whitespace-only bodies. Only ``<text>`` should be
    checked for emptiness.
    """
    xml_path = tmp_path / "stub.xml"
    _write_xml(
        xml_path,
        '<root><object Name="X" Inherits="Widget" Replace="true">\n  </object></root>',
    )

    result = main([str(xml_path)])
    captured = capsys.readouterr()

    assert result == 0
    assert "Empty text" not in captured.out


def test_empty_text_self_closing_flagged(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    """Self-closing ``<text/>`` is flagged as empty."""
    xml_path = tmp_path / "selfclose.xml"
    _write_xml(xml_path, "<root><text/></root>")

    result = main([str(xml_path)])
    captured = capsys.readouterr()

    assert result == 0
    assert "Empty text in element 'text'" in captured.out


def test_empty_object_stub_not_flagged(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    """Empty inheritance-only ``<object>`` is not flagged."""
    xml_path = tmp_path / "objstub.xml"
    _write_xml(
        xml_path,
        '<root><object Inherits="X" Replace="true"></object></root>',
    )

    result = main([str(xml_path)])
    captured = capsys.readouterr()

    assert result == 0
    assert "Empty text" not in captured.out


def test_source_root_reports_markup_drift_in_element_text_and_attributes(
    tmp_path: Path, capsys: pytest.CaptureFixture[str]
) -> None:
    """``--source-root`` compares source and localized XML markup tokens."""
    localization = tmp_path / "Localization"
    source_root = tmp_path / "Base"
    localized_xml = localization / "Conversations.jp.xml"
    source_xml = source_root / "Conversations.xml"
    _write_xml(
        source_xml,
        (
            '<conversations><node ID="Greet" Name="{{Y|Joppa}}">'
            "<text>{{R|Alert}} &amp;W =player.name=</text></node></conversations>"
        ),
    )
    _write_xml(
        localized_xml,
        '<conversations><node ID="Greet" Name="Joppa"><text>{{R|警告}} =player.name=</text></node></conversations>',
    )

    result = main(["--source-root", str(source_root), str(localized_xml)])
    captured = capsys.readouterr()

    assert result == 0
    assert "Markup token drift" in captured.out
    assert "@Name" in captured.out
    assert "text()" in captured.out
    assert "'&W': 1" in captured.out
    assert "'{{Y|': 1" in captured.out


def test_source_root_maps_nested_jp_xml_to_base_xml(
    tmp_path: Path, capsys: pytest.CaptureFixture[str]
) -> None:
    """Nested ``ObjectBlueprints/Foo.jp.xml`` maps to ``ObjectBlueprints/Foo.xml``."""
    localization = tmp_path / "Localization"
    source_root = tmp_path / "Base"
    localized_xml = localization / "ObjectBlueprints" / "Creatures.jp.xml"
    source_xml = source_root / "ObjectBlueprints" / "Creatures.xml"
    _write_xml(
        source_xml,
        '<objects><object Name="Glowfish" Description="A &amp;Cglowing&amp;y fish."/></objects>',
    )
    _write_xml(
        localized_xml,
        '<objects><object Name="Glowfish" Description="光る魚。"/></objects>',
    )

    result = main(["--source-root", str(source_root), str(localization)])
    captured = capsys.readouterr()

    assert result == 0
    assert "Creatures.jp.xml" in captured.out
    assert "ObjectBlueprints/Creatures.xml" in captured.out
    assert "@Description" in captured.out
    assert "'&C': 1" in captured.out
    assert "'&y': 1" in captured.out


def test_source_root_missing_source_warning_uses_relative_path(tmp_path: Path) -> None:
    """Missing source XML warning is stable across source-root spelling."""
    localization = tmp_path / "Localization"
    source_root = tmp_path / "Base"
    localized_xml = localization / "ObjectBlueprints" / "Creatures.jp.xml"
    _write_xml(localized_xml, '<objects><object Name="Glowfish"/></objects>')

    absolute_result = validate_xml_file(
        localized_xml,
        source_root=source_root,
        validation_roots=[localization],
    )
    relative_result = validate_xml_file(
        localized_xml,
        source_root=Path("Base"),
        validation_roots=[localization],
    )

    expected_warning = "Source XML not found for Creatures.jp.xml: ObjectBlueprints/Creatures.xml"
    assert absolute_result.warnings == [expected_warning]
    assert relative_result.warnings == [expected_warning]


def test_source_root_comparison_is_opt_in(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    """Source-vs-localized markup comparison is not run without ``--source-root``."""
    localized_xml = tmp_path / "Conversations.jp.xml"
    _write_xml(localized_xml, "<conversations><text>警告</text></conversations>")

    result = main([str(localized_xml)])
    captured = capsys.readouterr()

    assert result == 0
    assert "Markup token drift" not in captured.out


def test_source_root_does_not_report_source_only_overlay_values(
    tmp_path: Path, capsys: pytest.CaptureFixture[str]
) -> None:
    """Source-only values are inherited by merge overlays, so they are not drift."""
    localization = tmp_path / "Localization"
    source_root = tmp_path / "Base"
    localized_xml = localization / "Skills.jp.xml"
    source_xml = source_root / "Skills.xml"
    _write_xml(
        source_xml,
        '<skills><skill Name="Acrobatics" Description="{{G|Jump}}"/></skills>',
    )
    _write_xml(localized_xml, '<skills><skill Name="Acrobatics"/></skills>')

    result = main(["--source-root", str(source_root), str(localized_xml)])
    captured = capsys.readouterr()

    assert result == 0
    assert "Markup token drift" not in captured.out


def test_source_root_matches_keyed_elements_when_sibling_order_differs(
    tmp_path: Path, capsys: pytest.CaptureFixture[str]
) -> None:
    """Element matching prefers stable IDs over sibling position."""
    localization = tmp_path / "Localization"
    source_root = tmp_path / "Base"
    localized_xml = localization / "Conversations.jp.xml"
    source_xml = source_root / "Conversations.xml"
    _write_xml(
        source_xml,
        (
            '<conversations><conversation ID="A"><node ID="Start"><text>{{R|Alert}}</text></node></conversation>'
            '<conversation ID="B"><node ID="Start"><text>{{G|Safe}}</text></node></conversation></conversations>'
        ),
    )
    _write_xml(
        localized_xml,
        (
            '<conversations><conversation ID="B"><node ID="Start"><text>{{G|安全}}</text></node></conversation>'
            '<conversation ID="A"><node ID="Start"><text>警告</text></node></conversation></conversations>'
        ),
    )

    result = main(["--source-root", str(source_root), str(localized_xml)])
    captured = capsys.readouterr()

    assert result == 0
    assert captured.out.count("Markup token drift") == 1
    assert "conversation[@ID='A'][1]" in captured.out
    assert "'{{R|': 1" in captured.out
    assert "'{{G|': 1" not in captured.out


def test_source_root_matches_duplicate_keyed_conditionals_when_sibling_order_differs(
    tmp_path: Path, capsys: pytest.CaptureFixture[str]
) -> None:
    """Duplicate keyed conditional siblings use stable attributes before position."""
    localization = tmp_path / "Localization"
    source_root = tmp_path / "Base"
    localized_xml = localization / "Conversations.jp.xml"
    source_xml = source_root / "Conversations.xml"
    _write_xml(
        source_xml,
        (
            '<conversations><conversation ID="Village">'
            '<node ID="Ask" IfHaveState="met"><text>{{G|Met}}</text></node>'
            '<node ID="Ask" IfHaveState="new"><text>{{R|New}}</text></node>'
            "</conversation></conversations>"
        ),
    )
    _write_xml(
        localized_xml,
        (
            '<conversations><conversation ID="Village">'
            '<node ID="Ask" IfHaveState="new"><text>{{R|新規}}</text></node>'
            '<node ID="Ask" IfHaveState="met"><text>{{G|既知}}</text></node>'
            "</conversation></conversations>"
        ),
    )

    result = main(["--source-root", str(source_root), str(localized_xml)])
    captured = capsys.readouterr()

    assert result == 0
    assert "Markup token drift" not in captured.out


def test_source_root_reports_name_attribute_drift_when_name_is_translated(
    tmp_path: Path, capsys: pytest.CaptureFixture[str]
) -> None:
    """ID-less elements still compare ``@Name`` when the name itself is translated."""
    localization = tmp_path / "Localization"
    source_root = tmp_path / "Base"
    localized_xml = localization / "ObjectBlueprints.jp.xml"
    source_xml = source_root / "ObjectBlueprints.xml"
    _write_xml(source_xml, '<objects><object Name="{{R|Alert}}"/></objects>')
    _write_xml(localized_xml, '<objects><object Name="警告"/></objects>')

    result = main(["--source-root", str(source_root), str(localized_xml)])
    captured = capsys.readouterr()

    assert result == 0
    assert "Markup token drift" in captured.out
    assert "@Name" in captured.out
    assert "'{{R|': 1" in captured.out


def test_source_root_uses_translated_name_fallback_for_description_without_false_positive(
    tmp_path: Path, capsys: pytest.CaptureFixture[str]
) -> None:
    """Translated ID-less ``@Name`` still maps sibling attributes to the source element."""
    localization = tmp_path / "Localization"
    source_root = tmp_path / "Base"
    localized_xml = localization / "ObjectBlueprints.jp.xml"
    source_xml = source_root / "ObjectBlueprints.xml"
    _write_xml(
        source_xml,
        '<objects><object Name="{{R|Alert}}" Description="{{G|Important}}"/></objects>',
    )
    _write_xml(
        localized_xml,
        '<objects><object Name="警告" Description="{{G|重要}}"/></objects>',
    )

    result = main(["--source-root", str(source_root), str(localized_xml)])
    captured = capsys.readouterr()

    assert result == 0
    assert "Markup token drift" in captured.out
    assert "@Name" in captured.out
    assert "@Description" not in captured.out


def test_source_root_reports_description_drift_when_name_is_translated(
    tmp_path: Path, capsys: pytest.CaptureFixture[str]
) -> None:
    """Translated ID-less ``@Name`` fallback applies to all units on that element."""
    localization = tmp_path / "Localization"
    source_root = tmp_path / "Base"
    localized_xml = localization / "ObjectBlueprints.jp.xml"
    source_xml = source_root / "ObjectBlueprints.xml"
    _write_xml(
        source_xml,
        '<objects><object Name="{{R|Alert}}" Description="{{G|Important}}"/></objects>',
    )
    _write_xml(localized_xml, '<objects><object Name="警告" Description="重要"/></objects>')

    result = main(["--source-root", str(source_root), str(localized_xml)])
    captured = capsys.readouterr()

    assert result == 0
    assert captured.out.count("Markup token drift") == 2
    assert "@Name" in captured.out
    assert "@Description" in captured.out
    assert "'{{R|': 1" in captured.out
    assert "'{{G|': 1" in captured.out


def test_source_root_rematches_translated_name_siblings_by_stable_attributes(
    tmp_path: Path, capsys: pytest.CaptureFixture[str]
) -> None:
    """Translated ID-less ``@Name`` siblings rematch by stable attrs before position."""
    localization = tmp_path / "Localization"
    source_root = tmp_path / "Base"
    localized_xml = localization / "Worlds.jp.xml"
    source_xml = source_root / "Worlds.xml"
    _write_xml(
        source_xml,
        (
            '<worlds><world Name="JoppaWorld">'
            '<zone Name="North" Level="10" x="0" y="0" Description="{{G|Safe}}"/>'
            '<zone Name="South" Level="11" x="0" y="0" Description="{{R|Danger}}"/>'
            "</world></worlds>"
        ),
    )
    _write_xml(
        localized_xml,
        (
            '<worlds><world Name="JoppaWorld">'
            '<zone Name="南" Level="11" x="0" y="0" Description="危険"/>'
            '<zone Name="北" Level="10" x="0" y="0" Description="{{G|安全}}"/>'
            "</world></worlds>"
        ),
    )

    result = main(["--source-root", str(source_root), str(localized_xml)])
    captured = capsys.readouterr()

    assert result == 0
    assert captured.out.count("Markup token drift") == 1
    assert "zone[@Name='South'][1] @Description" in captured.out
    assert "'{{R|': 1" in captured.out
    assert "'{{G|': 1" not in captured.out


def test_source_root_maps_reordered_keyed_children_under_positional_parent(
    tmp_path: Path, capsys: pytest.CaptureFixture[str]
) -> None:
    """Children inherit a positionally matched translated parent source path."""
    localization = tmp_path / "Localization"
    source_root = tmp_path / "Base"
    localized_xml = localization / "Conversations.jp.xml"
    source_xml = source_root / "Conversations.xml"
    _write_xml(
        source_xml,
        (
            '<conversations><conversation Name="Village">'
            '<node ID="Keep"><text>{{G|Keep}}</text></node>'
            '<node ID="Drop"><text>{{R|Drop}}</text></node>'
            "</conversation></conversations>"
        ),
    )
    _write_xml(
        localized_xml,
        (
            '<conversations><conversation Name="村">'
            '<node ID="Drop"><text>削除</text></node>'
            '<node ID="Keep"><text>{{G|維持}}</text></node>'
            "</conversation></conversations>"
        ),
    )

    result = main(["--source-root", str(source_root), str(localized_xml)])
    captured = capsys.readouterr()

    assert result == 0
    assert captured.out.count("Markup token drift") == 1
    assert "conversation[@Name='Village'][1]/node[@ID='Drop'][1]/text[1] text()" in captured.out
    assert "'{{R|': 1" in captured.out
    assert "'{{G|': 1" not in captured.out


def test_source_root_parse_os_error_reports_deterministic_warning(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    """Source XML read failures report a stable source-relative warning."""
    localization = tmp_path / "Localization"
    source_root = tmp_path / "Base"
    localized_xml = localization / "ObjectBlueprints" / "Creatures.jp.xml"
    source_xml = source_root / "ObjectBlueprints" / "Creatures.xml"
    _write_xml(localized_xml, '<objects><object Name="Glowfish"/></objects>')
    _write_xml(source_xml, '<objects><object Name="Glowfish"/></objects>')

    original_parse = ET.parse

    def fail_source_parse(path: Path) -> "ET.ElementTree[ET.Element]":
        if Path(path) == source_xml:
            message = "permission denied"
            raise OSError(message)
        return original_parse(path)

    monkeypatch.setattr(ET, "parse", fail_source_parse)

    result = validate_xml_file(
        localized_xml,
        source_root=source_root,
        validation_roots=[localization],
    )

    assert result.warnings == [
        "Source XML read failed for ObjectBlueprints/Creatures.xml: permission denied",
    ]
