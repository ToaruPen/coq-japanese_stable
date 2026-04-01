"""Batch-translate English corpus to Japanese via Codex CLI."""
# ruff: noqa: BLE001, C901, D103, PLR0912, PLR0915, PLW1510, PLW2901, RUF001, S603, S607, T201

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

TRANSLATION_PROMPT_TEMPLATE = """\
以下のJSON配列に含まれる英文を日本語に翻訳し、[{{"id": N, "ja": "翻訳文"}}] 形式のJSON配列のみを出力してください。

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
    return json.loads(INPUT_PATH.read_text(encoding="utf-8"))


def load_existing_translations() -> dict[int, str]:
    """Load already-translated entries to allow resume."""
    existing: dict[int, str] = {}
    if OUTPUT_PATH.exists():
        for entry in json.loads(OUTPUT_PATH.read_text(encoding="utf-8")):
            existing[entry["id"]] = entry["ja"]
    # Also check tmp file from earlier Copilot run
    tmp = Path("scripts/corpus_ja_translated.json.tmp")
    if tmp.exists():
        for entry in json.loads(tmp.read_text(encoding="utf-8")):
            existing.setdefault(entry["id"], entry["ja"])
    return existing


def extract_json_array(text: str) -> list[dict] | None:
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


def translate_chunk(chunk: list[dict], chunk_idx: int, total_chunks: int) -> list[dict]:
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
    if not normalized.endswith("."):
        normalized += "."
    return normalized


def _collect_chunk_translations(chunk: list[dict], translated: list[dict]) -> tuple[dict[int, str], list[str]]:
    """Collect only valid translations for the current chunk."""
    expected_ids = {entry["id"] for entry in chunk}
    chunk_translations: dict[int, str] = {}
    invalid_results: list[str] = []

    for item in translated:
        tid = item.get("id")
        ja = item.get("ja")
        if tid not in expected_ids:
            invalid_results.append(f"unexpected id={tid!r}")
            continue
        if tid in chunk_translations:
            invalid_results.append(f"duplicate id={tid!r}")
            continue
        if not isinstance(ja, str):
            invalid_results.append(f"id={tid!r} ja is not a string")
            continue

        normalized = _normalize_translation_text(ja)
        if normalized is None:
            invalid_results.append(f"id={tid!r} empty or invalid punctuation")
            continue
        chunk_translations[tid] = normalized

    return chunk_translations, invalid_results


def save_progress(translations: dict[int, str], all_entries: list[dict]) -> None:
    """Save current progress to output file."""
    output = [{"id": e["id"], "ja": translations[e["id"]]} for e in all_entries if e["id"] in translations]
    OUTPUT_PATH.write_text(json.dumps(output, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def main() -> None:
    entries = load_input()
    translations = load_existing_translations()
    print(f"Total sentences: {len(entries)}")
    print(f"Already translated: {len(translations)}")

    # Filter out already-translated entries
    remaining = [e for e in entries if e["id"] not in translations]
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
            except Exception as e:
                print(f"  -> Error: {e}, retry {retries + 1}/{max_retries}")
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
    else:
        print("All chunks succeeded!")


if __name__ == "__main__":
    main()
