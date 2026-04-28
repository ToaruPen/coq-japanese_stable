"""Validate JSON localization entries for source-side translation token preservation."""

from __future__ import annotations

import argparse
import json
import re
import sys
from collections import Counter, defaultdict
from dataclasses import dataclass
from functools import cache
from pathlib import Path
from typing import Final

type TextRange = tuple[int, int]
type TextRangeCandidates = tuple[TextRange, ...]
type ExactRangeCandidateSequence = tuple[TextRangeCandidates, ...]
type VariableTokenCandidateSequence = tuple[tuple[str, TextRangeCandidates], ...]

_DEFAULT_DUPLICATE_BASELINE = Path(__file__).with_name("translation_token_duplicate_baseline.json")
_DUPLICATE_BASELINE_VERSION = 3
_MIN_CONFLICTING_TRANSLATIONS = 2
_SAME_FILE_DUPLICATE_SCOPE = "same_file"
_CROSS_FILE_DUPLICATE_SCOPE = "cross_file"

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
_VARIABLE_TOKEN = re.compile(r"=[A-Za-z_][A-Za-z0-9_.:']*=")
_LOCALIZABLE_VERB_TOKEN = re.compile(r"=(?:[A-Za-z_][A-Za-z0-9_]*\.)?verb:[A-Za-z0-9_:']+=")
_VARIABLE_TOKEN_EQUIVALENTS = {
    "=object.T=": ("=object.name=",),
    "=object.Does:are=": ("=object.name=", "=object.t=", "=object.T="),
    "=object.an=": ("=object.name=",),
    "=object.t=": ("=object.name=",),
    "=subject.Name's=": ("=subject.name=の", "=subject.Name=の"),
    "=subject.T=": ("=subject.name=",),
    "=subject.T's=": ("=subject.name=の", "=subject.T=の"),
    "=subject.t=": ("=subject.name=",),
    "=subject.t's=": ("=subject.name=の", "=subject.t=の"),
    "=pronouns.Subjective=": ("=subject.name=",),
    "=pronouns.objective=": ("=subject.name=",),
    "=pronouns.possessive=": ("=subject.name=",),
}
_VARIABLE_EQUIVALENT_TOKENS = frozenset(
    equivalent for token_equivalents in _VARIABLE_TOKEN_EQUIVALENTS.values() for equivalent in token_equivalents
)
_OMITTABLE_VARIABLE_TOKENS = {
    "=objpronouns.reflexive=",
    "=subject.possessive=",
}
_ARTICLE_ONLY_VARIABLE_TOKENS = {
    "=subject.The=",
    "=subject.the=",
}
# This marks tokens that can be dropped any number of times by design.
UNLIMITED_ALLOWED_MISSING_TOKENS: Final[int] = sys.maxsize


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
class DuplicateOccurrence:
    """One source-key occurrence in a duplicate conflict state."""

    path: str
    entry_index: int
    text: str


@dataclass(frozen=True)
class DuplicateConflictState:
    """Expected or observed duplicate source-key conflict state."""

    scope: str
    path: str
    key: str
    entry_count: int
    texts: tuple[str, ...]
    occurrences: tuple[DuplicateOccurrence, ...]


def _path_candidates(path: Path) -> tuple[Path, ...]:
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
    try:
        payload: object = json.loads(path.read_text(encoding="utf-8"))
    except (json.JSONDecodeError, UnicodeDecodeError, OSError) as exc:
        msg = f"Failed to load JSON from {path}: {exc}"
        raise ValueError(msg) from exc
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
        _VARIABLE_TOKEN,
    ):
        tokens.update(match.group(0) for match in pattern.finditer(value))
    tokens.update(match.group(0) for match in _QUD_CLOSER.finditer(bare_span_stripped))
    return tokens


def _placeholder_multiset(value: str) -> Counter[str]:
    return Counter(match.group(1) for match in _PLACEHOLDER.finditer(value))


def _format_counter(counter: Counter[str]) -> str:
    return ", ".join(f"{token!r}: {count}" for token, count in sorted(counter.items()))


