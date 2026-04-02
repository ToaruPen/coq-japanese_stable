"""Assemble the final Japanese Markov corpus from reused and translated rows."""

from __future__ import annotations

import json
import re
from collections import Counter, defaultdict, deque
from itertools import pairwise
from pathlib import Path
from typing import Any

# ruff: noqa: EM102, TRY003, TRY004
try:
    from tokenize_corpus import create_tokenizer, tokenize_sentence
except ModuleNotFoundError as exc:
    create_tokenizer = None
    tokenize_sentence = None
    TOKENIZER_IMPORT_ERROR = exc
else:
    TOKENIZER_IMPORT_ERROR = None

MANIFEST_PATH = Path("scripts/reuse_manifest.json")
TRANSLATION_INPUT_PATH = Path("scripts/corpus_en_for_translation.json")
TRANSLATED_PATH = Path("scripts/corpus_ja_translated.json")
OUTPUT_PATH = Path("Mods/QudJP/Localization/Corpus/LibraryCorpus.ja.json")
CORPUS_ORDER = 2
JAPANESE_CHARACTER_PATTERN = re.compile(r"[\u3040-\u309F\u30A0-\u30FF\u4E00-\u9FFF]")
LEXICAL_TOKEN_PATTERN = re.compile(r"[0-9A-Za-z\u3040-\u309F\u30A0-\u30FF\u4E00-\u9FFF]")
DOUBLE_BRACKET_PATTERN = re.compile(r"\[\[[^\[\]]+\]\]")
INLINE_STYLE_PATTERN = re.compile(r"\{\{[^|{}]+\|([^{}]*)\}\}")
SINGLE_BRACKET_PATTERN = re.compile(r"\[([^\[\]]+)\]")
ELLIPSIS_PATTERN = re.compile(r"(?:\.|…){2,}")

JsonRow = dict[str, Any]
ManifestKey = tuple[int, str]


def _load_json_array(path: Path, *, label: str) -> list[JsonRow]:
    if not path.is_file():
        raise FileNotFoundError(f"{label} file not found: {path}")

    data = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(data, list):
        raise ValueError(f"{label} must be a JSON array: {path}")

    rows: list[JsonRow] = []
    for index, item in enumerate(data):
        if not isinstance(item, dict):
            raise ValueError(f"{label} row {index} is not a JSON object: {item!r}")
        rows.append(item)
    return rows


def _require_int(row: JsonRow, field: str, *, label: str, index: int) -> int:
    value = row.get(field)
    if not isinstance(value, int):
        raise ValueError(f"{label} row {index} field '{field}' must be an int: {value!r}")
    return value


def _require_text(row: JsonRow, field: str, *, label: str, index: int) -> str:
    value = row.get(field)
    if not isinstance(value, str):
        raise ValueError(f"{label} row {index} field '{field}' must be a string: {value!r}")

    normalized = value.strip()
    if not normalized:
        raise ValueError(f"{label} row {index} field '{field}' is empty.")
    return normalized


def _manifest_key(row: JsonRow, *, label: str, index: int) -> ManifestKey:
    return (
        _require_int(row, "id", label=label, index=index),
        _require_text(row, "en", label=label, index=index),
    )


def _build_manifest_position_queues(manifest_rows: list[JsonRow]) -> dict[ManifestKey, deque[int]]:
    positions: dict[ManifestKey, deque[int]] = defaultdict(deque)
    for index, row in enumerate(manifest_rows):
        positions[_manifest_key(row, label="reuse manifest", index=index)].append(index)
    return positions


