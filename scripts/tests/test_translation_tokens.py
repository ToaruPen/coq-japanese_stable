from __future__ import annotations

import json
from collections import Counter
from pathlib import Path

import pytest

from scripts import check_translation_tokens


def _write_entries(path: Path, entries: list[dict[str, str]]) -> None:
    _write_payload(path, {"entries": entries})


def _write_payload(path: Path, payload: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, ensure_ascii=False), encoding="utf-8")


def _missing_translation_tokens(key: str, text: str) -> Counter[str]:
    return check_translation_tokens._missing_translation_tokens(key, text)  # noqa: SLF001


def _same_file_duplicate_state(*, texts: list[str], entry_count: int = 2) -> dict[str, object]:
    return {
        "scope": "same_file",
        "path": "Dictionaries/demo.ja.json",
        "key": "Same source",
        "entry_count": entry_count,
        "texts": sorted(texts),
        "occurrences": [
            {"path": "Dictionaries/demo.ja.json", "entry_index": index, "text": text}
            for index, text in enumerate(texts, start=1)
        ],
    }


def _cross_file_duplicate_state(*, texts: list[str]) -> dict[str, object]:
    paths = ["Dictionaries/a.ja.json", "Dictionaries/b.ja.json"]
    return {
        "scope": "cross_file",
        "path": "Dictionaries",
        "key": "Shared source",
        "entry_count": len(texts),
        "texts": sorted(texts),
        "occurrences": [
            {"path": path, "entry_index": 1, "text": text} for path, text in zip(paths, texts, strict=True)
        ],
    }


def _write_duplicate_baseline(path: Path, *states: dict[str, object]) -> None:
    path.write_text(
        json.dumps(
            {
                "version": 3,
                "duplicate_conflicts": list(states),
            },
            ensure_ascii=False,
        ),
        encoding="utf-8",
    )


def _run_with_duplicate_baseline(tmp_path: Path, payload: object) -> int:
    localization = tmp_path / "Localization"
    _write_entries(localization / "Dictionaries" / "demo.ja.json", [{"key": "Source", "text": "訳"}])
    baseline = tmp_path / "baseline.json"
    baseline.write_text(json.dumps(payload), encoding="utf-8")

    return check_translation_tokens.main([str(localization), "--duplicate-conflict-baseline", str(baseline)])


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


def test_cli_reports_missing_bare_qud_span_token(
    tmp_path: Path,
    capsys: pytest.CaptureFixture[str],
) -> None:
    """Bare Qud spans such as `{{R}}` are source tokens, not disposable text."""
    localization = tmp_path / "Localization"
    _write_entries(
        localization / "Dictionaries" / "demo.ja.json",
        [
            {
                "key": "{{R}}",
                "text": "",
            },
        ],
    )

    exit_code = check_translation_tokens.main([str(localization)])

    captured = capsys.readouterr()
    assert exit_code == 1
    assert "missing translation tokens: '{{R}}': 1" in captured.out


def test_cli_accepts_bare_qud_span_as_tagged_translation(
    tmp_path: Path,
    capsys: pytest.CaptureFixture[str],
) -> None:
    """Bare Qud spans may translate to the equivalent tagged span form."""
    localization = tmp_path / "Localization"
    _write_entries(
        localization / "Dictionaries" / "demo.ja.json",
        [
            {
                "key": "{{phase-conjugate}}",
                "text": "{{phase-conjugate|位相共役}}",
            },
        ],
    )

    exit_code = check_translation_tokens.main([str(localization)])

    captured = capsys.readouterr()
    assert exit_code == 0
    assert "0 issue(s)" in captured.out


def test_cli_reports_missing_game_text_variable_token(
    tmp_path: Path,
    capsys: pytest.CaptureFixture[str],
) -> None:
    """GameText variable tokens such as `=variable.name=` must be preserved."""
    localization = tmp_path / "Localization"
    _write_entries(
        localization / "Dictionaries" / "demo.ja.json",
        [
            {
                "key": "Talk to =creature.displayName=.",
                "text": "話しかける。",
            },
        ],
    )

    exit_code = check_translation_tokens.main([str(localization)])

    captured = capsys.readouterr()
    assert exit_code == 1
    assert "missing translation tokens: '=creature.displayName=': 1" in captured.out


