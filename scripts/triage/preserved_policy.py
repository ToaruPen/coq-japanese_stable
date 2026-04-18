"""Classify intentionally preserved English text from a route-aware policy."""

from __future__ import annotations

import fnmatch
import json
import re
from dataclasses import dataclass
from functools import lru_cache
from pathlib import Path
from typing import Any

from scripts.triage.models import LogEntry, LogEntryKind, TriageClassification, TriageResult

_POLICY_PATH = Path(__file__).with_name("preserved_english_policy.json")


@dataclass(frozen=True)
class PreservedEnglishRule:
    """One route-aware preserved-English policy rule."""

    id: str
    category: str
    reason: str
    routes: tuple[str, ...]
    texts: frozenset[str]
    patterns: tuple[str, ...]
    protected_tokens: tuple[str, ...]
    dictionary_contexts: tuple[str, ...]

    @classmethod
    def from_json(cls, payload: dict[str, Any]) -> PreservedEnglishRule:
        """Create a rule from the machine-readable policy payload."""
        return cls(
            id=str(payload["id"]),
            category=str(payload["category"]),
            reason=str(payload["reason"]),
            routes=tuple(str(route) for route in payload.get("routes", ())),
            texts=frozenset(str(text) for text in payload.get("texts", ())),
            patterns=tuple(str(pattern) for pattern in payload.get("patterns", ())),
            protected_tokens=tuple(str(token) for token in payload.get("protected_tokens", ())),
            dictionary_contexts=tuple(str(context) for context in payload.get("dictionary_contexts", ())),
        )

    def matches_route(self, route: str) -> bool:
        """Return whether this rule applies to ``route``."""
        return not self.routes or any(fnmatch.fnmatchcase(route, route_pattern) for route_pattern in self.routes)

    def matches_text(self, text: str) -> bool:
        """Return whether this rule applies to ``text``."""
        return text in self.texts or any(re.search(pattern, text) for pattern in self.patterns)

    def protected_tokens_in(self, text: str) -> list[str]:
        """Return protected tokens from this rule that appear in ``text``."""
        return [token for token in self.protected_tokens if _contains_token(text, token)]


@dataclass(frozen=True)
class PreservedEnglishPolicy:
    """Machine-readable preserved-English policy."""

    version: int
    rules: tuple[PreservedEnglishRule, ...]

    @classmethod
    def from_json(cls, payload: dict[str, Any]) -> PreservedEnglishPolicy:
        """Create a policy from a decoded JSON payload."""
        return cls(
            version=int(payload["version"]),
            rules=tuple(PreservedEnglishRule.from_json(rule) for rule in payload.get("rules", ())),
        )


@lru_cache(maxsize=1)
def load_preserved_english_policy() -> PreservedEnglishPolicy:
    """Load the route-aware preserved-English policy."""
    return PreservedEnglishPolicy.from_json(json.loads(_POLICY_PATH.read_text(encoding="utf-8")))


def classify_preserved_english(entry: LogEntry) -> TriageResult | None:
    """Classify a log entry using the preserved-English policy."""
    for rule in load_preserved_english_policy().rules:
        if rule.category == "numeric_or_glyph_only_text":
            continue
        if not rule.matches_route(entry.route) or not rule.matches_text(entry.text):
            continue

        unexpected_tokens = _unexpectedly_translated_tokens(entry, rule)
        if unexpected_tokens:
            return TriageResult(
                entry=entry,
                classification=TriageClassification.UNEXPECTED_TRANSLATION_OF_PRESERVED_TOKEN,
                reason=_format_reason(rule, "Protected English token was translated unexpectedly"),
                slot_evidence=unexpected_tokens,
            )

        return TriageResult(
            entry=entry,
            classification=TriageClassification.PRESERVED_ENGLISH,
            reason=_format_reason(rule, "Allowed by preserved-English policy"),
            slot_evidence=rule.protected_tokens_in(entry.text) or [rule.id],
        )

    return None


def _unexpectedly_translated_tokens(entry: LogEntry, rule: PreservedEnglishRule) -> list[str]:
    """Return protected tokens that disappeared from a translated runtime probe."""
    if rule.category != "must_remain_english":
        return []
    if entry.kind != LogEntryKind.DYNAMIC_TEXT_PROBE or not entry.changed or entry.translated_text is None:
        return []

    source_tokens = rule.protected_tokens_in(entry.text)
    if not source_tokens:
        return []
    return [token for token in source_tokens if not _contains_token(entry.translated_text or "", token)]


def _format_reason(rule: PreservedEnglishRule, prefix: str) -> str:
    """Build a stable human-readable reason."""
    return f"{prefix}: {rule.id} ({rule.category}) - {rule.reason}"


def _contains_token(text: str, token: str) -> bool:
    """Return whether ``token`` appears as a standalone ASCII token in ``text``."""
    escaped = re.escape(token)
    return re.search(rf"(?<![A-Za-z0-9]){escaped}(?![A-Za-z0-9])", text) is not None