def _assign_translations_to_manifest(
    manifest_rows: list[JsonRow],
    translation_input_rows: list[JsonRow],
    translated_rows: list[JsonRow],
) -> dict[int, str]:
    if len(translated_rows) > len(translation_input_rows):
        raise RuntimeError(
            "Translated rows exceed translation input rows. "
            f"translated={len(translated_rows)} input={len(translation_input_rows)}"
        )

    positions_by_key = _build_manifest_position_queues(manifest_rows)
    translated_by_manifest_index: dict[int, str] = {}

    for index, translated_row in enumerate(translated_rows):
        input_row = translation_input_rows[index]
        expected_id = _require_int(input_row, "id", label="translation input", index=index)
        actual_id = _require_int(translated_row, "id", label="translated corpus", index=index)
        if actual_id != expected_id:
            raise RuntimeError(
                "Translated rows are not aligned with corpus_en_for_translation.json at "
                f"index {index}: expected id {expected_id}, got {actual_id}."
            )

        key = _manifest_key(input_row, label="translation input", index=index)
        matching_positions = positions_by_key.get(key)
        if not matching_positions:
            raise RuntimeError(
                "Could not map translation input row back to reuse_manifest.json at "
                f"index {index}: id={key[0]!r} en={key[1]!r}"
            )

        manifest_index = matching_positions.popleft()
        translated_by_manifest_index[manifest_index] = _require_text(
            translated_row,
            "ja",
            label="translated corpus",
            index=index,
        )

    return translated_by_manifest_index


def _merge_japanese_sentences(
    manifest_rows: list[JsonRow],
    translated_by_manifest_index: dict[int, str],
) -> tuple[list[str], Counter[str]]:
    sentences: list[str] = []
    source_counts: Counter[str] = Counter()
    missing: list[str] = []

    for index, row in enumerate(manifest_rows):
        source = _require_text(row, "source", label="reuse manifest", index=index)
        if source != "llm":
            ja = row.get("ja")
            if not isinstance(ja, str) or not ja.strip():
                raise RuntimeError(
                    "Reuse manifest row is missing Japanese text even though source != 'llm': "
                    f"index={index} id={row.get('id')!r} source={source!r}"
                )
            sentences.append(ja.strip())
            source_counts["reused"] += 1
            continue

        translated = translated_by_manifest_index.get(index)
        if translated is None:
            missing.append(
                f"index={index} id={row.get('id')!r} en={str(row.get('en', ''))[:80]!r}"
            )
            continue

        sentences.append(translated)
        source_counts["translated"] += 1

    if missing:
        sample = "\n".join(f"  - {item}" for item in missing[:10])
        total_llm = source_counts["translated"] + len(missing)
        raise RuntimeError(
            "Missing Japanese for llm-backed manifest rows after merge.\n"
            f"cause: translated corpus covers {source_counts['translated']} llm rows, but "
            f"{len(missing)} remain.\n"
            "impact: LibraryCorpus.ja.json was not written.\n"
            "sample missing rows:\n"
            f"{sample}\n"
            "next action: complete scripts/corpus_ja_translated.json so all "
            f"{total_llm} llm rows "
            "from scripts/corpus_en_for_translation.json are present in order."
        )

    return sentences, source_counts


def _normalize_sentence(text: str) -> str:
    """Replace 。 with . and strip trailing punctuation noise."""
    while True:
        sanitized = INLINE_STYLE_PATTERN.sub(r"\1", text)
        if sanitized == text:
            break
        text = sanitized
    text = DOUBLE_BRACKET_PATTERN.sub(" ", text)
    text = SINGLE_BRACKET_PATTERN.sub(r"\1", text)
    text = text.replace("{", " ").replace("}", " ")
    text = text.replace("[", " ").replace("]", " ")
    text = text.replace("~", " ")
    text = text.replace("。", ".")
    text = text.replace("\uFF01", ".")
    text = text.replace("\uFF1F", ".")
    text = ELLIPSIS_PATTERN.sub("…", text)
    # Remove trailing duplicate periods
    while text.endswith(".."):
        text = text[:-1]
    return text


def _normalize_tokenized_sentence(tokenized: str) -> str:
    tokens = tokenized.split()
    while tokens and not LEXICAL_TOKEN_PATTERN.search(tokens[0]):
        tokens.pop(0)
    if not tokens:
        return ""
    if tokens[-1] != ".":
        tokens.append(".")
    return " ".join(tokens)