def test_game_text_variable_token_regex_captures_real_shapes() -> None:
    """Real GameText variables may contain colons and apostrophes."""
    value = "=object.Does:are= =subject.Name's= =subject.T's= =verb:beep:afterpronoun="

    assert check_translation_tokens._translation_token_multiset(value) == {  # noqa: SLF001
        "=object.Does:are=": 1,
        "=subject.Name's=": 1,
        "=subject.T's=": 1,
        "=verb:beep:afterpronoun=": 1,
    }


@pytest.mark.parametrize(
    ("key", "text"),
    [
        ("=subject.T= =verb:slip= on the slime!", "=subject.T=はスライムで滑った。"),
        ("=object.Does:are= too old.", "=object.name=は古すぎる。"),
        ("=subject.The==subject.name= =verb:start= up.", "=subject.name=が起動した。"),
        (
            "You touch =subject.t= and recall =pronouns.possessive= passcode. "
            "=pronouns.Subjective= =verb:beep:afterpronoun= warmly.",
            "あなたは=subject.name=に触れ、=subject.name=のパスコードを思い出した。"
            "=subject.name=が温かくビープ音を鳴らした。",
        ),
        (
            "{{g|You touch =subject.the==subject.name= and recall =pronouns.possessive= passcode.}}",
            "{{g|あなたは=subject.name=に触れ、=subject.name=のパスコードを思い出した。}}",
        ),
        ("=subject.T= =verb:consume= =object.an=.", "=subject.name=が=object.name=を消費した。"),
        (
            "=object.T= =object.verb:react= with =subject.t= and =object.verb:convert= =pronouns.objective=.",
            "=object.name=が=subject.name=と反応し、=subject.name=を変換した。",
        ),
        ("=subject.T= =verb:swallow= =object.t=!", "=subject.name=が=object.name=を飲み込んだ!"),
        ("=subject.T= =verb:bat= =subject.possessive= wings.", "=subject.name=が翼をはためかせた。"),
        ("=object.verb:burst= =objpronouns.reflexive=.", "自壊した。"),
        ("=subject.Name's= shell cracked.", "=subject.name=の殻が割れた。"),
        ("=subject.T's= shell cracked.", "=subject.name=の殻が割れた。"),
    ],
)
def test_cli_allows_explicit_game_text_variable_equivalents(
    tmp_path: Path,
    capsys: pytest.CaptureFixture[str],
    key: str,
    text: str,
) -> None:
    """Japanese GameText can encode selected English grammar tokens explicitly."""
    localization = tmp_path / "Localization"
    _write_entries(localization / "Dictionaries" / "demo.ja.json", [{"key": key, "text": text}])

    exit_code = check_translation_tokens.main([str(localization)])

    captured = capsys.readouterr()
    assert exit_code == 0
    assert "0 issue(s)" in captured.out


def test_game_text_variable_equivalents_consume_translation_occurrences() -> None:
    """One Japanese-side equivalent can satisfy only one consuming source token."""
    missing_tokens = _missing_translation_tokens(
        "=subject.t= =pronouns.Subjective=",
        "=subject.name=",
    )

    assert missing_tokens == Counter({"=pronouns.Subjective=": 1})


def test_game_text_variable_equivalents_do_not_reuse_exact_subject_name_occurrences() -> None:
    """Exact source-token preservation reserves the matching translation occurrence."""
    missing_tokens = _missing_translation_tokens(
        "=subject.name= =pronouns.Subjective=",
        "=subject.name=",
    )

    assert missing_tokens == Counter({"=pronouns.Subjective=": 1})


def test_game_text_object_article_equivalent_does_not_reuse_exact_object_name_occurrences() -> None:
    """Object article variables consume a separate object-name equivalent."""
    missing_tokens = _missing_translation_tokens(
        "=object.name= =object.an=",
        "=object.name=",
    )

    assert missing_tokens == Counter({"=object.an=": 1})


