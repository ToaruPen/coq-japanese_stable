"""Tests for the translate_corpus_batch module."""
# ruff: noqa: SLF001

import json
from pathlib import Path

import pytest

from scripts import translate_corpus_batch


def test_build_translation_prompt_uses_glossary_file(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    """The translation prompt injects glossary content from the canonical file."""
    glossary_path = tmp_path / "translation_glossary.txt"
    glossary_path.write_text("UniqueTerm = ユニーク語", encoding="utf-8")
    monkeypatch.setattr(translate_corpus_batch, "GLOSSARY_PATH", glossary_path)

    prompt = translate_corpus_batch.build_translation_prompt('[{"index": 0, "id": 1, "en": "UniqueTerm."}]')

    assert "UniqueTerm = ユニーク語" in prompt
    assert '[{"index": 0, "id": 1, "en": "UniqueTerm."}]' in prompt


def test_build_translation_prompt_rejects_empty_glossary(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    """An empty glossary file must fail fast."""
    glossary_path = tmp_path / "translation_glossary.txt"
    glossary_path.write_text("", encoding="utf-8")
    monkeypatch.setattr(translate_corpus_batch, "GLOSSARY_PATH", glossary_path)

    with pytest.raises(ValueError, match="Glossary file is empty"):
        translate_corpus_batch.build_translation_prompt("[]")


def test_main_does_not_merge_low_match_trials(monkeypatch: pytest.MonkeyPatch) -> None:
    """Partial low-match retries do not pollute the saved translation state."""
    entries = [
        {"index": 0, "id": 1, "en": "One."},
        {"index": 1, "id": 2, "en": "Two."},
        {"index": 2, "id": 3, "en": "Three."},
        {"index": 3, "id": 4, "en": "Four."},
    ]
    attempts = iter(
        [
            [{"index": 0, "id": 1, "ja": "一。"}, {"index": 1, "id": 2, "ja": "二。"}],
            [
                {"index": 0, "id": 1, "ja": "一。"},
                {"index": 1, "id": 2, "ja": "二。"},
                {"index": 2, "id": 3, "ja": "三。"},
                {"index": 3, "id": 4, "ja": "四。"},
            ],
        ]
    )
    snapshots: list[dict[int, str]] = []

    monkeypatch.setattr(translate_corpus_batch, "load_input", lambda: entries)
    monkeypatch.setattr(translate_corpus_batch, "load_existing_translations", lambda _entries: {})
    monkeypatch.setattr(
        translate_corpus_batch,
        "translate_chunk",
        lambda _chunk, _idx, _total: next(attempts),
    )
    monkeypatch.setattr(
        translate_corpus_batch,
        "save_progress",
        lambda translations, _entries: snapshots.append(dict(translations)),
    )
    monkeypatch.setattr(translate_corpus_batch.time, "sleep", lambda _seconds: None)

    translate_corpus_batch.main()

    assert snapshots == [{0: "一.", 1: "二.", 2: "三.", 3: "四."}]


def test_collect_chunk_translations_tracks_duplicate_ids_by_index() -> None:
    """Duplicate source ids remain distinct when their per-entry indexes differ."""
    chunk = [
        {"index": 10, "id": 7, "en": "First."},
        {"index": 11, "id": 7, "en": "Second."},
    ]

    chunk_translations, invalid_results = translate_corpus_batch._collect_chunk_translations(
        chunk,
        [
            {"index": 10, "id": 7, "ja": "一つ目。"},
            {"index": 11, "id": 7, "ja": "二つ目。"},
        ],
    )

    assert chunk_translations == {10: "一つ目.", 11: "二つ目."}
    assert invalid_results == []


def test_load_existing_translations_rejects_duplicate_id_progress_without_indexes(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    """Legacy progress rows without indexes fail fast once ids are ambiguous."""
    output_path = tmp_path / "corpus_ja_translated.json"
    output_path.write_text(
        json.dumps([{"id": 7, "ja": "既存訳."}], ensure_ascii=False),
        encoding="utf-8",
    )
    monkeypatch.setattr(translate_corpus_batch, "OUTPUT_PATH", output_path)

    with pytest.raises(ValueError, match="duplicate id 7"):
        translate_corpus_batch.load_existing_translations(
            [
                {"index": 10, "id": 7, "en": "First."},
                {"index": 11, "id": 7, "en": "Second."},
            ]
        )
