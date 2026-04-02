"""Batch-translate English corpus to Japanese via Codex CLI."""
# ruff: noqa: C901, D103, PLW1510, PLW2901, RUF001, S603, S607, T201

from __future__ import annotations

import json
import re
import subprocess
import time
from pathlib import Path

INPUT_PATH = Path("scripts/corpus_en_for_translation.json")
OUTPUT_PATH = Path("scripts/corpus_ja_translated.json")
CHUNKS_DIR = Path("scripts/translation_chunks")
GLOSSARY_PATH = Path("scripts/translation_glossary.txt")
CHUNK_SIZE = 300  # sentences per Codex call
ENTRY_INDEX_FIELD = "index"

TRANSLATION_PROMPT_TEMPLATE = """\
以下のJSON配列に含まれる英文を日本語に翻訳し、
[{{"index": N, "id": M, "ja": "翻訳文"}}] 形式のJSON配列のみを出力してください。

## 必須用語（必ずこの訳語を使うこと）
{glossary}

## 翻訳ルール
1. 原文のレジスターを維持する — 文語体は文語体のまま、科学的な文は分析的に、格言は簡潔に
2. 英文1文 = 日本語1文（結合・分割しない）
3. 文末は半角ピリオド「.」で終える（「。」は使わない）
4. 固有名詞はカタカナに音写する（複合名は中点「・」で区切る）
5. Qud 世界のロア概念は忠実に訳す — 現実世界の概念に置き換えない
6. これはファンタジー/SF世界「Caves of Qud」のMarkovチェーン用コーパスである
7. 古英語風の文（Whilom, dide, thurgh 等）は擬古文調で訳す
8. JSON配列のみを出力し、解説やコメントは一切付けない

## 入力
{input_json}
"""


def load_glossary() -> str:
    """Load the canonical glossary text used by translators."""
    if not GLOSSARY_PATH.is_file():
        msg = f"Glossary file not found: {GLOSSARY_PATH}"
        raise FileNotFoundError(msg)
    glossary = GLOSSARY_PATH.read_text(encoding="utf-8").strip()
    if not glossary:
        msg = f"Glossary file is empty: {GLOSSARY_PATH}"
        raise ValueError(msg)
    return glossary


def build_translation_prompt(input_json: str) -> str:
    """Build the translation prompt from the canonical glossary and JSON payload."""
    glossary = load_glossary()
    return TRANSLATION_PROMPT_TEMPLATE.format(glossary=glossary, input_json=input_json)


def load_input() -> list[dict]:
    """Load the English corpus."""
    return [
        {**entry, ENTRY_INDEX_FIELD: index}
        for index, entry in enumerate(json.loads(INPUT_PATH.read_text(encoding="utf-8")))
    ]


def _entry_index(entry: dict, *, label: str) -> int:
    """Return the stable per-entry index used for progress tracking."""
    index = entry.get(ENTRY_INDEX_FIELD)
    if not isinstance(index, int):
        msg = f"{label} is missing int field '{ENTRY_INDEX_FIELD}': {entry!r}"
        raise TypeError(msg)
    return index


def _build_progress_id_lookup(all_entries: list[dict]) -> tuple[dict[int, int], set[int]]:
    """Build safe legacy-id lookup and detect ambiguous duplicate ids."""
    indexes_by_id: dict[int, list[int]] = {}
    for entry in all_entries:
        entry_id = entry["id"]
        indexes_by_id.setdefault(entry_id, []).append(_entry_index(entry, label="input entry"))

    unique_ids = {entry_id: indexes[0] for entry_id, indexes in indexes_by_id.items() if len(indexes) == 1}
    duplicate_ids = {entry_id for entry_id, indexes in indexes_by_id.items() if len(indexes) > 1}
    return unique_ids, duplicate_ids


def _resolve_progress_key(
    entry: dict,
    *,
    all_entries: list[dict],
    duplicate_ids: set[int],
    label: str,
    row_index: int,
    unique_ids: dict[int, int],
) -> int:
    """Resolve a saved progress row to the unique translation key."""
    progress_index = entry.get(ENTRY_INDEX_FIELD)
    if isinstance(progress_index, int):
        if not 0 <= progress_index < len(all_entries):
            msg = f"{label} row {row_index} has out-of-range index {progress_index!r}."
            raise ValueError(msg)
        expected_id = all_entries[progress_index]["id"]
        actual_id = entry.get("id")
        if actual_id != expected_id:
            msg = (
                f"{label} row {row_index} has id {actual_id!r} for index {progress_index}, "
                f"expected {expected_id!r}."
            )
            raise ValueError(msg)
        return progress_index

    entry_id = entry.get("id")
    if not isinstance(entry_id, int):
        msg = f"{label} row {row_index} field 'id' must be an int: {entry_id!r}"
        raise TypeError(msg)
    if entry_id in duplicate_ids:
        msg = (
            f"{label} row {row_index} uses duplicate id {entry_id!r} without '{ENTRY_INDEX_FIELD}'; "
            "delete the stale progress file and rerun with the indexed format."
        )
        raise ValueError(msg)
    if entry_id not in unique_ids:
        msg = f"{label} row {row_index} references unknown id {entry_id!r}."
        raise ValueError(msg)
    return unique_ids[entry_id]