def _bare_span_opener_equivalent(token: str) -> str | None:
    if not _BARE_QUD_SPAN.fullmatch(token):
        return None
    return f"{token[:-2]}|"


def _missing_translation_tokens(source: str, translation: str) -> Counter[str]:
    source_tokens = _translation_token_multiset(source)
    translation_tokens = _translation_token_multiset(translation)
    missing_tokens = source_tokens - translation_tokens
    for token, count in list(missing_tokens.items()):
        opener = _bare_span_opener_equivalent(token)
        if opener is None:
            continue
        available_openers = translation_tokens[opener] - source_tokens[opener]
        covered_count = min(count, max(available_openers, 0))
        if covered_count == 0:
            continue
        _consume_missing_token_count(missing_tokens, token, covered_count)
    _consume_allowed_missing_variable_tokens(missing_tokens, source_tokens, translation)
    return missing_tokens


def _consume_allowed_missing_variable_tokens(
    missing_tokens: Counter[str],
    source_tokens: Counter[str],
    translation: str,
) -> None:
    available_equivalents = _available_variable_equivalent_token_ranges(translation)
    for token in list(missing_tokens):
        non_consuming_count = _allowed_missing_non_consuming_variable_token_count(token)
        if non_consuming_count:
            _consume_missing_token_count(missing_tokens, token, non_consuming_count)

    exact_token_candidates = _preserved_exact_variable_token_candidate_ranges(source_tokens, translation)
    for token, count in _matched_variable_equivalent_token_counts(
        missing_tokens,
        available_equivalents,
        exact_token_candidates,
    ).items():
        _consume_missing_token_count(missing_tokens, token, count)


def _consume_missing_token_count(missing_tokens: Counter[str], token: str, count: int) -> None:
    consumed_count = min(missing_tokens[token], count)
    if consumed_count <= 0:
        return

    missing_tokens[token] -= consumed_count
    if missing_tokens[token] <= 0:
        del missing_tokens[token]


def _allowed_missing_non_consuming_variable_token_count(token: str) -> int:
    if not _VARIABLE_TOKEN.fullmatch(token):
        return 0
    if _LOCALIZABLE_VERB_TOKEN.fullmatch(token):
        return UNLIMITED_ALLOWED_MISSING_TOKENS
    if token in _OMITTABLE_VARIABLE_TOKENS or token in _ARTICLE_ONLY_VARIABLE_TOKENS:
        return UNLIMITED_ALLOWED_MISSING_TOKENS
    return 0


def _available_variable_equivalent_token_ranges(translation: str) -> dict[str, list[TextRange]]:
    return {
        equivalent: [(match.start(), match.end()) for match in re.finditer(re.escape(equivalent), translation)]
        for equivalent in _VARIABLE_EQUIVALENT_TOKENS
    }


def _preserved_exact_variable_token_candidate_ranges(
    source_tokens: Counter[str],
    translation: str,
) -> list[TextRangeCandidates]:
    translation_ranges = _variable_token_ranges_by_token(translation)
    exact_token_candidates: list[TextRangeCandidates] = []
    for token, source_count in source_tokens.items():
        if not _VARIABLE_TOKEN.fullmatch(token):
            continue
        ranges = translation_ranges.get(token, ())
        preserved_count = min(source_count, len(ranges))
        exact_token_candidates.extend(ranges for _ in range(preserved_count))
    exact_token_candidates.sort(key=lambda candidates: (len(candidates), candidates))
    return exact_token_candidates


def _variable_token_ranges_by_token(translation: str) -> dict[str, TextRangeCandidates]:
    ranges_by_token: dict[str, list[TextRange]] = defaultdict(list)
    for match in _VARIABLE_TOKEN.finditer(translation):
        ranges_by_token[match.group(0)].append((match.start(), match.end()))
    return {token: tuple(ranges) for token, ranges in ranges_by_token.items()}


