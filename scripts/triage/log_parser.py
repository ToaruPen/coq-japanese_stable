"""Parse Player.log for untranslated string observations."""

from __future__ import annotations

import re
from typing import TYPE_CHECKING

from scripts.triage.models import LogEntry, LogEntryKind

if TYPE_CHECKING:
    from pathlib import Path

_MISSING_KEY_PATTERN = re.compile(
    r"\[QudJP\] Translator: missing key '(?P<key>.*?)' \(hit (?P<hits>\d+)\)\."
    r"(?: \(context: (?P<context>[^)]+)\))?",
)
_NO_PATTERN_PATTERN = re.compile(
    r"\[QudJP\] MessagePatternTranslator: no pattern for '(?P<source>.*?)' \(hit (?P<hits>\d+)\)\."
    r"(?: \(context: (?P<context>[^)]+)\))?",
)
_DYNAMIC_TEXT_PROBE_PATTERN = re.compile(
    r"\[QudJP\] DynamicTextProbe/v\d+: route='(?P<route>.*?)' family='(?P<family>.*?)' "
    r"hit=(?P<hits>\d+) changed=(?P<changed>true|false) source='(?P<source>.*?)' "
    r"translated='(?P<translated>.*?)'\.(?: \(context: (?P<context>[^)]+)\))?",
)
_SINK_OBSERVE_PATTERN = re.compile(
    r"\[QudJP\] SinkObserve/v\d+: sink='(?P<sink>.*?)' route='(?P<route>.*?)' "
    r"detail='(?P<detail>.*?)' source='(?P<source>.*?)' stripped='(?P<stripped>.*?)'",
)
_FINAL_OUTPUT_PROBE_PATTERN = re.compile(
    r"\[QudJP\] FinalOutputProbe/v\d+: sink='(?P<sink>(?:\\'|[^'])*)' "
    r"route='(?P<route>(?:\\'|[^'])*)' detail='(?P<detail>(?:\\'|[^'])*)' "
    r"phase='(?P<phase>(?:\\'|[^'])*)' "
    r"translation_status='(?P<translation_status>(?:\\'|[^'])*)' "
    r"markup_status='(?P<markup_status>(?:\\'|[^'])*)' "
    r"direct_marker_status='(?P<direct_marker_status>(?:\\'|[^'])*)' hit=(?P<hits>\d+) "
    r"source='(?P<source>(?:\\'|[^'])*)' stripped='(?P<stripped>(?:\\'|[^'])*)' "
    r"translated='(?P<translated>(?:\\'|[^'])*)' final='(?P<final>(?:\\'|[^'])*)'",
)
_STRUCTURED_SUFFIX_TOKEN = re.compile(
    r"(?P<key>[a-z_][a-z0-9_]*)=(?P<value>[^\n]*?)(?=; [a-z_][a-z0-9_]*=|$)",
)
_NULLABLE_STRUCTURED_FIELDS = frozenset({"template_id", "payload_sha256"})


def _extract_primary_context(context: str | None) -> str:
    """Extract the primary route name before the nested-context separator."""
    if not context:
        return "<no-context>"
    return context.split(" > ", maxsplit=1)[0]


def _unescape_probe_value(value: str) -> str:
    """Normalize escaped values recorded in quoted probe prefixes."""
    return re.sub(
        r"\\u([0-9A-Fa-f]{4})",
        lambda match: chr(int(match.group(1), 16)),
        value.replace(r"\'", "'").replace(r"\n", "\n").replace(r"\r", "\r").replace(r"\t", "\t"),
    )


def _dedupe_key(entry: LogEntry) -> tuple[str, ...]:
    """Build the deduplication key for a parsed log entry."""
    if entry.kind == LogEntryKind.FINAL_OUTPUT_PROBE:
        return (
            entry.kind.value,
            entry.route,
            entry.text,
            entry.family or "",
            entry.payload_excerpt or "",
            entry.payload_sha256 or "",
            entry.sink or "",
            entry.detail or "",
            entry.phase or "",
            entry.source_text_sample or "",
            entry.stripped_text_sample or "",
            entry.translated_text_sample or "",
            entry.final_text_sample or "",
            entry.source_markup_spans or "",
            entry.final_markup_spans or "",
            entry.markup_span_status or "",
            entry.source_visible_sha256 or "",
            entry.final_visible_sha256 or "",
            entry.translation_status or "",
            entry.markup_status or "",
            entry.direct_marker_status or "",
        )

    family_key = entry.family or ""
    payload_excerpt_key = ""
    payload_sha256_key = ""
    if entry.kind in {LogEntryKind.MISSING_KEY, LogEntryKind.NO_PATTERN, LogEntryKind.SINK_OBSERVE}:
        family_key = ""
    if entry.kind in {LogEntryKind.DYNAMIC_TEXT_PROBE, LogEntryKind.SINK_OBSERVE}:
        payload_excerpt_key = entry.payload_excerpt or ""
        payload_sha256_key = entry.payload_sha256 or ""
    return (entry.kind.value, entry.route, entry.text, family_key, payload_excerpt_key, payload_sha256_key)


