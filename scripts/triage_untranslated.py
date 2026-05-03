"""Triage untranslated strings from Player.log into family classifications."""

from __future__ import annotations

import argparse
import json
import sys
from collections import defaultdict
from pathlib import Path
from typing import Any

if __package__ in {None, ""}:
    _PROJECT_ROOT = Path(__file__).resolve().parents[1]
    _PROJECT_ROOT_STR = str(_PROJECT_ROOT)
    if _PROJECT_ROOT_STR not in sys.path:
        sys.path.insert(0, _PROJECT_ROOT_STR)

from scripts.triage.classifier import classify
from scripts.triage.log_parser import parse_log
from scripts.triage.models import LogEntry, LogEntryKind, TriageClassification, TriageResult

_DEFAULT_LOG = Path.home() / "Library" / "Logs" / "Freehold Games" / "CavesOfQud" / "Player.log"
_ACTIONABLE_KINDS = frozenset({LogEntryKind.MISSING_KEY, LogEntryKind.NO_PATTERN})
_PHASE_F_ONLY_KINDS = frozenset(
    {
        LogEntryKind.DYNAMIC_TEXT_PROBE,
        LogEntryKind.SINK_OBSERVE,
        LogEntryKind.FINAL_OUTPUT_PROBE,
    },
)
_PHASE_F_STRUCTURED_FIELDS = (
    "template_id",
    "rendered_text_sample",
    "payload_mode",
    "payload_excerpt",
    "payload_sha256",
    "sink",
    "detail",
    "phase",
    "translation_status",
    "markup_status",
    "direct_marker_status",
    "source_text_sample",
    "stripped_text_sample",
    "translated_text_sample",
    "final_text_sample",
    "source_markup_spans",
    "final_markup_spans",
    "markup_span_status",
    "markup_semantic_status",
    "markup_semantic_flags",
    "source_visible_sha256",
    "final_visible_sha256",
)


def _find_project_root() -> Path:
    """Locate the project root by traversing up to find ``pyproject.toml``."""
    current = Path(__file__).resolve().parent
    while current != current.parent:
        if (current / "pyproject.toml").exists():
            return current
        current = current.parent
    msg = "Could not find project root (no pyproject.toml found)"
    raise FileNotFoundError(msg)


def _validate_log_path(log_path: Path) -> None:
    """Raise when the requested Player.log path does not exist."""
    if log_path.exists():
        return
    msg = f"Player.log not found: {log_path}"
    raise FileNotFoundError(msg)


def main(argv: list[str] | None = None) -> int:
    """Run the triage pipeline.

    Args:
        argv: Command-line arguments. Defaults to ``sys.argv[1:]``.

    Returns:
        Process exit code.
    """
    _find_project_root()

    parser = argparse.ArgumentParser(description="Triage untranslated strings from Player.log")
    parser.add_argument(
        "--log",
        type=Path,
        default=_DEFAULT_LOG,
        help=f"Path to Player.log (default: {_DEFAULT_LOG})",
    )
    parser.add_argument(
        "--output",
        type=Path,
        default=None,
        help="Output path for JSON report (default: stdout)",
    )
    args = parser.parse_args(argv)

    try:
        _validate_log_path(args.log)
        entries = parse_log(args.log)
        actionable_results = [classify(entry) for entry in _iter_actionable_entries(entries)]
        phase_f_results = [classify(entry) for entry in _iter_phase_f_entries(entries)]
        report = _build_report(actionable_results, phase_f_results)
        report_json = json.dumps(report, ensure_ascii=False, indent=2)
    except (FileNotFoundError, ValueError) as exc:
        print(f"Error: {exc}", file=sys.stderr)  # noqa: T201
        return 1

    if args.output is None:
        print(report_json)  # noqa: T201
        return 0

    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(report_json, encoding="utf-8")
    print(f"Triage report written to {args.output}", file=sys.stderr)  # noqa: T201
    return 0


def _iter_actionable_entries(entries: list[LogEntry]) -> list[LogEntry]:
    """Return only untranslated observations that should become triage items."""
    return [entry for entry in entries if entry.kind in _ACTIONABLE_KINDS]