def _matched_variable_equivalent_token_counts(
    missing_tokens: Counter[str],
    available_equivalents: dict[str, list[TextRange]],
    exact_token_candidates: list[TextRangeCandidates],
) -> Counter[str]:
    token_candidates = [
        (token, _variable_equivalent_candidate_ranges(token, available_equivalents))
        for token, count in missing_tokens.items()
        for _ in range(count)
        if _VARIABLE_TOKEN.fullmatch(token) and token in _VARIABLE_TOKEN_EQUIVALENTS
    ]
    token_candidates = [(token, candidates) for token, candidates in token_candidates if candidates]
    token_candidates.sort(key=lambda item: (len(item[1]), item[0]))

    consumed_tokens = _best_variable_equivalent_assignment(exact_token_candidates, token_candidates)
    return Counter(consumed_tokens)


def _variable_equivalent_candidate_ranges(
    token: str,
    available_equivalents: dict[str, list[TextRange]],
) -> TextRangeCandidates:
    candidates = []
    for equivalent in _VARIABLE_TOKEN_EQUIVALENTS[token]:
        candidates.extend(available_equivalents[equivalent])
    return tuple(sorted(set(candidates), key=lambda item: (item[0], item[1])))


def _best_variable_equivalent_assignment(
    exact_token_candidates: list[TextRangeCandidates],
    token_candidates: list[tuple[str, TextRangeCandidates]],
) -> tuple[str, ...]:
    exact_candidate_sequence = tuple(exact_token_candidates)
    token_candidate_sequence = tuple(token_candidates)
    try:
        return _best_variable_equivalent_assignment_after_exact(
            exact_candidate_sequence,
            token_candidate_sequence,
            0,
            (),
        )
    finally:
        _best_variable_equivalent_assignment_after_exact.cache_clear()
        _best_optional_variable_equivalent_assignment.cache_clear()


@cache
def _best_optional_variable_equivalent_assignment(
    token_candidate_sequence: VariableTokenCandidateSequence,
    candidate_index: int,
    used_ranges: TextRangeCandidates,
) -> tuple[str, ...]:
    if candidate_index >= len(token_candidate_sequence):
        return ()

    best_consumed_tokens = _best_optional_variable_equivalent_assignment(
        token_candidate_sequence,
        candidate_index + 1,
        used_ranges,
    )
    token, candidates = token_candidate_sequence[candidate_index]
    remaining_token_count = len(token_candidate_sequence) - candidate_index
    for candidate_range in candidates:
        if any(_ranges_overlap(candidate_range, used_range) for used_range in used_ranges):
            continue
        next_used_ranges = tuple(sorted((*used_ranges, candidate_range)))
        consumed_tokens = (
            token,
            *_best_optional_variable_equivalent_assignment(
                token_candidate_sequence,
                candidate_index + 1,
                next_used_ranges,
            ),
        )
        if len(consumed_tokens) > len(best_consumed_tokens):
            best_consumed_tokens = consumed_tokens
            if len(best_consumed_tokens) == remaining_token_count:
                break

    return best_consumed_tokens


@cache
def _best_variable_equivalent_assignment_after_exact(
    exact_candidate_sequence: ExactRangeCandidateSequence,
    token_candidate_sequence: VariableTokenCandidateSequence,
    candidate_index: int,
    used_ranges: TextRangeCandidates,
) -> tuple[str, ...]:
    if candidate_index >= len(exact_candidate_sequence):
        return _best_optional_variable_equivalent_assignment(token_candidate_sequence, 0, used_ranges)

    best_consumed_tokens: tuple[str, ...] = ()
    candidates = exact_candidate_sequence[candidate_index]
    for candidate_range in candidates:
        if any(_ranges_overlap(candidate_range, used_range) for used_range in used_ranges):
            continue
        next_used_ranges = tuple(sorted((*used_ranges, candidate_range)))
        consumed_tokens = _best_variable_equivalent_assignment_after_exact(
            exact_candidate_sequence,
            token_candidate_sequence,
            candidate_index + 1,
            next_used_ranges,
        )
        if len(consumed_tokens) > len(best_consumed_tokens):
            best_consumed_tokens = consumed_tokens
            if len(best_consumed_tokens) == len(token_candidate_sequence):
                break

    return best_consumed_tokens


def _ranges_overlap(left: TextRange, right: TextRange) -> bool:
    return left[0] < right[1] and right[0] < left[1]


