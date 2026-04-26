"""Translate accepted annals candidates via Codex CLI batch invocation."""
# ruff: noqa: T201, S607, RUF001, ANN401, C901, PLR0912, PLR2004

from __future__ import annotations

import argparse
import hashlib
import json
import re
import shutil
import subprocess
import sys
from pathlib import Path
from typing import Any

CHUNK_SIZE_DEFAULT = 8
MAX_RETRIES = 3
PLACEHOLDER_RE = re.compile(r"\{(t?)(\d+)\}")


def _canonical_json(payload: Any) -> str:
    return json.dumps(payload, sort_keys=True, ensure_ascii=False, separators=(",", ":"))


def compute_en_template_hash(candidate: dict[str, Any]) -> str:
    """Hash the structural identity of a candidate (excludes status/ja_template/reason)."""
    payload = {
        "extracted_pattern": candidate["extracted_pattern"],
        "slots": candidate["slots"],
        "sample_source": candidate["sample_source"],
        "event_property": candidate["event_property"],
        "switch_case": candidate.get("switch_case"),
    }
    digest = hashlib.sha256(_canonical_json(payload).encode("utf-8")).hexdigest()
    return f"sha256:{digest}"


def select_pending_candidates(candidates: list[dict[str, Any]]) -> list[dict[str, Any]]:
    """Pick candidates that need a fresh translation."""
    pending: list[dict[str, Any]] = []
    for c in candidates:
        if c["status"] != "accepted":
            continue
        current_hash = compute_en_template_hash(c)
        if c.get("ja_template", "") == "" or c.get("en_template_hash") != current_hash:
            pending.append(c)
    return pending


def chunk_candidates(
    candidates: list[dict[str, Any]],
    chunk_size: int = CHUNK_SIZE_DEFAULT,
) -> list[list[dict[str, Any]]]:
    """Split candidates into chunks of at most chunk_size."""
    return [candidates[i : i + chunk_size] for i in range(0, len(candidates), chunk_size)]


def validate_chunk_response(
    chunk: list[dict[str, Any]],
    response: list[dict[str, Any]],
) -> tuple[bool, list[str]]:
    """Validate that the response covers all chunk IDs with valid ja_templates."""
    errors: list[str] = []
    expected_ids = {c["id"] for c in chunk}
    received_ids = {item.get("id") for item in response if isinstance(item, dict)}
    missing = expected_ids - received_ids
    extra = received_ids - expected_ids
    if missing:
        errors.append(f"missing translations for ids: {sorted(missing)}")
    if extra:
        errors.append(f"unexpected ids in response: {sorted(extra)}")

    by_id = {c["id"]: c for c in chunk}
    for item in response:
        if not isinstance(item, dict):
            errors.append(f"non-object response entry: {item!r}")
            continue
        cid = item.get("id")
        if cid not in by_id:
            continue
        ja = item.get("ja_template", "")
        if not ja:
            errors.append(f"id={cid}: empty ja_template")
            continue
        candidate = by_id[cid]
        try:
            capture_count = re.compile(candidate["extracted_pattern"]).groups
        except re.error as exc:
            errors.append(f"id={cid}: candidate regex compile fail: {exc}")
            continue
        for match in PLACEHOLDER_RE.finditer(ja):
            slot_index = int(match.group(2))
            if slot_index >= capture_count:
                errors.append(f"id={cid}: placeholder {{...{slot_index}}} exceeds capture count {capture_count}")
                break
    return (not errors, errors)


def build_prompt(chunk: list[dict[str, Any]], glossary: str, all_candidates: list[dict[str, Any]]) -> str:
    """Build the per-chunk Codex prompt with backstory context."""
    context_summary = "\n".join(
        f"- {c['id']}: {c['sample_source'][:100]}{'…' if len(c['sample_source']) > 100 else ''}" for c in all_candidates
    )
    chunk_payload = json.dumps(
        [
            {
                "id": c["id"],
                "event_property": c["event_property"],
                "sample_source": c["sample_source"],
                "extracted_pattern": c["extracted_pattern"],
                "slots": c["slots"],
            }
            for c in chunk
        ],
        ensure_ascii=False,
        indent=2,
    )
    return (
        "あなたはCaves of Qudの「Sultan Histories」（スルタン史）ジャーナル文を翻訳します。\n"
        '出力は厳密なJSON配列のみ。各要素は {"id": ..., "ja_template": ...} のみ。\n'
        "## 必須用語\n"
        f"{glossary}\n\n"
        "## 翻訳ルール\n"
        "1. ja_template には extracted_pattern の capture group に対応する {t0} {t1} ... を使う\n"
        "2. capture が翻訳対象の固有名詞・場所名なら {tN}（per-capture lookup あり）\n"
        "3. capture が year のような構造値なら {N}（lookup なし）\n"
        "4. 文末は半角ピリオド「.」ではなく日本語句点「。」\n"
        "5. 古英語・伝承調の英文は擬古文調の日本語に\n"
        "6. JSON配列のみ出力、コメント・説明は禁止\n\n"
        "## Resheph背景一覧（文脈共有用、訳す対象は下のチャンクのみ）\n"
        f"{context_summary}\n\n"
        "## 翻訳対象チャンク\n"
        f"{chunk_payload}\n"
    )


