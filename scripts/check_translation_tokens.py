"""Validate JSON localization entries for source-side translation token preservation."""

from __future__ import annotations

import argparse
import json
import re
import sys
from collections import Counter, defaultdict
from dataclasses import dataclass
from pathlib import Path

_DEFAULT_DUPLICATE_BASELINE = Path(__file__).with_name("translation_token_duplicate_baseline.json")
_DUPLICATE_BASELINE_VERSION = 2
_MIN_CONFLICTING_TRANSLATIONS = 2

_BARE_QUD_SPAN = re.compile(r"\{\{[^|{}]+\}\}")
_QUD_OPENER = re.compile(r"\{\{[^|}]+\|")
_QUD_CLOSER = re.compile(r"\}\}")
_LITERAL_AMPERSAND = re.compile(r"&&")
_LITERAL_CARET = re.compile(r"\^\^")
_LEGACY_AMPERSAND_COLOR = re.compile(r"(?<!&)&(?!&)[A-Za-z0-9]")
_LEGACY_CARET_COLOR = re.compile(r"(?<!\^)\^(?!\^)[A-Za-z0-9]")
_HTML_COLOR_OPEN = re.compile(r"<color=[^>]+>")
_HTML_COLOR_CLOSE = re.compile(r"</color>")
_PLACEHOLDER = re.compile(r"\{([0-9]+)(?::[^{}]*)?\}")


@dataclass(frozen=True)
class TranslationEntry:
    """A current-slice JSON localization entry."""

    path: Path
    relative_path: str
    index: int
    key: str
    text: str


@dataclass(frozen=True)
class TranslationIssue:
    """A validation issue reported by the translation-token gate."""

    relative_path: str
    kind: str
    detail: str
    entry_index: int | None = None
    key: str | None = None
    text: str | None = None


@dataclass(frozen=True)
class DuplicateConflictState:
    """Expected or observed same-file duplicate source-key conflict state."""

    path: str
    key: str
    entry_count: int
    texts: tuple[str, ...]


def _path_candidates(path: Path) -> tuple[Path, Path]:
    resolved = path.resolve()
    return (path, resolved) if path != resolved else (path,)


def _relative_path_from_marker(path: Path, marker: str) -> str | None:
    parts = path.parts
    if marker not in parts:
        return None
    index = len(parts) - 1 - parts[::-1].index(marker)
    return Path(*parts[index:]).as_posix()


def _relative_asset_path(path: Path) -> str:
    for candidate in _path_candidates(path):
        relative_path = _relative_path_from_marker(candidate, "Localization")
        if relative_path is not None:
            return Path(*Path(relative_path).parts[1:]).as_posix()
        for marker in ("Dictionaries", "BlueprintTemplates"):
            relative_path = _relative_path_from_marker(candidate, marker)
            if relative_path is not None:
                return relative_path
    return path.name


def _is_target_translation_file(path: Path) -> bool:
    if path.suffix != ".json" or not path.name.endswith(".ja.json"):
        return False
    return any(
        "Dictionaries" in candidate.parts or candidate.parent.name == "BlueprintTemplates"
        for candidate in _path_candidates(path)
    )


def collect_translation_json_files(paths: list[Path]) -> list[Path]:
    """Collect JSON localization files covered by the non-GUI token gate."""
    files: set[Path] = set()
    for input_path in paths:
        if not input_path.exists():
            msg = f"Path not found: {input_path}"
            raise FileNotFoundError(msg)

        resolved_input_path = input_path.resolve()
        if resolved_input_path.is_file():
            if _is_target_translation_file(resolved_input_path):
                files.add(resolved_input_path)
            continue

        if resolved_input_path.is_dir():
            files.update(
                path.resolve() for path in resolved_input_path.rglob("*.ja.json") if _is_target_translation_file(path)
            )
            continue

        msg = f"Path is not a regular file or directory: {input_path}"
        raise ValueError(msg)

    return sorted(files, key=lambda item: item.as_posix())


def _load_json(path: Path) -> object:
    payload: object = json.loads(path.read_text(encoding="utf-8"))
    return payload


def _entries_from_payload(payload: object, *, path_context: str) -> list[object]:
    if not isinstance(payload, dict):
        msg = f"Translation payload must be a JSON object with an 'entries' list: {path_context}"
        raise TypeError(msg)

    entries = payload.get("entries")
    if not isinstance(entries, list):
        msg = f"Translation payload must contain an 'entries' list: {path_context}"
        raise TypeError(msg)
    return entries


def iter_translation_entries(path: Path) -> list[TranslationEntry]:
    """Read current `entries[].key/text` dictionary and BlueprintTemplates JSON.

    Issue #409 first slice intentionally does not compare BlueprintTemplates
    GameText variable usage. Current shipped BlueprintTemplates data is covered
    only where it exposes top-level `entries` with source `key` and Japanese
    `text` fields.
    """
    payload = _load_json(path)
    entries: list[TranslationEntry] = []
    relative_path = _relative_asset_path(path)
    for index, item in enumerate(_entries_from_payload(payload, path_context=relative_path), start=1):
        if not isinstance(item, dict):
            msg = f"Translation entry must be a JSON object: {relative_path} entry_index={index}"
            raise TypeError(msg)
        key = item.get("key")
        text = item.get("text")
        if not isinstance(key, str) or not isinstance(text, str):
            msg = f"Translation entry must contain string 'key' and 'text': {relative_path} entry_index={index}"
            raise TypeError(msg)
        if not key.strip():
            continue
        entries.append(
            TranslationEntry(
                path=path,
                relative_path=relative_path,
                index=index,
                key=key,
                text=text,
            )
        )
    return entries


