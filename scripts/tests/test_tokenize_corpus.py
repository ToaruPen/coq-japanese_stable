"""Tests for the tokenize_corpus module."""
# ruff: noqa: SLF001

import sys
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