def invoke_codex_translation(
    chunk: list[dict[str, Any]],
    glossary: str,
    all_candidates: list[dict[str, Any]] | None = None,
) -> list[dict[str, Any]] | None:
    """Invoke the Codex CLI and return parsed JSON, or None on failure."""
    if all_candidates is None:
        all_candidates = chunk
    prompt = build_prompt(chunk, glossary, all_candidates)
    if not shutil.which("codex"):
        print("error: codex CLI not on PATH", file=sys.stderr)
        return None
    try:
        result = subprocess.run(
            ["codex", "exec", "-s", "read-only", "-c", 'approval_policy="never"', "-"],
            input=prompt,
            capture_output=True,
            text=True,
            check=False,
        )
    except OSError as exc:
        print(f"error: failed to invoke codex CLI: {exc}", file=sys.stderr)
        return None
    if result.returncode != 0:
        print(f"error: codex CLI exit {result.returncode}: {result.stderr}", file=sys.stderr)
        return None
    try:
        return json.loads(result.stdout)
    except json.JSONDecodeError:
        # Try to extract a JSON array from a response that might have surrounding text
        match = re.search(r"\[[\s\S]*\]", result.stdout)
        if match:
            try:
                return json.loads(match.group(0))
            except json.JSONDecodeError:
                pass
        return None


def save_progress(path: Path, doc: dict[str, Any]) -> None:
    """Write the doc back to disk in canonical formatting."""
    text = json.dumps(doc, ensure_ascii=False, indent=2) + "\n"
    path.write_text(text, encoding="utf-8")


def load_glossary_from_existing_pipeline() -> str:
    """Reuse the existing translation_glossary.txt loader from translate_corpus_batch.py."""
    glossary_path = Path("scripts/translation_glossary.txt")
    if not glossary_path.is_file():
        return ""
    return glossary_path.read_text(encoding="utf-8").strip()


def translate_chunk_with_retries(
    chunk: list[dict[str, Any]],
    glossary: str,
    all_candidates: list[dict[str, Any]],
) -> dict[str, str]:
    """Return id -> ja_template for successfully translated entries.

    Retry strategy per spec §3.4:
    - Whole JSON unparseable → re-send entire chunk (max 3)
    - Parsed but missing IDs → next retry sends only failed IDs
    - 100% match required
    """
    successes: dict[str, str] = {}
    remaining = chunk
    for attempt in range(1, MAX_RETRIES + 1):
        response = invoke_codex_translation(remaining, glossary, all_candidates)
        if response is None:
            print(
                f"[translate] attempt {attempt}/{MAX_RETRIES}: unparseable; retrying full chunk",
                file=sys.stderr,
            )
            continue
        valid, errors = validate_chunk_response(remaining, response)
        # Even on partial success, harvest valid items
        for item in response:
            if not isinstance(item, dict):
                continue
            cid = item.get("id")
            ja = item.get("ja_template", "")
            if cid in {c["id"] for c in remaining} and ja and cid not in successes:
                # Per-item placeholder check
                candidate = next((c for c in remaining if c["id"] == cid), None)
                if candidate is None:
                    continue
                try:
                    capture_count = re.compile(candidate["extracted_pattern"]).groups
                except re.error:
                    continue
                ok = True
                for m in PLACEHOLDER_RE.finditer(ja):
                    if int(m.group(2)) >= capture_count:
                        ok = False
                        break
                if ok:
                    successes[cid] = ja

        if valid and len(successes) == len(chunk):
            return successes

        # Recompute remaining: ids not yet in successes
        remaining = [c for c in chunk if c["id"] not in successes]
        if not remaining:
            return successes
        print(
            f"[translate] attempt {attempt}/{MAX_RETRIES}: {errors}; will retry {len(remaining)} ids",
            file=sys.stderr,
        )

    return successes


def main(argv: list[str] | None = None) -> int:
    """Entry point for the translate_annals_patterns CLI."""
    parser = argparse.ArgumentParser(description="Translate accepted annals candidates.")
    parser.add_argument("path", type=Path, help="Path to candidates JSON")
    parser.add_argument("--chunk-size", type=int, default=CHUNK_SIZE_DEFAULT)
    args = parser.parse_args(argv)

    try:
        doc = json.loads(args.path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        print(f"error: cannot read {args.path}: {exc}", file=sys.stderr)
        return 1

    candidates = doc.get("candidates", [])
    pending = select_pending_candidates(candidates)
    if not pending:
        print("[translate] nothing to translate (all accepted candidates have current translations)")
        return 0

    glossary = load_glossary_from_existing_pipeline()
    if not glossary:
        print("error: glossary file scripts/translation_glossary.txt missing or empty", file=sys.stderr)
        return 1

    print(
        f"[translate] {len(pending)} candidate(s) pending across"
        f" {len(chunk_candidates(pending, args.chunk_size))} chunk(s)"
    )

    by_id = {c["id"]: c for c in candidates}
    any_failure = False
    for chunk in chunk_candidates(pending, args.chunk_size):
        successes = translate_chunk_with_retries(chunk, glossary, candidates)
        for cid, ja in successes.items():
            target = by_id[cid]
            target["ja_template"] = ja
            target["en_template_hash"] = compute_en_template_hash(target)
        save_progress(args.path, doc)
        for c in chunk:
            if c["id"] not in successes:
                # Downgrade to needs_manual
                target = by_id[c["id"]]
                target["status"] = "needs_manual"
                target["reason"] = "translation retries exhausted; review manually"
                any_failure = True
        save_progress(args.path, doc)

    return 1 if any_failure else 0


if __name__ == "__main__":
    raise SystemExit(main())