def _find_token_issues(entry: TranslationEntry) -> list[TranslationIssue]:
    issues: list[TranslationIssue] = []
    missing_tokens = _missing_translation_tokens(entry.key, entry.text)
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


def _load_duplicate_baseline(path: Path | None) -> dict[tuple[str, str, str], DuplicateConflictState]:
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
            f"Duplicate conflict baseline expected version {_DUPLICATE_BASELINE_VERSION} but found {version!r}: {path}"
        )
        raise TypeError(msg)
    conflicts = payload.get("duplicate_conflicts")
    if not isinstance(conflicts, list):
        msg = f"Duplicate conflict baseline must contain a 'duplicate_conflicts' list: {path}"
        raise TypeError(msg)

    baseline: dict[tuple[str, str, str], DuplicateConflictState] = {}
    for item in conflicts:
        state = _duplicate_baseline_state(item, path)
        identity = _duplicate_identity(state)
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
    scope = item.get("scope")
    if scope not in {_SAME_FILE_DUPLICATE_SCOPE, _CROSS_FILE_DUPLICATE_SCOPE}:
        msg = f"Invalid duplicate conflict baseline entry in {baseline_path}: {item!r}"
        raise TypeError(msg)
    if not isinstance(relative_path, str) or not isinstance(key, str):
        msg = f"Invalid duplicate conflict baseline entry in {baseline_path}: {item!r}"
        raise TypeError(msg)

    texts = item.get("texts")
    entry_count = item.get("entry_count")
    occurrences = item.get("occurrences")
    if (
        not isinstance(entry_count, int)
        or not isinstance(texts, list)
        or not all(isinstance(text, str) for text in texts)
    ):
        msg = f"Invalid duplicate conflict baseline state in {baseline_path}: {item!r}"
        raise TypeError(msg)
    if entry_count != len(texts):
        msg = f"Duplicate conflict baseline entry_count does not match texts length in {baseline_path}: {item!r}"
        raise TypeError(msg)
    if not isinstance(occurrences, list):
        msg = f"Duplicate conflict baseline must contain occurrence details in {baseline_path}: {item!r}"
        raise TypeError(msg)
    parsed_occurrences = tuple(_duplicate_occurrence_state(occurrence, baseline_path) for occurrence in occurrences)
    if entry_count != len(parsed_occurrences):
        msg = f"Duplicate conflict baseline entry_count does not match occurrences length in {baseline_path}: {item!r}"
        raise TypeError(msg)
    occurrence_texts = sorted(occurrence.text for occurrence in parsed_occurrences)
    if sorted(texts) != occurrence_texts:
        msg = f"Duplicate conflict baseline texts do not match occurrences in {baseline_path}: {item!r}"
        raise TypeError(msg)

    return DuplicateConflictState(
        scope=scope,
        path=relative_path,
        key=key,
        entry_count=entry_count,
        texts=tuple(sorted(texts)),
        occurrences=tuple(sorted(parsed_occurrences, key=lambda occurrence: (occurrence.path, occurrence.entry_index))),
    )


def _duplicate_occurrence_state(item: object, baseline_path: Path) -> DuplicateOccurrence:
    if not isinstance(item, dict):
        msg = f"Invalid duplicate conflict baseline occurrence in {baseline_path}: {item!r}"
        raise TypeError(msg)

    relative_path = item.get("path")
    entry_index = item.get("entry_index")
    text = item.get("text")
    if not isinstance(relative_path, str) or not isinstance(entry_index, int) or not isinstance(text, str):
        msg = f"Invalid duplicate conflict baseline occurrence in {baseline_path}: {item!r}"
        raise TypeError(msg)
    return DuplicateOccurrence(path=relative_path, entry_index=entry_index, text=text)


def _duplicate_identity(state: DuplicateConflictState) -> tuple[str, str, str]:
    return (state.scope, state.path, state.key)


def _duplicate_occurrences(entries: list[TranslationEntry]) -> tuple[DuplicateOccurrence, ...]:
    return tuple(
        sorted(
            (
                DuplicateOccurrence(path=entry.relative_path, entry_index=entry.index, text=entry.text)
                for entry in entries
            ),
            key=lambda occurrence: (occurrence.path, occurrence.entry_index, occurrence.text),
        )
    )