def test_game_text_exact_name_reservation_selects_non_overlapping_occurrence() -> None:
    """Exact name preservation should leave a genitive occurrence available when possible."""
    missing_tokens = _missing_translation_tokens(
        "=subject.name= =subject.Name's=",
        "=subject.name=の =subject.name=",
    )

    assert missing_tokens == Counter()


def test_game_text_variable_equivalents_do_not_reuse_subject_name_for_possessive_chain() -> None:
    """A single name equivalent cannot cover multiple consuming source references."""
    missing_tokens = _missing_translation_tokens(
        "=subject.t= =pronouns.possessive= =pronouns.Subjective=",
        "=subject.name=",
    )

    assert missing_tokens == Counter({"=pronouns.possessive=": 1, "=pronouns.Subjective=": 1})


def test_game_text_variable_equivalents_do_not_reuse_overlapping_text_ranges() -> None:
    """A genitive name occurrence cannot also cover a separate bare-name reference."""
    missing_tokens = _missing_translation_tokens(
        "=subject.t= =subject.Name's=",
        "=subject.name=の",
    )

    assert missing_tokens == Counter({"=subject.Name's=": 1})


def test_game_text_variable_equivalents_match_overlapping_candidates_when_assignment_is_valid() -> None:
    """A bare-name token should not greedily steal the prefix of a genitive occurrence."""
    missing_tokens = _missing_translation_tokens(
        "=subject.Name's= =subject.t=",
        "=subject.name=の =subject.name=",
    )

    assert missing_tokens == Counter()


def test_game_text_article_only_tokens_do_not_consume_subject_name_equivalents() -> None:
    """Article-only variables are explicitly non-consuming for Japanese text."""
    missing_tokens = _missing_translation_tokens(
        "=subject.the= =pronouns.Subjective=",
        "=subject.name=",
    )

    assert missing_tokens == Counter()


def test_cli_scans_pure_formatter_source_key_token_loss(
    tmp_path: Path,
    capsys: pytest.CaptureFixture[str],
) -> None:
    """Pure formatter source keys are translation leaves and must not be pruned."""
    localization = tmp_path / "Localization"
    _write_entries(
        localization / "Dictionaries" / "demo.ja.json",
        [
            {
                "key": "{{phase-conjugate}}",
                "text": "位相共役",
            },
        ],
    )

    exit_code = check_translation_tokens.main([str(localization)])

    captured = capsys.readouterr()
    assert exit_code == 1
    assert "missing translation tokens: '{{phase-conjugate}}': 1" in captured.out


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


def test_cli_rejects_payload_without_entries_list(
    tmp_path: Path,
    capsys: pytest.CaptureFixture[str],
) -> None:
    """Malformed translation payloads fail fast instead of being treated as empty."""
    localization = tmp_path / "Localization"
    _write_payload(localization / "Dictionaries" / "demo.ja.json", {})

    exit_code = check_translation_tokens.main([str(localization)])

    captured = capsys.readouterr()
    assert exit_code == 1
    assert "Translation payload must contain an 'entries' list" in captured.err
    assert "Dictionaries/demo.ja.json" in captured.err


def test_cli_rejects_non_object_translation_payload(
    tmp_path: Path,
    capsys: pytest.CaptureFixture[str],
) -> None:
    """Top-level translation payloads must be JSON objects."""
    localization = tmp_path / "Localization"
    _write_payload(localization / "Dictionaries" / "demo.ja.json", [])

    exit_code = check_translation_tokens.main([str(localization)])

    captured = capsys.readouterr()
    assert exit_code == 1
    assert "Translation payload must be a JSON object with an 'entries' list" in captured.err
    assert "Dictionaries/demo.ja.json" in captured.err


