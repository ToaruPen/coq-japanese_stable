"""Tests for dictionary provenance auditing."""

from __future__ import annotations

import json
from typing import TYPE_CHECKING

from scripts.provenance.dictionary_auditor import audit_dictionaries, load_dictionary_entries
from scripts.provenance.models import AuditFindingKind

if TYPE_CHECKING:
    from pathlib import Path


def _write_dict(
    path: Path,
    entries: list[dict[str, object]],
    *,
    dict_id: str | None = None,
    include_meta: bool = True,
    include_rules: bool = True,
    patterns: list[dict[str, object]] | None = None,
) -> None:
    """Write a test dictionary JSON file with configurable schema variants."""
    path.parent.mkdir(parents=True, exist_ok=True)
    data: dict[str, object] = {"entries": entries}
    if include_meta:
        data["meta"] = {"id": dict_id or path.name.removesuffix(".ja.json"), "lang": "ja", "version": "0.1.0"}
    if include_rules:
        data["rules"] = {"protectColorTags": True, "protectHtmlEntities": True}
    if patterns is not None:
        data["patterns"] = patterns
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")


def test_load_dictionary_entries(tmp_path: Path) -> None:
    """Loader reads standard dictionaries with metadata."""
    _write_dict(tmp_path / "test.ja.json", [{"key": "Inventory", "text": "インベントリ"}], dict_id="test")
    entries = load_dictionary_entries(tmp_path)
    assert len(entries) == 1
    assert entries[0].dictionary_id == "test"
    assert entries[0].key == "Inventory"


def test_load_dictionary_entries_without_meta_uses_filename(tmp_path: Path) -> None:
    """Entries-only dictionaries derive their id from the file name."""
    _write_dict(
        tmp_path / "ui-death.ja.json",
        [{"key": "Dead", "text": "死亡"}],
        include_meta=False,
        include_rules=False,
    )
    entries = load_dictionary_entries(tmp_path)
    assert entries[0].dictionary_id == "ui-death"


def test_load_dictionary_entries_ignores_patterns_variant(tmp_path: Path) -> None:
    """Patterns are ignored because provenance analysis only audits explicit entries."""
    _write_dict(
        tmp_path / "messages.ja.json",
        [],
        include_meta=False,
        include_rules=False,
        patterns=[{"pattern": "^You hit (.+)$", "template": "{0}に命中した"}],
    )
    assert load_dictionary_entries(tmp_path) == []


def test_detect_trailing_whitespace_fragment(tmp_path: Path) -> None:
    """Keys with trailing whitespace are flagged as fragments."""
    _write_dict(
        tmp_path / "msg.ja.json",
        [{"key": "You stagger ", "text": "よろめかせた："}],  # noqa: RUF001
        dict_id="msg",
    )
    findings = audit_dictionaries(tmp_path)
    fragment_findings = [finding for finding in findings if finding.kind == AuditFindingKind.FRAGMENT_KEY]
    assert len(fragment_findings) == 1
    assert "trailing whitespace" in fragment_findings[0].message.lower()


def test_detect_leading_whitespace_fragment(tmp_path: Path) -> None:
    """Keys with leading whitespace are flagged as suffix fragments."""
    _write_dict(
        tmp_path / "msg.ja.json",
        [{"key": " with your shield block!", "text": "（盾ブロック）"}],  # noqa: RUF001
        dict_id="msg",
    )
    findings = audit_dictionaries(tmp_path)
    fragment_findings = [finding for finding in findings if finding.kind == AuditFindingKind.FRAGMENT_KEY]
    assert len(fragment_findings) == 1
    assert "leading whitespace" in fragment_findings[0].message.lower()


def test_detect_incomplete_bracket_fragment(tmp_path: Path) -> None:
    """Keys ending with an unmatched bracket are treated as fragments."""
    _write_dict(
        tmp_path / "msg.ja.json",
        [{"key": "You fail to deal damage with your attack! [", "text": "攻撃はダメージを与えられなかった！["}],  # noqa: RUF001
        dict_id="msg",
    )
    findings = audit_dictionaries(tmp_path)
    fragment_findings = [finding for finding in findings if finding.kind == AuditFindingKind.FRAGMENT_KEY]
    assert len(fragment_findings) == 1


def test_normal_entry_no_fragment_finding(tmp_path: Path) -> None:
    """Complete labels are not flagged as fragments."""
    _write_dict(tmp_path / "ui.ja.json", [{"key": "Inventory", "text": "インベントリ"}], dict_id="ui")
    findings = audit_dictionaries(tmp_path)
    fragment_findings = [finding for finding in findings if finding.kind == AuditFindingKind.FRAGMENT_KEY]
    assert fragment_findings == []


def test_detect_duplicate_key_across_dictionaries(tmp_path: Path) -> None:
    """Duplicate keys across files produce a finding with related dictionary metadata."""
    shared_entry = {"key": "You stagger {target} with your shield block!", "text": "翻訳A"}
    _write_dict(tmp_path / "a.ja.json", [shared_entry], dict_id="a")
    _write_dict(tmp_path / "b.ja.json", [{**shared_entry, "text": "翻訳B"}], dict_id="b")
    findings = audit_dictionaries(tmp_path)
    duplicate_findings = [finding for finding in findings if finding.kind == AuditFindingKind.DUPLICATE_KEY]
    assert len(duplicate_findings) == 1
    assert duplicate_findings[0].related_dictionary_id == "a"


def test_detect_placeholder_mismatch(tmp_path: Path) -> None:
    """Different placeholder sets between key and text are flagged."""
    _write_dict(
        tmp_path / "msg.ja.json",
        [{"key": "You pass by {target}.", "text": "{targets}のそばを通り過ぎた。"}],
        dict_id="msg",
    )
    findings = audit_dictionaries(tmp_path)
    placeholder_findings = [finding for finding in findings if finding.kind == AuditFindingKind.PLACEHOLDER_MISMATCH]
    assert len(placeholder_findings) == 1


def test_matching_placeholders_no_finding(tmp_path: Path) -> None:
    """Matching placeholder sets are accepted."""
    _write_dict(
        tmp_path / "msg.ja.json",
        [{"key": "You pass by {targets}.", "text": "{targets}のそばを通り過ぎた。"}],
        dict_id="msg",
    )
    findings = audit_dictionaries(tmp_path)
    placeholder_findings = [finding for finding in findings if finding.kind == AuditFindingKind.PLACEHOLDER_MISMATCH]
    assert placeholder_findings == []


def test_dead_placeholder_in_text(tmp_path: Path) -> None:
    """Text-only placeholders are reported without fragment false positives."""
    _write_dict(
        tmp_path / "msg.ja.json",
        [{"key": "You hit", "text": "{target}に命中した。"}],
        dict_id="msg",
    )
    findings = audit_dictionaries(tmp_path)
    dead_findings = [finding for finding in findings if finding.kind == AuditFindingKind.DEAD_PLACEHOLDER]
    fragment_findings = [finding for finding in findings if finding.kind == AuditFindingKind.FRAGMENT_KEY]
    assert len(dead_findings) == 1
    assert fragment_findings == []