def _hits_value(hits: int | None) -> int:
    """Normalize hit counts for deduplication comparisons."""
    return -1 if hits is None else hits


def _parse_structured_suffix(line: str, prefix_end: int) -> tuple[dict[str, str | None], frozenset[str]]:
    """Parse the deterministic `; key=value` suffix added by Phase F emitters."""
    # Phase F suffix parsing assumes parse_log() has already split Player.log into
    # one log entry per line, so structured values never span newlines here.
    suffix = line[prefix_end:]
    if not suffix.startswith("; "):
        return {}, frozenset()

    parsed: dict[str, str | None] = {}
    present_fields: set[str] = set()
    for match in _STRUCTURED_SUFFIX_TOKEN.finditer(suffix[2:]):
        key = match.group("key")
        raw_value = match.group("value")
        present_fields.add(key)
        parsed[key] = (
            None
            if raw_value == "<missing>" and key in _NULLABLE_STRUCTURED_FIELDS
            else _unescape_structured_value(raw_value)
        )
    return parsed, frozenset(present_fields)


def _unescape_structured_value(value: str) -> str:
    """Decode structured-value escaping without touching existing control escapes."""
    builder: list[str] = []
    index = 0
    while index < len(value):
        character = value[index]
        if character == "\\" and index + 1 < len(value) and value[index + 1] in {"\\", ";", "="}:
            builder.append(value[index + 1])
            index += 2
            continue

        builder.append(character)
        index += 1

    return "".join(builder)


def _structured_value(
    parsed_fields: dict[str, str | None],
    present_fields: frozenset[str],
    field_name: str,
    default: str | None = None,
) -> str | None:
    """Return a structured suffix value when present, otherwise a default."""
    if field_name in present_fields:
        return parsed_fields.get(field_name)
    return default


def _entry_with_structured_fields(
    entry: LogEntry,
    parsed_fields: dict[str, str | None],
    present_fields: frozenset[str],
) -> LogEntry:
    """Merge Phase F structured suffix data into a parsed log entry."""

    def structured(field_name: str, default: str | None = None) -> str | None:
        return _structured_value(parsed_fields, present_fields, field_name, default)

    return LogEntry(
        kind=entry.kind,
        route=structured("route", entry.route) or entry.route,
        text=entry.text,
        hits=entry.hits,
        line_number=entry.line_number,
        family=structured("family", entry.family),
        translated_text=entry.translated_text,
        changed=entry.changed,
        template_id=structured("template_id"),
        rendered_text_sample=structured("rendered_text_sample"),
        payload_mode=structured("payload_mode"),
        payload_excerpt=structured("payload_excerpt"),
        payload_sha256=structured("payload_sha256"),
        sink=structured("sink", entry.sink),
        detail=structured("detail", entry.detail),
        phase=structured("phase", entry.phase),
        translation_status=structured("translation_status", entry.translation_status),
        markup_status=structured("markup_status", entry.markup_status),
        direct_marker_status=structured("direct_marker_status", entry.direct_marker_status),
        source_text_sample=structured("source_text_sample", entry.source_text_sample),
        stripped_text_sample=structured("stripped_text_sample", entry.stripped_text_sample),
        translated_text_sample=structured("translated_text_sample", entry.translated_text_sample),
        final_text_sample=structured("final_text_sample", entry.final_text_sample),
        source_markup_spans=structured("source_markup_spans", entry.source_markup_spans),
        final_markup_spans=structured("final_markup_spans", entry.final_markup_spans),
        markup_span_status=structured("markup_span_status", entry.markup_span_status),
        source_visible_sha256=structured("source_visible_sha256", entry.source_visible_sha256),
        final_visible_sha256=structured("final_visible_sha256", entry.final_visible_sha256),
        structured_fields=present_fields,
    )


def parse_log(log_path: Path) -> list[LogEntry]:
    """Parse a Player.log file into deduplicated ``LogEntry`` records.

    Same kind+route+text(+family) at multiple hit counts becomes a single entry with
    the maximum hit count.

    Args:
        log_path: Path to ``Player.log``.

    Returns:
        Deduplicated entries sorted by line number, route, and text.
    """
    if not log_path.exists():
        return []

    return parse_log_text(log_path.read_text(encoding="utf-8", errors="replace"))