def test_cli_rejects_non_list_entries_payload(
    tmp_path: Path,
    capsys: pytest.CaptureFixture[str],
) -> None:
    """The top-level `entries` member must be a list."""
    localization = tmp_path / "Localization"
    _write_payload(localization / "Dictionaries" / "demo.ja.json", {"entries": {}})

    exit_code = check_translation_tokens.main([str(localization)])

    captured = capsys.readouterr()
    assert exit_code == 1
    assert "Translation payload must contain an 'entries' list" in captured.err
    assert "Dictionaries/demo.ja.json" in captured.err


def test_load_json_wraps_parse_errors_with_path_context(tmp_path: Path) -> None:
    """JSON parse failures keep the original exception and identify the file."""
    target = tmp_path / "Localization" / "Dictionaries" / "broken.ja.json"
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text('{"entries": [', encoding="utf-8")

    with pytest.raises(ValueError, match="Failed to load JSON") as exc_info:
        check_translation_tokens._load_json(target)  # noqa: SLF001

    assert str(target) in str(exc_info.value)
    assert isinstance(exc_info.value.__cause__, json.JSONDecodeError)


def test_cli_rejects_malformed_translation_entry_with_index(
    tmp_path: Path,
    capsys: pytest.CaptureFixture[str],
) -> None:
    """Malformed entries report path context and the one-based entry index."""
    localization = tmp_path / "Localization"
    _write_payload(localization / "Dictionaries" / "demo.ja.json", {"entries": ["not an object"]})

    exit_code = check_translation_tokens.main([str(localization)])

    captured = capsys.readouterr()
    assert exit_code == 1
    assert "Translation entry must be a JSON object" in captured.err
    assert "Dictionaries/demo.ja.json" in captured.err
    assert "entry_index=1" in captured.err


def test_cli_rejects_entry_without_string_key_and_text(
    tmp_path: Path,
    capsys: pytest.CaptureFixture[str],
) -> None:
    """Entry shape validation covers missing and non-string key/text fields."""
    localization = tmp_path / "Localization"
    _write_payload(localization / "Dictionaries" / "demo.ja.json", {"entries": [{"key": "Source"}]})

    exit_code = check_translation_tokens.main([str(localization)])

    captured = capsys.readouterr()
    assert exit_code == 1
    assert "Translation entry must contain string 'key' and 'text'" in captured.err
    assert "Dictionaries/demo.ja.json" in captured.err
    assert "entry_index=1" in captured.err


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
    _write_duplicate_baseline(baseline, _same_file_duplicate_state(texts=["訳語A", "訳語B"]))

    assert check_translation_tokens.main([str(localization), "--duplicate-conflict-baseline", str(baseline)]) == 0


def test_duplicate_source_key_with_identical_text_fails_unless_baselined(
    tmp_path: Path,
    capsys: pytest.CaptureFixture[str],
) -> None:
    """Same-file duplicate source keys fail even when their translations match."""
    localization = tmp_path / "Localization"
    _write_entries(
        localization / "Dictionaries" / "demo.ja.json",
        [
            {"key": "Same source", "text": "同じ訳語"},
            {"key": "Same source", "text": "同じ訳語"},
        ],
    )

    assert check_translation_tokens.main([str(localization)]) == 1
    captured = capsys.readouterr()
    assert "duplicate source key conflict" in captured.out
    assert "entry_count=2" in captured.out

    baseline = tmp_path / "baseline.json"
    _write_duplicate_baseline(baseline, _same_file_duplicate_state(texts=["同じ訳語", "同じ訳語"]))

    assert check_translation_tokens.main([str(localization), "--duplicate-conflict-baseline", str(baseline)]) == 0


def test_dictionary_cross_file_duplicate_key_with_divergent_text_fails_unless_baselined(
    tmp_path: Path,
    capsys: pytest.CaptureFixture[str],
) -> None:
    """Dictionary keys reused across files fail when Japanese text diverges."""
    localization = tmp_path / "Localization"
    _write_entries(localization / "Dictionaries" / "a.ja.json", [{"key": "Shared source", "text": "訳語A"}])
    _write_entries(localization / "Dictionaries" / "b.ja.json", [{"key": "Shared source", "text": "訳語B"}])

    assert check_translation_tokens.main([str(localization)]) == 1
    captured = capsys.readouterr()
    assert "duplicate source key conflict" in captured.out
    assert "scope=cross_file" in captured.out
    assert "Dictionaries/a.ja.json" in captured.out
    assert "Dictionaries/b.ja.json" in captured.out

    baseline = tmp_path / "baseline.json"
    _write_duplicate_baseline(baseline, _cross_file_duplicate_state(texts=["訳語A", "訳語B"]))

    assert check_translation_tokens.main([str(localization), "--duplicate-conflict-baseline", str(baseline)]) == 0


