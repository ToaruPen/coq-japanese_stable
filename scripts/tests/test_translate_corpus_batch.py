"""Tests for the translate_corpus_batch module."""

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

    prompt = translate_corpus_batch.build_translation_prompt('[{"id": 1, "en": "UniqueTerm."}]')

    assert "UniqueTerm = ユニーク語" in prompt
    assert '[{"id": 1, "en": "UniqueTerm."}]' in prompt


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
        {"id": 1, "en": "One."},
        {"id": 2, "en": "Two."},
        {"id": 3, "en": "Three."},
        {"id": 4, "en": "Four."},
    ]
    attempts = iter(
        [
            [{"id": 1, "ja": "一。"}, {"id": 2, "ja": "二。"}],
            [
                {"id": 1, "ja": "一。"},
                {"id": 2, "ja": "二。"},
                {"id": 3, "ja": "三。"},
                {"id": 4, "ja": "四。"},
            ],
        ]
    )
    snapshots: list[dict[int, str]] = []

    monkeypatch.setattr(translate_corpus_batch, "load_input", lambda: entries)
    monkeypatch.setattr(translate_corpus_batch, "load_existing_translations", dict)
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

    assert snapshots == [{1: "一.", 2: "二.", 3: "三.", 4: "四."}]