def _iter_phase_f_entries(entries: list[LogEntry]) -> list[LogEntry]:
    """Return Phase F-only observations that must stay separate from actionable triage."""
    return [entry for entry in entries if entry.kind in _PHASE_F_ONLY_KINDS]


def _build_report(
    actionable_results: list[TriageResult],
    phase_f_results: list[TriageResult],
) -> dict[str, Any]:
    """Build a grouped JSON-serializable report from triage results."""
    summary: dict[str, int] = {"total": len(actionable_results)}
    by_classification: dict[str, list[dict[str, Any]]] = {
        classification.value: [] for classification in TriageClassification
    }
    by_route: dict[str, dict[str, list[dict[str, Any]]]] = defaultdict(
        lambda: {classification.value: [] for classification in TriageClassification},
    )

    for classification in TriageClassification:
        summary[classification.value] = sum(
            1 for result in actionable_results if result.classification == classification
        )

    for result in sorted(actionable_results, key=_result_sort_key):
        entry_data = _serialize_result(result)
        by_classification[result.classification.value].append(entry_data)
        by_route[result.entry.route][result.classification.value].append(entry_data)

    return {
        "summary": summary,
        "by_classification": by_classification,
        "by_route": dict(sorted(by_route.items())),
        "phase_f": _build_phase_f_report(phase_f_results),
    }


def _build_phase_f_report(results: list[TriageResult]) -> dict[str, Any]:
    """Build the separate Phase F report section for non-actionable runtime evidence."""
    summary = {
        "total": len(results),
        LogEntryKind.DYNAMIC_TEXT_PROBE.value: sum(
            1 for result in results if result.entry.kind == LogEntryKind.DYNAMIC_TEXT_PROBE
        ),
        LogEntryKind.SINK_OBSERVE.value: sum(1 for result in results if result.entry.kind == LogEntryKind.SINK_OBSERVE),
        LogEntryKind.FINAL_OUTPUT_PROBE.value: sum(
            1 for result in results if result.entry.kind == LogEntryKind.FINAL_OUTPUT_PROBE
        ),
        "markup_semantic_drift": sum(1 for result in results if result.entry.markup_semantic_status == "drift"),
    }
    entries = [
        _serialize_result(result, include_phase_f_runtime_fields=True)
        for result in sorted(results, key=_result_sort_key)
    ]
    return {"summary": summary, "entries": entries}


def _result_sort_key(result: TriageResult) -> tuple[str, int, str, str]:
    """Return a deterministic sort key for triage results."""
    return (
        result.entry.route,
        result.entry.line_number,
        result.classification.value,
        result.entry.text,
    )


def _serialize_result(
    result: TriageResult,
    *,
    include_phase_f_runtime_fields: bool = False,
) -> dict[str, Any]:
    """Serialize a single triage result into JSON-compatible data."""
    entry = result.entry
    payload: dict[str, Any] = {
        "text": entry.text,
        "route": entry.route,
        "kind": entry.kind.value,
        "line_number": entry.line_number,
        "reason": result.reason,
    }
    if entry.hits is not None:
        payload["hits"] = entry.hits
    if entry.family is not None:
        payload["family"] = entry.family
    if entry.translated_text is not None:
        payload["translated_text"] = entry.translated_text
    if entry.changed is not None:
        payload["changed"] = entry.changed
    if result.slot_evidence:
        payload["slot_evidence"] = result.slot_evidence
    phase_f = _serialize_phase_f_fields(entry, include_runtime_fields=include_phase_f_runtime_fields)
    if phase_f:
        payload["phase_f"] = phase_f
    return payload


def _serialize_phase_f_fields(
    entry: LogEntry,
    *,
    include_runtime_fields: bool,
) -> dict[str, Any]:
    """Serialize structured Phase F fields without mutating the legacy actionable shape."""
    payload: dict[str, Any] = {}

    if include_runtime_fields or entry.has_structured_phase_f_data():
        payload["route"] = entry.route
    if entry.family is not None and (include_runtime_fields or entry.has_structured_field("family")):
        payload["family"] = entry.family

    for field_name in _PHASE_F_STRUCTURED_FIELDS:
        if entry.has_structured_field(field_name):
            payload[field_name] = getattr(entry, field_name)

    return payload


if __name__ == "__main__":
    raise SystemExit(main())