def _translation_token_multiset(value: str) -> Counter[str]:
    tokens: Counter[str] = Counter()
    tokens.update(match.group(0) for match in _BARE_QUD_SPAN.finditer(value))
    bare_span_stripped = _BARE_QUD_SPAN.sub("", value)
    for pattern in (
        _QUD_OPENER,
        _LITERAL_AMPERSAND,
        _LITERAL_CARET,
        _LEGACY_AMPERSAND_COLOR,
        _LEGACY_CARET_COLOR,
        _HTML_COLOR_OPEN,
        _HTML_COLOR_CLOSE,
    ):
        tokens.update(match.group(0) for match in pattern.finditer(value))
    tokens.update(match.group(0) for match in _QUD_CLOSER.finditer(bare_span_stripped))
    return tokens


def _placeholder_multiset(value: str) -> Counter[str]:
    return Counter(match.group(1) for match in _PLACEHOLDER.finditer(value))


def _format_counter(counter: Counter[str]) -> str:
    return ", ".join(f"{token!r}: {count}" for token, count in sorted(counter.items()))


def _find_token_issues(entry: TranslationEntry) -> list[TranslationIssue]:
    issues: list[TranslationIssue] = []
    missing_tokens = _translation_token_multiset(entry.key) - _translation_token_multiset(entry.text)
    if missing_tokens:
        issues.append(
            TranslationIssue(
                relative_path=entry.relative_path,
                entry_index=entry.index,
                kind="TOKEN",
                detail=f"missing translation tokens: {_format_counter(missing_tokens)}",
                key=entry.key,
                text=entry.text,
            ),
        )

    source_placeholders = _placeholder_multiset(entry.key)
    translation_placeholders = _placeholder_multiset(entry.text)
    if source_placeholders != translation_placeholders:
        issues.append(
            TranslationIssue(
                relative_path=entry.relative_path,
                entry_index=entry.index,
                kind="PLACEHOLDER",
                detail=(
                    "placeholder multiset mismatch: "
                    f"source={dict(sorted(source_placeholders.items()))} "
                    f"translation={dict(sorted(translation_placeholders.items()))}"
                ),
                key=entry.key,
                text=entry.text,
            ),
        )
    return issues


def _load_duplicate_baseline(path: Path | None) -> dict[tuple[str, str], DuplicateConflictState]:
    if path is None or not path.exists():
        return {}
    payload = _load_json(path)
    if not isinstance(payload, dict):
        msg = f"Duplicate conflict baseline must be a JSON object: {path}"
        raise TypeError(msg)
    version = payload.get("version")
    if version is None:
        msg = f"Duplicate conflict baseline must contain version {_DUPLICATE_BASELINE_VERSION}: {path}"
        raise TypeError(msg)
    if version != _DUPLICATE_BASELINE_VERSION:
        msg = (
            "Duplicate conflict baseline expected "
            f"version {_DUPLICATE_BASELINE_VERSION} but found {version!r}: {path}"
        )
        raise TypeError(msg)
    conflicts = payload.get("duplicate_conflicts")
    if not isinstance(conflicts, list):
        msg = f"Duplicate conflict baseline must contain a 'duplicate_conflicts' list: {path}"
        raise TypeError(msg)

    baseline: dict[tuple[str, str], DuplicateConflictState] = {}
    for item in conflicts:
        state = _duplicate_baseline_state(item, path)
        identity = (state.path, state.key)
        if identity in baseline:
            msg = f"Duplicate conflict baseline contains duplicate entry for {identity!r}: {path}"
            raise TypeError(msg)
        baseline[identity] = state
    return baseline


def _duplicate_baseline_state(item: object, baseline_path: Path) -> DuplicateConflictState:
    if not isinstance(item, dict):
        msg = f"Invalid duplicate conflict baseline entry in {baseline_path}: {item!r}"
        raise TypeError(msg)

    relative_path = item.get("path")
    key = item.get("key")
    if not isinstance(relative_path, str) or not isinstance(key, str):
        msg = f"Invalid duplicate conflict baseline entry in {baseline_path}: {item!r}"
        raise TypeError(msg)

    texts = item.get("texts")
    entry_count = item.get("entry_count")
    if not isinstance(entry_count, int) or not isinstance(texts, list) or not all(
        isinstance(text, str) for text in texts
    ):
        msg = f"Invalid duplicate conflict baseline state in {baseline_path}: {item!r}"
        raise TypeError(msg)
    if entry_count != len(texts):
        msg = f"Duplicate conflict baseline entry_count does not match texts length in {baseline_path}: {item!r}"
        raise TypeError(msg)

    return DuplicateConflictState(
        path=relative_path,
        key=key,
        entry_count=entry_count,
        texts=tuple(sorted(texts)),
    )


