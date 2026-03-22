"""Classify untranslated strings by family attribution."""

from __future__ import annotations

import re
from typing import TYPE_CHECKING

from scripts.triage.models import LogEntry, LogEntryKind, TriageClassification, TriageResult

if TYPE_CHECKING:
    from collections.abc import Callable

_TRAILING_WHITESPACE = re.compile(r"\s+$")
_LEADING_WHITESPACE = re.compile(r"^\s+(?!\s*\{\{)")
_JAPANESE_CHARS = re.compile(r"[\u3040-\u309F\u30A0-\u30FF\u4E00-\u9FFF]")
_EMBEDDED_NUMBER = re.compile(r"(?<!\.)\b-?\d+\b(?!\.)")
_STAT_LINE = re.compile(r"\d+/\d+")
_DRAM_PATTERN = re.compile(r"\d+\s+drams?\s+of\s+", re.IGNORECASE)
_LEVEL_PATTERN = re.compile(r"(?:Level|LVL|Lv):\s*-?\d+", re.IGNORECASE)
_POINTS_PATTERN = re.compile(r"(?:Points?|SP|XP|HP|MP|AV|DV|MA)(?:\s*\([^)]*\))?:\s*-?\d+", re.IGNORECASE)
_WEIGHT_PATTERN = re.compile(r"\d+#")
_DAY_NAMES = re.compile(r"\b(?:Monday|Tuesday|Wednesday|Thursday|Friday|Saturday|Sunday)\b")
_VERSION_PATTERN = re.compile(r"^\d+\.\d+\.\d+$")
_SINGLE_TOKEN_TITLE_CASE = re.compile(r"^[A-Z][a-z]+$")
_LABEL_PATTERN = re.compile(r"^[A-Za-z][A-Za-z\s&/()'\-]{0,40}$")
_MESSAGE_PATTERN_ROUTES = frozenset({"MessageLogPatch", "PopupTranslationPatch"})
_MIN_UPPERCASE_LABEL_LENGTH = 2
_KNOWN_STATIC_LABELS = frozenset(
    {
        "Abilities",
        "Attributes",
        "Character",
        "Effects",
        "Equipment",
        "Factions",
        "Inventory",
        "Journal",
        "Map",
        "Mutations",
        "Options",
        "Powers",
        "SKILLS",
        "Skills",
        "Total",
    },
)


def classify(entry: LogEntry) -> TriageResult:
    """Classify a single untranslated string observation."""
    classifiers: tuple[Callable[[LogEntry], TriageResult | None], ...] = (
        _classify_dynamic_probe,
        _classify_fragment,
        _classify_japanese_text,
        _classify_no_pattern,
        _classify_slot_text,
        _classify_version,
        _classify_static_label,
        _classify_proper_noun,
    )
    for classifier in classifiers:
        result = classifier(entry)
        if result is not None:
            return result

    return TriageResult(
        entry=entry,
        classification=TriageClassification.UNRESOLVED,
        reason="Could not determine classification — investigate upstream before adding to dictionary",
    )


def _classify_dynamic_probe(entry: LogEntry) -> TriageResult | None:
    """Classify DynamicTextProbe observations."""
    if entry.kind != LogEntryKind.DYNAMIC_TEXT_PROBE:
        return None
    family = entry.family or "<unknown-family>"
    return TriageResult(
        entry=entry,
        classification=TriageClassification.LOGIC_REQUIRED,
        reason=f"DynamicTextProbe reported family '{family}' — investigate upstream generator/template family",
        slot_evidence=[family],
    )


def _classify_fragment(entry: LogEntry) -> TriageResult | None:
    """Classify string-fragment observations based on whitespace cues."""
    if _TRAILING_WHITESPACE.search(entry.text):
        return TriageResult(
            entry=entry,
            classification=TriageClassification.LOGIC_REQUIRED,
            reason="Fragment: trailing whitespace indicates string concatenation upstream",
        )
    if _LEADING_WHITESPACE.match(entry.text):
        return TriageResult(
            entry=entry,
            classification=TriageClassification.LOGIC_REQUIRED,
            reason="Fragment: leading whitespace indicates string concatenation suffix",
        )
    return None


def _classify_japanese_text(entry: LogEntry) -> TriageResult | None:
    """Classify observations that already include Japanese text."""
    if not _JAPANESE_CHARS.search(entry.text):
        return None
    return TriageResult(
        entry=entry,
        classification=TriageClassification.UNRESOLVED,
        reason="Contains Japanese characters — likely a display-name composition artifact",
    )