def _load_progress_file(
    path: Path,
    *,
    all_entries: list[dict],
    duplicate_ids: set[int],
    label: str,
    unique_ids: dict[int, int],
) -> dict[int, str]:
    """Load a progress file keyed by the stable per-entry index."""
    progress: dict[int, str] = {}
    if not path.exists():
        return progress

    payload = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(payload, list):
        msg = f"{label} must contain a top-level JSON array of progress rows."
        raise TypeError(msg)

    for row_index, entry in enumerate(payload):
        if not isinstance(entry, dict):
            msg = f"{label} row {row_index} must be an object: {entry!r}"
            raise TypeError(msg)
        key = _resolve_progress_key(
            entry,
            all_entries=all_entries,
            duplicate_ids=duplicate_ids,
            label=label,
            row_index=row_index,
            unique_ids=unique_ids,
        )
        if key in progress:
            msg = f"{label} row {row_index} duplicates index {key!r}."
            raise ValueError(msg)
        translation = entry.get("ja")
        if not isinstance(translation, str):
            msg = f"{label} row {row_index} field 'ja' must be a string: {translation!r}"
            raise TypeError(msg)
        normalized = _normalize_translation_text(translation)
        if normalized is None:
            msg = f"{label} row {row_index} field 'ja' has invalid punctuation: {translation!r}"
            raise ValueError(msg)
        progress[key] = normalized
    return progress


def load_existing_translations(all_entries: list[dict]) -> dict[int, str]:
    """Load already-translated entries to allow resume."""
    unique_ids, duplicate_ids = _build_progress_id_lookup(all_entries)
    existing = _load_progress_file(
        OUTPUT_PATH,
        all_entries=all_entries,
        duplicate_ids=duplicate_ids,
        label=str(OUTPUT_PATH),
        unique_ids=unique_ids,
    )

    # Also check tmp file from earlier Copilot run
    tmp = Path("scripts/corpus_ja_translated.json.tmp")
    for key, value in _load_progress_file(
        tmp,
        all_entries=all_entries,
        duplicate_ids=duplicate_ids,
        label=str(tmp),
        unique_ids=unique_ids,
    ).items():
        existing.setdefault(key, value)
    return existing


def extract_json_array(text: str) -> list[object] | None:
    """Extract a JSON array from potentially noisy Codex output."""
    # Try direct parse first
    text = text.strip()
    if text.startswith("["):
        try:
            return json.loads(text)
        except json.JSONDecodeError:
            pass

    # Try to find JSON array in the output
    match = re.search(r"\[[\s\S]*\]", text)
    if match:
        try:
            return json.loads(match.group())
        except json.JSONDecodeError:
            pass

    return None


def translate_chunk(chunk: list[dict], chunk_idx: int, total_chunks: int) -> list[object]:
    """Call Codex to translate a chunk of sentences."""
    input_json = json.dumps(chunk, ensure_ascii=False, indent=1)
    prompt = build_translation_prompt(input_json)

    print(f"  Chunk {chunk_idx}/{total_chunks}: {len(chunk)} sentences, calling Codex...")

    result = subprocess.run(
        [
            "codex", "exec", "--full-auto", "--json",
            "-c", 'model_reasoning_effort="high"',
            prompt,
        ],
        capture_output=True,
        text=True,
        timeout=600,
    )

    # Parse JSON Lines output from Codex
    all_text = []
    for line in result.stdout.splitlines():
        line = line.strip()
        if not line:
            continue
        try:
            obj = json.loads(line)
            if obj.get("type") == "item.completed":
                item = obj.get("item", {})
                if item.get("type") == "agent_message":
                    all_text.append(item.get("text", ""))
                elif item.get("type") == "command_execution":
                    out = item.get("aggregated_output", "")
                    if out:
                        all_text.append(out)
        except json.JSONDecodeError:
            continue

    # Try to extract translated JSON from the output
    for text in reversed(all_text):  # Check latest outputs first
        parsed = extract_json_array(text)
        if parsed and len(parsed) > 0:
            return parsed

    # Fallback: try parsing the entire stdout
    parsed = extract_json_array(result.stdout)
    if parsed:
        return parsed

    print(f"  WARNING: Failed to parse Codex output for chunk {chunk_idx}")
    print(f"  stderr: {result.stderr[:500]}")
    return []