def _duplicate_state(
    *,
    scope: str,
    path: str,
    key: str,
    entries: list[TranslationEntry],
) -> DuplicateConflictState:
    occurrences = _duplicate_occurrences(entries)
    return DuplicateConflictState(
        scope=scope,
        path=path,
        key=key,
        entry_count=len(occurrences),
        texts=tuple(sorted(occurrence.text for occurrence in occurrences)),
        occurrences=occurrences,
    )


def _duplicate_conflict_states(entries: list[TranslationEntry]) -> dict[tuple[str, str, str], DuplicateConflictState]:
    by_path_and_key: dict[tuple[str, str], list[TranslationEntry]] = defaultdict(list)
    dictionary_entries_by_key: dict[str, list[TranslationEntry]] = defaultdict(list)
    for entry in entries:
        by_path_and_key[(entry.relative_path, entry.key)].append(entry)
        if entry.relative_path.startswith("Dictionaries/"):
            dictionary_entries_by_key[entry.key].append(entry)

    states: dict[tuple[str, str, str], DuplicateConflictState] = {}
    for (relative_path, key), matches in sorted(by_path_and_key.items()):
        if len(matches) < _MIN_CONFLICTING_TRANSLATIONS:
            continue
        state = _duplicate_state(
            scope=_SAME_FILE_DUPLICATE_SCOPE,
            path=relative_path,
            key=key,
            entries=matches,
        )

        states[_duplicate_identity(state)] = state

    for key, matches in sorted(dictionary_entries_by_key.items()):
        paths = {entry.relative_path for entry in matches}
        texts = {entry.text for entry in matches}
        if len(paths) < _MIN_CONFLICTING_TRANSLATIONS or len(texts) < _MIN_CONFLICTING_TRANSLATIONS:
            continue
        state = _duplicate_state(
            scope=_CROSS_FILE_DUPLICATE_SCOPE,
            path="Dictionaries",
            key=key,
            entries=matches,
        )
        states[_duplicate_identity(state)] = state
    return states


def _format_duplicate_state(state: DuplicateConflictState | None) -> str:
    if state is None:
        return "missing current conflict"
    texts = json.dumps(list(state.texts), ensure_ascii=False)
    occurrences = json.dumps(
        [
            {"path": occurrence.path, "entry_index": occurrence.entry_index, "text": occurrence.text}
            for occurrence in state.occurrences
        ],
        ensure_ascii=False,
    )
    return f"scope={state.scope}, entry_count={state.entry_count}, texts={texts}, occurrences={occurrences}"


def _baseline_state_applies(state: DuplicateConflictState, scanned_paths: set[str]) -> bool:
    if state.scope == _SAME_FILE_DUPLICATE_SCOPE:
        return state.path in scanned_paths
    return {occurrence.path for occurrence in state.occurrences}.issubset(scanned_paths)


def _find_duplicate_conflicts(
    entries: list[TranslationEntry],
    *,
    baseline: dict[tuple[str, str, str], DuplicateConflictState],
    scanned_paths: set[str],
) -> list[TranslationIssue]:
    current_states = _duplicate_conflict_states(entries)
    issues: list[TranslationIssue] = []

    for identity, expected in sorted(baseline.items()):
        if not _baseline_state_applies(expected, scanned_paths):
            continue
        current = current_states.get(identity)
        if current == expected:
            continue
        issues.append(
            TranslationIssue(
                relative_path=expected.path,
                entry_index=None,
                kind="BASELINE",
                detail=(
                    "duplicate conflict baseline changed: "
                    f"expected {_format_duplicate_state(expected)}; current {_format_duplicate_state(current)}"
                ),
                key=expected.key,
            ),
        )

    for identity, state in sorted(current_states.items()):
        if identity in baseline:
            continue
        issues.append(
            TranslationIssue(
                relative_path=state.path,
                entry_index=None,
                kind="DUPLICATE",
                detail=f"duplicate source key conflict: {_format_duplicate_state(state)}",
                key=state.key,
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