def parse_log_text(text: str) -> list[LogEntry]:
    """Parse Player.log text into deduplicated ``LogEntry`` records.

    Args:
        text: Log text to parse.

    Returns:
        Deduplicated entries sorted by line number, route, and text.
    """
    seen: dict[tuple[str, ...], LogEntry] = {}
    for line_number, line in enumerate(text.splitlines(), start=1):
        entry = _try_parse_line(line, line_number)
        if entry is None:
            continue
        key = _dedupe_key(entry)
        existing = seen.get(key)
        if existing is None or _hits_value(entry.hits) > _hits_value(existing.hits):
            seen[key] = entry

    return sorted(seen.values(), key=lambda entry: (entry.line_number, entry.route, entry.text))


def _try_parse_line(line: str, line_number: int) -> LogEntry | None:
    """Try to parse a single log line into a ``LogEntry``."""
    missing_key_match = _MISSING_KEY_PATTERN.search(line)
    if missing_key_match:
        entry = LogEntry(
            kind=LogEntryKind.MISSING_KEY,
            route=_extract_primary_context(missing_key_match.group("context")),
            text=missing_key_match.group("key"),
            hits=int(missing_key_match.group("hits")),
            line_number=line_number,
        )
        parsed_fields, present_fields = _parse_structured_suffix(line, missing_key_match.end())
        return _entry_with_structured_fields(entry, parsed_fields, present_fields)

    no_pattern_match = _NO_PATTERN_PATTERN.search(line)
    if no_pattern_match:
        entry = LogEntry(
            kind=LogEntryKind.NO_PATTERN,
            route=_extract_primary_context(no_pattern_match.group("context")),
            text=no_pattern_match.group("source"),
            hits=int(no_pattern_match.group("hits")),
            line_number=line_number,
        )
        parsed_fields, present_fields = _parse_structured_suffix(line, no_pattern_match.end())
        return _entry_with_structured_fields(entry, parsed_fields, present_fields)

    dynamic_probe_match = _DYNAMIC_TEXT_PROBE_PATTERN.search(line)
    if dynamic_probe_match:
        context = dynamic_probe_match.group("context")
        route = dynamic_probe_match.group("route")
        entry = LogEntry(
            kind=LogEntryKind.DYNAMIC_TEXT_PROBE,
            route=_extract_primary_context(context or route),
            text=_unescape_probe_value(dynamic_probe_match.group("source")),
            hits=int(dynamic_probe_match.group("hits")),
            line_number=line_number,
            family=dynamic_probe_match.group("family"),
            translated_text=_unescape_probe_value(dynamic_probe_match.group("translated")),
            changed=dynamic_probe_match.group("changed") == "true",
        )
        parsed_fields, present_fields = _parse_structured_suffix(line, dynamic_probe_match.end())
        return _entry_with_structured_fields(entry, parsed_fields, present_fields)

    sink_observe_match = _SINK_OBSERVE_PATTERN.search(line)
    if sink_observe_match:
        entry = LogEntry(
            kind=LogEntryKind.SINK_OBSERVE,
            route=sink_observe_match.group("route"),
            text=_unescape_probe_value(sink_observe_match.group("source")),
            hits=None,
            line_number=line_number,
            sink=sink_observe_match.group("sink"),
            detail=sink_observe_match.group("detail"),
            stripped_text_sample=_unescape_probe_value(sink_observe_match.group("stripped")),
        )
        parsed_fields, present_fields = _parse_structured_suffix(line, sink_observe_match.end())
        return _entry_with_structured_fields(entry, parsed_fields, present_fields)

    final_output_match = _FINAL_OUTPUT_PROBE_PATTERN.search(line)
    if final_output_match:
        group = final_output_match.group
        unescaped = _unescape_probe_value
        entry = LogEntry(
            kind=LogEntryKind.FINAL_OUTPUT_PROBE,
            route=group("route"),
            text=unescaped(group("source")),
            hits=int(group("hits")),
            line_number=line_number,
            sink=group("sink"),
            detail=group("detail"),
            phase=group("phase"),
            translation_status=group("translation_status"),
            markup_status=group("markup_status"),
            direct_marker_status=group("direct_marker_status"),
            source_text_sample=unescaped(group("source")),
            stripped_text_sample=unescaped(group("stripped")),
            translated_text_sample=unescaped(group("translated")),
            final_text_sample=unescaped(group("final")),
        )
        parsed_fields, present_fields = _parse_structured_suffix(line, final_output_match.end())
        return _entry_with_structured_fields(entry, parsed_fields, present_fields)

    return None
