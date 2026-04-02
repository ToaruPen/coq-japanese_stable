"""Tokenize Japanese text for Markov corpus using SudachiPy with lore-term protection."""
# ruff: noqa: T201

from __future__ import annotations

import csv
import re
from pathlib import Path
from typing import TYPE_CHECKING

from sudachipy import Dictionary, SplitMode

if TYPE_CHECKING:
    from sudachipy import Tokenizer as SudachiTokenizer

GLOSSARY_PATH = Path("docs/glossary.csv")
MIN_PROTECTED_TERM_LENGTH = 2

# Module-level cache for protected terms.
_PROTECTED_TERMS: list[str] = []


def _load_protected_terms() -> list[str]:
    """Load multi-char confirmed/approved terms from glossary, longest first."""
    if _PROTECTED_TERMS:
        return _PROTECTED_TERMS

    terms: set[str] = set()
    with GLOSSARY_PATH.open(encoding="utf-8") as f:
        reader = csv.DictReader(f)
        for row in reader:
            status = row.get("Status", "").strip()
            ja = row.get("Japanese", "").strip()
            if status in ("confirmed", "approved") and len(ja) >= MIN_PROTECTED_TERM_LENGTH:
                terms.add(ja)

    # Sort longest first for greedy replacement
    sorted_terms = sorted(terms, key=lambda term: (-len(term), term))
    _PROTECTED_TERMS.extend(sorted_terms)
    return _PROTECTED_TERMS


def _build_protection_pattern(terms: list[str]) -> re.Pattern[str]:
    """Build a regex pattern that matches any protected term."""
    if not terms:
        msg = "Protection terms list is empty; docs/glossary.csv has no confirmed/approved Japanese terms."
        raise ValueError(msg)
    escaped = [re.escape(t) for t in terms]
    return re.compile("|".join(escaped))


def tokenize_sentence(
    text: str,
    tokenizer: SudachiTokenizer,
    protection_pattern: re.Pattern[str],
) -> str:
    """Tokenize a single sentence with lore-term protection.

    Splits text around protected terms, tokenizes only the gaps,
    then reassembles with protected terms intact as single tokens.
    Returns space-separated morphemes ending with '.'.
    """
    # Split text into alternating (gap, protected_term) segments
    parts = protection_pattern.split(text)
    matches = protection_pattern.findall(text)

    result_tokens: list[str] = []
    for i, part in enumerate(parts):
        # Tokenize the non-protected gap
        if part.strip():
            morphemes = [m.surface() for m in tokenizer.tokenize(part, SplitMode.B)]
            for morph in morphemes:
                stripped = morph.strip()
                if stripped:
                    result_tokens.append(stripped)
        # Insert the protected term (if any follows this gap)
        if i < len(matches):
            result_tokens.append(matches[i])

    # Ensure sentence ends with '.'
    if result_tokens and result_tokens[-1] != ".":
        if result_tokens[-1].endswith("."):
            last = result_tokens[-1][:-1].strip()
            if last:
                result_tokens[-1] = last
            else:
                result_tokens.pop()
            result_tokens.append(".")
        else:
            result_tokens.append(".")

    return " ".join(result_tokens)


def create_tokenizer() -> tuple[SudachiTokenizer, re.Pattern[str]]:
    """Initialize SudachiPy tokenizer and protection pattern."""
    terms = _load_protected_terms()
    pattern = _build_protection_pattern(terms)
    dictionary = Dictionary()
    tokenizer = dictionary.create()
    return tokenizer, pattern


if __name__ == "__main__":
    tok, pat = create_tokenizer()
    test_sentences = [
        "喰らう者の墓所は古代の遺構である.",
        "スルタンの歴史はクロームの秘密と共に語られる.",
        "ジョッパの村からスピンドルが見える.",
        "バラサラムはゴルゴタの廃墟を調査した.",
        "チャヴァはレシェフの宿敵である.",
        "六日のスティルトは塩の砂漠にそびえ立つ.",
        "クッドの地は荒涼としている.",
    ]
    print(f"Protected terms ({len(_PROTECTED_TERMS)}): {_PROTECTED_TERMS}")
    print()
    for sent in test_sentences:
        result = tokenize_sentence(sent, tok, pat)
        print(f"  IN:  {sent}")
        print(f"  OUT: {result}")
        print()
