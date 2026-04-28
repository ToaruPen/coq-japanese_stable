from __future__ import annotations

import json
from typing import TYPE_CHECKING

from scripts import check_translation_tokens

if TYPE_CHECKING:
    from pathlib import Path

    import pytest


def _write_entries(path: Path, entries: list[dict[str, str]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps({"entries": entries}, ensure_ascii=False), encoding="utf-8")


def _write_duplicate_baseline(path: Path, *, texts: list[str], entry_count: int = 2) -> None:
    path.write_text(
        json.dumps(
            {
                "version": 2,
                "duplicate_conflicts": [
                    {
                        "path": "Dictionaries/demo.ja.json",
                        "key": "Same source",
                        "entry_count": entry_count,
                        "texts": sorted(texts),
                    },
                ],
            },
            ensure_ascii=False,
        ),
        encoding="utf-8",
    )


def test_cli_reports_missing_source_tokens_for_dictionary_json(
    tmp_path: Path,
    capsys: pytest.CaptureFixture[str],
) -> None:
    """The CLI reports dropped markup and placeholder tokens in dictionary JSON."""
    localization = tmp_path / "Localization"
    _write_entries(
        localization / "Dictionaries" / "demo.ja.json",
        [
            {
                "key": "{{R|Alert}} && &W ^y <color=#B1C9C3FF>hot</color> {0}",
                "text": "警告 &W <color=#B1C9C3FF>熱</color>",
            },
        ],
    )

    exit_code = check_translation_tokens.main([str(localization)])

    captured = capsys.readouterr()
    assert exit_code == 1
    assert "Dictionaries/demo.ja.json" in captured.out
    assert "missing translation tokens" in captured.out
    assert "placeholder multiset mismatch" in captured.out


def test_cli_passes_when_source_tokens_are_preserved_and_translation_adds_decoration(
    tmp_path: Path,
    capsys: pytest.CaptureFixture[str],
) -> None:
    """The gate is asymmetric for markup and allows translation-only decoration."""
    localization = tmp_path / "Localization"
    _write_entries(
        localization / "Dictionaries" / "demo.ja.json",
        [
            {
                "key": "{{R|Alert}} && &W ^y <color=#B1C9C3FF>hot</color> {0}",
                "text": "{{R|警告}} && &W ^y <color=#B1C9C3FF>熱</color> {0} {{G|追加}}",
            },
        ],
    )

    exit_code = check_translation_tokens.main([str(localization)])

    captured = capsys.readouterr()
    assert exit_code == 0
    assert "0 issue(s)" in captured.out


def test_duplicate_source_key_conflict_fails_unless_baselined(
    tmp_path: Path,
    capsys: pytest.CaptureFixture[str],
) -> None:
    """Same-file duplicate source keys fail only when their conflict is not baselined."""
    localization = tmp_path / "Localization"
    _write_entries(
        localization / "Dictionaries" / "demo.ja.json",
        [
            {"key": "Same source", "text": "訳語A"},
            {"key": "Same source", "text": "訳語B"},
        ],
    )

    assert check_translation_tokens.main([str(localization)]) == 1
    captured = capsys.readouterr()
    assert "duplicate source key conflict" in captured.out

    baseline = tmp_path / "baseline.json"
    _write_duplicate_baseline(baseline, texts=["訳語A", "訳語B"])

    assert check_translation_tokens.main([str(localization), "--duplicate-conflict-baseline", str(baseline)]) == 0


def test_duplicate_source_key_baseline_fails_when_text_state_changes(
    tmp_path: Path,
    capsys: pytest.CaptureFixture[str],
) -> None:
    """Baselined duplicate conflicts are allowed only while their text state matches exactly."""
    localization = tmp_path / "Localization"
    target = localization / "Dictionaries" / "demo.ja.json"
    baseline = tmp_path / "baseline.json"
    _write_duplicate_baseline(baseline, texts=["訳語A", "訳語B"])
    _write_entries(
        target,
        [
            {"key": "Same source", "text": "訳語A"},
            {"key": "Same source", "text": "訳語C"},
        ],
    )

    assert check_translation_tokens.main([str(localization), "--duplicate-conflict-baseline", str(baseline)]) == 1
    captured = capsys.readouterr()
    assert "duplicate conflict baseline changed" in captured.out
    assert "訳語C" in captured.out

    _write_entries(
        target,
        [
            {"key": "Same source", "text": "訳語A"},
        ],
    )

    assert check_translation_tokens.main([str(localization), "--duplicate-conflict-baseline", str(baseline)]) == 1
    captured = capsys.readouterr()
    assert "missing current conflict" in captured.out


def test_duplicate_baseline_path_is_stable_for_localization_relative_cli_shapes(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    """Duplicate baseline keys stay stable from the localization root and direct JSON file paths."""
    localization = tmp_path / "Localization"
    _write_entries(
        localization / "Dictionaries" / "demo.ja.json",
        [
            {"key": "Same source", "text": "訳語A"},
            {"key": "Same source", "text": "訳語B"},
        ],
    )
    baseline = tmp_path / "baseline.json"
    _write_duplicate_baseline(baseline, texts=["訳語A", "訳語B"])

    monkeypatch.chdir(localization)
    assert check_translation_tokens.main([".", "--duplicate-conflict-baseline", str(baseline)]) == 0
    assert check_translation_tokens.main(["Dictionaries", "--duplicate-conflict-baseline", str(baseline)]) == 0

    monkeypatch.chdir(localization / "Dictionaries")
    assert check_translation_tokens.main(["demo.ja.json", "--duplicate-conflict-baseline", str(baseline)]) == 0


def test_blueprint_templates_first_slice_entries_are_scanned_for_token_loss(
    tmp_path: Path,
    capsys: pytest.CaptureFixture[str],
) -> None:
    """BlueprintTemplates `entries` are covered by the first-slice token gate."""
    localization = tmp_path / "Localization"
    _write_entries(
        localization / "BlueprintTemplates" / "templates.ja.json",
        [
            {
                "key": "{{g|The {0} glows.}}",
                "text": "{0}が光る。",
            },
        ],
    )

    exit_code = check_translation_tokens.main([str(localization)])

    captured = capsys.readouterr()
    assert exit_code == 1
    assert "BlueprintTemplates/templates.ja.json" in captured.out
    assert "missing translation tokens" in captured.out