def test_dictionary_cross_file_duplicate_key_with_identical_text_passes(
    tmp_path: Path,
    capsys: pytest.CaptureFixture[str],
) -> None:
    """Cross-file dictionary reuse is allowed when the Japanese text is identical."""
    localization = tmp_path / "Localization"
    _write_entries(localization / "Dictionaries" / "a.ja.json", [{"key": "Shared source", "text": "同じ訳語"}])
    _write_entries(localization / "Dictionaries" / "b.ja.json", [{"key": "Shared source", "text": "同じ訳語"}])

    assert check_translation_tokens.main([str(localization)]) == 0
    captured = capsys.readouterr()
    assert "0 issue(s)" in captured.out


def test_duplicate_source_key_baseline_fails_when_text_state_changes(
    tmp_path: Path,
    capsys: pytest.CaptureFixture[str],
) -> None:
    """Baselined duplicate conflicts are allowed only while their text state matches exactly."""
    localization = tmp_path / "Localization"
    target = localization / "Dictionaries" / "demo.ja.json"
    baseline = tmp_path / "baseline.json"
    _write_duplicate_baseline(baseline, _same_file_duplicate_state(texts=["訳語A", "訳語B"]))
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
    _write_duplicate_baseline(baseline, _same_file_duplicate_state(texts=["訳語A", "訳語B"]))

    monkeypatch.chdir(localization)
    assert check_translation_tokens.main([".", "--duplicate-conflict-baseline", str(baseline)]) == 0
    assert check_translation_tokens.main(["Dictionaries", "--duplicate-conflict-baseline", str(baseline)]) == 0

    monkeypatch.chdir(localization / "Dictionaries")
    assert check_translation_tokens.main(["demo.ja.json", "--duplicate-conflict-baseline", str(baseline)]) == 0


def test_collect_translation_json_files_deduplicates_relative_and_absolute_inputs(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    """Collector canonicalizes inputs so relative and absolute shapes do not duplicate scans."""
    localization = tmp_path / "Localization"
    target = localization / "Dictionaries" / "demo.ja.json"
    _write_entries(target, [{"key": "Source", "text": "訳"}])

    monkeypatch.chdir(localization)

    files = check_translation_tokens.collect_translation_json_files([Path(), target])

    assert files == [target.resolve()]


def test_duplicate_baseline_rejects_missing_version(
    tmp_path: Path,
    capsys: pytest.CaptureFixture[str],
) -> None:
    """Duplicate baseline files must declare the expected schema version."""
    assert _run_with_duplicate_baseline(tmp_path, {"duplicate_conflicts": []}) == 1

    captured = capsys.readouterr()
    assert "must contain version 3" in captured.err


def test_duplicate_baseline_rejects_version_mismatch(
    tmp_path: Path,
    capsys: pytest.CaptureFixture[str],
) -> None:
    """Duplicate baseline files fail fast when their schema version is unexpected."""
    assert _run_with_duplicate_baseline(tmp_path, {"version": 1, "duplicate_conflicts": []}) == 1

    captured = capsys.readouterr()
    assert "expected version 3" in captured.err


def test_duplicate_baseline_rejects_non_object_payload(
    tmp_path: Path,
    capsys: pytest.CaptureFixture[str],
) -> None:
    """Duplicate baseline files must be JSON objects before fields are read."""
    assert _run_with_duplicate_baseline(tmp_path, []) == 1

    captured = capsys.readouterr()
    assert "must be a JSON object" in captured.err


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