def _duplicate_conflict_states(entries: list[TranslationEntry]) -> dict[tuple[str, str], DuplicateConflictState]:
    by_path_and_key: dict[tuple[str, str], list[TranslationEntry]] = defaultdict(list)
    for entry in entries:
        by_path_and_key[(entry.relative_path, entry.key)].append(entry)

    states: dict[tuple[str, str], DuplicateConflictState] = {}
    for (relative_path, key), matches in sorted(by_path_and_key.items()):
        texts = tuple(sorted(entry.text for entry in matches))
        if len(set(texts)) < _MIN_CONFLICTING_TRANSLATIONS:
            continue
        states[(relative_path, key)] = DuplicateConflictState(
            path=relative_path,
            key=key,
            entry_count=len(matches),
            texts=texts,
        )
    return states


def _format_duplicate_state(state: DuplicateConflictState | None) -> str:
    if state is None:
        return "missing current conflict"
    texts = json.dumps(list(state.texts), ensure_ascii=False)
    return f"entry_count={state.entry_count}, texts={texts}"


def _find_duplicate_conflicts(
    entries: list[TranslationEntry],
    *,
    baseline: dict[tuple[str, str], DuplicateConflictState],
    scanned_paths: set[str],
) -> list[TranslationIssue]:
    current_states = _duplicate_conflict_states(entries)
    issues: list[TranslationIssue] = []

    for identity, expected in sorted(baseline.items()):
        relative_path, key = identity
        if relative_path not in scanned_paths:
            continue
        current = current_states.get(identity)
        if current == expected:
            continue
        issues.append(
            TranslationIssue(
                relative_path=relative_path,
                entry_index=None,
                kind="BASELINE",
                detail=(
                    "duplicate conflict baseline changed: "
                    f"expected {_format_duplicate_state(expected)}; current {_format_duplicate_state(current)}"
                ),
                key=key,
            ),
        )

    for identity, state in sorted(current_states.items()):
        if identity in baseline:
            continue
        relative_path, key = identity
        issues.append(
            TranslationIssue(
                relative_path=relative_path,
                entry_index=None,
                kind="DUPLICATE",
                detail=f"duplicate source key conflict: {_format_duplicate_state(state)}",
                key=key,
            ),
        )
    return issues


def check_paths(
    paths: list[Path],
    *,
    duplicate_conflict_baseline: Path | None = None,
) -> tuple[int, list[TranslationIssue]]:
    """Check JSON localization token invariants for input paths."""
    files = collect_translation_json_files(paths)
    baseline = _load_duplicate_baseline(duplicate_conflict_baseline)
    entries: list[TranslationEntry] = []
    issues: list[TranslationIssue] = []

    for path in files:
        file_entries = iter_translation_entries(path)
        entries.extend(file_entries)
        for entry in file_entries:
            issues.extend(_find_token_issues(entry))

    scanned_paths = {_relative_asset_path(path) for path in files}
    issues.extend(_find_duplicate_conflicts(entries, baseline=baseline, scanned_paths=scanned_paths))
    return len(files), issues


def _print_report(file_count: int, issues: list[TranslationIssue]) -> None:
    print(f"Scanned {file_count} JSON localization file(s): {len(issues)} issue(s)")  # noqa: T201
    for issue in issues:
        location = issue.relative_path
        if issue.entry_index is not None:
            location = f"{location}#{issue.entry_index}"
        print(f"  [{issue.kind}] {location}: {issue.detail}")  # noqa: T201
        if issue.key is not None:
            print(f"    key={issue.key!r}")  # noqa: T201
        if issue.text is not None:
            print(f"    text={issue.text!r}")  # noqa: T201


def main(argv: list[str] | None = None) -> int:
    """Run the translation-token gate CLI."""
    parser = argparse.ArgumentParser(
        description="Validate JSON localization token preservation for dictionaries and BlueprintTemplates.",
    )
    parser.add_argument("paths", nargs="+", type=Path, help="Localization file or directory paths to scan.")
    parser.add_argument(
        "--duplicate-conflict-baseline",
        type=Path,
        default=_DEFAULT_DUPLICATE_BASELINE,
        help="JSON baseline of known same-file duplicate source-key conflicts.",
    )
    args = parser.parse_args(argv)

    try:
        file_count, issues = check_paths(
            args.paths,
            duplicate_conflict_baseline=args.duplicate_conflict_baseline,
        )
    except (FileNotFoundError, ValueError, TypeError, json.JSONDecodeError, UnicodeDecodeError) as exc:
        print(f"Error: {exc}", file=sys.stderr)  # noqa: T201
        return 1

    if file_count == 0:
        print("Error: No target *.ja.json localization files found.", file=sys.stderr)  # noqa: T201
        return 1

    _print_report(file_count, issues)
    return 1 if issues else 0


if __name__ == "__main__":
    sys.exit(main())
