"""Tests for the assemble_corpus module."""
# ruff: noqa: SLF001

import re

import pytest

from scripts import assemble_corpus


def test_merge_japanese_sentences_reports_dynamic_llm_total() -> None:
    """Missing-row errors report the actual number of expected llm rows."""
    manifest_rows = [
        {"id": 1, "en": "One.", "source": "llm"},
        {"id": 2, "en": "Two.", "source": "llm"},
        {"id": 3, "en": "Three.", "source": "llm"},
    ]
    translated_by_manifest_index = {0: "一.", 1: "二."}

    with pytest.raises(RuntimeError) as exc_info:
        assemble_corpus._merge_japanese_sentences(manifest_rows, translated_by_manifest_index)

    message = str(exc_info.value)
    assert "all 3 llm rows" in message
    assert "7441" not in message


def test_tokenize_sentences_raises_for_invalid_rows(monkeypatch: pytest.MonkeyPatch) -> None:
    """Invalid tokenized rows fail loudly with actionable row details."""
    monkeypatch.setattr(assemble_corpus, "TOKENIZER_IMPORT_ERROR", None)
    monkeypatch.setattr(assemble_corpus, "create_tokenizer", lambda: (object(), re.compile("protect")))

    def fake_tokenize_sentence(text: str, _tokenizer: object, _pattern: re.Pattern[str]) -> str:
        if text == "bad":
            return "."
        if text == "roman":
            return "latin ."
        return "日本語 ."

    monkeypatch.setattr(assemble_corpus, "tokenize_sentence", fake_tokenize_sentence)

    with pytest.raises(ValueError, match="Tokenization produced invalid corpus rows") as exc_info:
        assemble_corpus._tokenize_sentences(["bad", "roman", "valid"])

    message = str(exc_info.value)
    assert "index=0 reason=empty-or-period-only+missing-japanese-characters" in message
    assert "index=1 reason=missing-japanese-characters" in message
    assert "source='bad'" in message
    assert "source='roman'" in message