def _tokenize_sentences(sentences: list[str]) -> list[str]:
    if TOKENIZER_IMPORT_ERROR is not None:
        msg = (
            "SudachiPy dependencies are required for corpus tokenization. "
            "Install the optional NLP extras first, for example: python3 -m pip install '.[nlp]'"
        )
        raise RuntimeError(msg) from TOKENIZER_IMPORT_ERROR

    if create_tokenizer is None or tokenize_sentence is None:
        msg = "Tokenizer import state is invalid."
        raise RuntimeError(msg)

    tokenizer, protection_pattern = create_tokenizer()
    tokenized: list[str] = []
    invalid_rows: list[str] = []
    for index, sentence in enumerate(sentences):
        normalized = _normalize_sentence(sentence)
        result = tokenize_sentence(normalized, tokenizer, protection_pattern)
        result = _normalize_tokenized_sentence(result)
        reasons: list[str] = []
        if result.strip() in ("", "."):
            reasons.append("empty-or-period-only")
        if not JAPANESE_CHARACTER_PATTERN.search(result):
            reasons.append("missing-japanese-characters")
        if reasons:
            invalid_rows.append(
                f"index={index} reason={'+'.join(reasons)} "
                f"source={sentence[:80]!r} normalized={normalized[:80]!r} tokenized={result[:80]!r}"
            )
            continue
        tokenized.append(result)
    if invalid_rows:
        sample = "\n".join(f"  - {item}" for item in invalid_rows[:10])
        raise ValueError(f"Tokenization produced invalid corpus rows:\n{sample}")
    return tokenized


def _deduplicate(sentences: list[str]) -> list[str]:
    """Remove duplicate sentences while preserving order."""
    seen: set[str] = set()
    unique: list[str] = []
    for s in sentences:
        if s not in seen:
            seen.add(s)
            unique.append(s)
    return unique


def _compute_stats(tokenized_sentences: list[str]) -> tuple[int, int]:
    tokens = [token for sentence in tokenized_sentences for token in sentence.split()]
    unique_bigrams = len(set(pairwise(tokens)))
    return len(tokens), unique_bigrams


def main() -> None:
    """Assemble, tokenize, and write the final Japanese Markov corpus."""
    manifest_rows = _load_json_array(MANIFEST_PATH, label="reuse manifest")
    translation_input_rows = _load_json_array(
        TRANSLATION_INPUT_PATH,
        label="translation input",
    )
    translated_rows = _load_json_array(TRANSLATED_PATH, label="translated corpus")

    translated_by_manifest_index = _assign_translations_to_manifest(
        manifest_rows,
        translation_input_rows,
        translated_rows,
    )
    merged_sentences, source_counts = _merge_japanese_sentences(
        manifest_rows,
        translated_by_manifest_index,
    )
    tokenized_sentences = _tokenize_sentences(merged_sentences)
    tokenized_sentences = _deduplicate(tokenized_sentences)
    total_tokens, unique_bigrams = _compute_stats(tokenized_sentences)

    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(
        json.dumps({"order": CORPUS_ORDER, "sentences": tokenized_sentences}, ensure_ascii=False, indent=2)
        + "\n",
        encoding="utf-8",
    )

    print(f"Manifest rows: {len(manifest_rows)}")  # noqa: T201
    print(f"Translation input rows: {len(translation_input_rows)}")  # noqa: T201
    print(f"Translated rows loaded: {len(translated_rows)}")  # noqa: T201
    print(f"Reused rows used: {source_counts['reused']}")  # noqa: T201
    print(f"Translated rows used: {source_counts['translated']}")  # noqa: T201
    print(f"Total sentences: {len(tokenized_sentences)}")  # noqa: T201
    print(f"Tokens: {total_tokens}")  # noqa: T201
    print(f"Unique bigrams: {unique_bigrams}")  # noqa: T201
    print(f"Wrote: {OUTPUT_PATH}")  # noqa: T201


if __name__ == "__main__":
    main()