def _normalize_translation_text(text: str) -> str | None:
    """Normalize a translated sentence or return None when it is unusable."""
    normalized = text.rstrip()
    normalized = re.sub(r"[。！？]+$", "", normalized)
    if not normalized:
        return None
    if re.search(r"[。！？]", normalized):
        return None
    if not normalized.endswith("."):
        normalized += "."
    return normalized


def _collect_chunk_translations(chunk: list[dict], translated: list[object]) -> tuple[dict[int, str], list[str]]:
    """Collect only valid translations for the current chunk."""
    expected_entries = {_entry_index(entry, label="chunk entry"): entry for entry in chunk}
    chunk_translations: dict[int, str] = {}
    invalid_results: list[str] = []

    for item in translated:
        if not isinstance(item, dict):
            invalid_results.append(f"non-object result={item!r}")
            continue
        translation_index = item.get(ENTRY_INDEX_FIELD)
        if not isinstance(translation_index, int):
            invalid_results.append(f"missing or invalid index={translation_index!r}")
            continue
        if translation_index not in expected_entries:
            invalid_results.append(f"unexpected index={translation_index!r}")
            continue
        if translation_index in chunk_translations:
            invalid_results.append(f"duplicate index={translation_index!r}")
            continue

        expected_entry = expected_entries[translation_index]
        tid = item.get("id")
        if tid != expected_entry["id"]:
            invalid_results.append(
                f"index={translation_index!r} expected id={expected_entry['id']!r}, got {tid!r}"
            )
            continue

        ja = item.get("ja")
        if not isinstance(ja, str):
            invalid_results.append(f"index={translation_index!r} ja is not a string")
            continue

        normalized = _normalize_translation_text(ja)
        if normalized is None:
            invalid_results.append(f"index={translation_index!r} empty or invalid punctuation")
            continue
        chunk_translations[translation_index] = normalized

    return chunk_translations, invalid_results


def save_progress(translations: dict[int, str], all_entries: list[dict]) -> None:
    """Save current progress to output file."""
    output = [
        {
            ENTRY_INDEX_FIELD: _entry_index(entry, label="input entry"),
            "id": entry["id"],
            "ja": translations[_entry_index(entry, label="input entry")],
        }
        for entry in all_entries
        if _entry_index(entry, label="input entry") in translations
    ]
    OUTPUT_PATH.write_text(json.dumps(output, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def main() -> None:
    entries = load_input()
    translations = load_existing_translations(entries)
    print(f"Total sentences: {len(entries)}")
    print(f"Already translated: {len(translations)}")

    # Filter out already-translated entries
    remaining = [entry for entry in entries if _entry_index(entry, label="input entry") not in translations]
    print(f"Remaining to translate: {len(remaining)}")

    if not remaining:
        print("All sentences already translated!")
        save_progress(translations, entries)
        return

    # Split into chunks
    chunks = [remaining[i:i + CHUNK_SIZE] for i in range(0, len(remaining), CHUNK_SIZE)]
    total_chunks = len(chunks)
    print(f"Chunks: {total_chunks} (size={CHUNK_SIZE})")

    failed_chunks = []
    for idx, chunk in enumerate(chunks, 1):
        retries = 0
        max_retries = 2
        while retries <= max_retries:
            try:
                translated = translate_chunk(chunk, idx, total_chunks)
                if translated:
                    chunk_translations, invalid_results = _collect_chunk_translations(chunk, translated)
                    matched = len(chunk_translations)
                    print(f"  -> Translated {matched}/{len(chunk)} sentences")
                    if invalid_results:
                        print(f"  -> Ignored {len(invalid_results)} invalid results")
                    if matched < len(chunk) * 0.8:
                        print("  WARNING: Low match rate, retrying...")
                        retries += 1
                        continue
                    translations.update(chunk_translations)
                    save_progress(translations, entries)
                    print(f"  Progress saved: {len(translations)}/{len(entries)} total")
                    break
                print(f"  -> No output, retry {retries + 1}/{max_retries}")
                retries += 1
            except subprocess.TimeoutExpired:
                print(f"  -> Timeout, retry {retries + 1}/{max_retries}")
                retries += 1

        if retries > max_retries:
            failed_chunks.append(idx)
            print(f"  FAILED chunk {idx} after {max_retries} retries")

        # Brief pause between API calls
        if idx < total_chunks:
            time.sleep(2)

    print("\n=== Complete ===")
    print(f"Translated: {len(translations)}/{len(entries)}")
    if failed_chunks:
        print(f"Failed chunks: {failed_chunks}")
        raise SystemExit(1)
    print("All chunks succeeded!")


if __name__ == "__main__":
    main()
