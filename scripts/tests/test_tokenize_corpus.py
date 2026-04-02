"""Tests for the tokenize_corpus module."""
# ruff: noqa: SLF001

import sys
from pathlib import Path
from types import SimpleNamespace

import pytest

sys.modules.setdefault("sudachipy", SimpleNamespace(Dictionary=object, SplitMode=SimpleNamespace(B="B")))

from scripts import tokenize_corpus  # noqa: E402


def test_build_protection_pattern_raises_for_empty_terms() -> None:
    """An empty protected-term list is invalid and must fail fast."""
    with pytest.raises(ValueError, match="Protection terms list is empty"):
        tokenize_corpus._build_protection_pattern([])


def test_build_protection_pattern_matches_protected_terms() -> None:
    """A non-empty protected-term list still produces a working regex."""
    pattern = tokenize_corpus._build_protection_pattern(["喰らう者", "スルタン"])

    assert pattern.search("喰らう者の墓所") is not None
    assert pattern.search("スルタンの歴史") is not None


def test_load_protected_terms_sorts_same_length_terms_deterministically(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    """Protected terms use a stable lexical tiebreaker for equal lengths."""
    glossary_path = tmp_path / "glossary.csv"
    glossary_path.write_text(
        "Status,Japanese\n"
        "confirmed,塩の砂漠\n"
        "approved,喰らう者\n"
        "confirmed,スピンドル\n"
        "approved,ゴルゴタ\n",
        encoding="utf-8",
    )
    monkeypatch.setattr(tokenize_corpus, "GLOSSARY_PATH", glossary_path)
    tokenize_corpus._PROTECTED_TERMS.clear()

    try:
        assert tokenize_corpus._load_protected_terms() == ["スピンドル", "ゴルゴタ", "喰らう者", "塩の砂漠"]
    finally:
        tokenize_corpus._PROTECTED_TERMS.clear()