def _classify_no_pattern(entry: LogEntry) -> TriageResult | None:
    """Classify MessagePatternTranslator misses conservatively."""
    if entry.kind != LogEntryKind.NO_PATTERN:
        return None
    if entry.route in _MESSAGE_PATTERN_ROUTES:
        return TriageResult(
            entry=entry,
            classification=TriageClassification.ROUTE_PATCH,
            reason=(
                "No matching pattern in MessagePatternTranslator on a known "
                "message-pattern route — add a regex pattern to messages.ja.json"
            ),
        )
    return TriageResult(
        entry=entry,
        classification=TriageClassification.UNRESOLVED,
        reason="No-pattern observation came from an unknown route — investigate before assuming a route patch",
    )


def _classify_slot_text(entry: LogEntry) -> TriageResult | None:
    """Classify observations that contain dynamic-slot evidence."""
    slot_evidence = _collect_slot_evidence(entry.text)
    if not slot_evidence:
        return None
    return TriageResult(
        entry=entry,
        classification=TriageClassification.LOGIC_REQUIRED,
        reason=f"Contains dynamic slot(s): {', '.join(slot_evidence)}",
        slot_evidence=slot_evidence,
    )


def _classify_version(entry: LogEntry) -> TriageResult | None:
    """Classify version-like strings."""
    if not _VERSION_PATTERN.fullmatch(entry.text):
        return None
    return TriageResult(
        entry=entry,
        classification=TriageClassification.UNRESOLVED,
        reason="Version string — not a translatable game text",
    )


def _classify_static_label(entry: LogEntry) -> TriageResult | None:
    """Classify stable UI labels."""
    if not _looks_like_static_label(entry.text):
        return None
    return TriageResult(
        entry=entry,
        classification=TriageClassification.STATIC_LEAF,
        reason="Stable UI label — candidate for dictionary entry",
    )


def _classify_proper_noun(entry: LogEntry) -> TriageResult | None:
    """Classify unresolved proper-noun-like single tokens."""
    if not _looks_like_proper_noun(entry.text):
        return None
    return TriageResult(
        entry=entry,
        classification=TriageClassification.UNRESOLVED,
        reason="Single token (likely procedural name or proper noun) — investigate before classifying",
    )


def _collect_slot_evidence(text: str) -> list[str]:
    """Collect dynamic-slot evidence tokens from a text observation."""
    evidence: list[str] = []

    if _DRAM_PATTERN.search(text):
        _extend_unique(evidence, _extract_numbers(text))
    if _LEVEL_PATTERN.search(text):
        _extend_unique(evidence, _extract_numbers(text))
    if _POINTS_PATTERN.search(text):
        _extend_unique(evidence, _extract_numbers(text))
    if _WEIGHT_PATTERN.search(text):
        _extend_unique(evidence, _extract_numbers(text))
    if _STAT_LINE.search(text):
        _extend_unique(evidence, _extract_numbers(text))

    day_name_match = _DAY_NAMES.search(text)
    if day_name_match is not None:
        _extend_unique(evidence, [day_name_match.group()])

    if not evidence and _EMBEDDED_NUMBER.search(text):
        _extend_unique(evidence, _extract_numbers(text))

    return evidence


def _extract_numbers(text: str) -> list[str]:
    """Extract embedded integer-like slot values from text."""
    return [match.group() for match in _EMBEDDED_NUMBER.finditer(text)]


def _extend_unique(target: list[str], values: list[str]) -> None:
    """Append values to ``target`` while preserving order and uniqueness."""
    for value in values:
        if value not in target:
            target.append(value)


def _looks_like_static_label(text: str) -> bool:
    """Return whether text looks like a stable UI label."""
    if not _LABEL_PATTERN.fullmatch(text):
        return False
    if text in _KNOWN_STATIC_LABELS:
        return True
    if text.isupper() and len(text) >= _MIN_UPPERCASE_LABEL_LENGTH:
        return True
    return any(separator in text for separator in (" ", "&", "/", "(", ")", "-", "'"))


def _looks_like_proper_noun(text: str) -> bool:
    """Return whether text looks like a single unresolved proper noun."""
    return bool(_SINGLE_TOKEN_TITLE_CASE.fullmatch(text))
