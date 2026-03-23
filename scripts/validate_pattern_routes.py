"""Validate route annotations in the message pattern dictionary."""

from __future__ import annotations

import argparse
import json
import re
import sys
from collections import Counter
from dataclasses import dataclass
from pathlib import Path

ALLOWED_ROUTES = (
    "message-frame",
    "popup",
    "journal",
    "leaf",
    "emit-message",
    "does-verb",
    "unclassified",
)
ALLOWED_ROUTE_SET = set(ALLOWED_ROUTES)
_MESSAGE_FRAME_CAPTURE_COUNT = 2
DEFAULT_PATTERN_FILE = (
    Path(__file__).resolve().parent.parent / "Mods" / "QudJP" / "Localization" / "Dictionaries" / "messages.ja.json"
)
_JOURNAL_MARKERS = (
    "^Notes: ",
    "Sultan Histories",
    "section of your journal",
    "On the (.+?) of",
    "You journeyed to ",
    "You discover the location of ",
    "You discovered the location of ",
    "You discovered the hidden village of ",
    "There exists a pocket dimension known as ",
    "There exists a dimension known as ",
    "Last visited on the ",
    "You visited the village of ",
    "You visited the historic site of ",
    "You became loved among ",
    "You recovered the historic relic, ",
    "You appeased a baetyl with ",
    "You stopped calling a location '",
    "You started calling a location '",
    "A baetyl demanding ",
    'A "SATED" baetyl',
    "You note this piece of information",
    "You note the location of ",
)


@dataclass(frozen=True)
class RouteValidationReport:
    """Validation summary for one pattern dictionary."""

    path: Path
    counts: dict[str, int]
    missing_routes: list[str]
    invalid_routes: list[str]


def classify_route(pattern: str) -> str:
    """Classify a pattern conservatively for Phase 1 route inventory work."""
    captures = re.compile(pattern).groups
    standard_verb_alternation = re.search(r"\(\?:([A-Za-z]+s)\|([A-Za-z]+)\)", pattern)
    primary_verb = standard_verb_alternation.group(2) if standard_verb_alternation else None

    route = "unclassified"
    if any(marker in pattern for marker in _JOURNAL_MARKERS):
        route = "journal"
    elif "not owned by you" in pattern:
        route = "popup"
    elif captures == 0:
        route = "leaf"
    elif pattern.startswith("^(?:The |the |[Aa]n? )?") and standard_verb_alternation:
        route = "does-verb"
        if primary_verb in {"hit", "fail"} and captures >= _MESSAGE_FRAME_CAPTURE_COUNT:
            route = "message-frame"
    elif pattern.startswith(("^You ", "^Your ", "^Something ")):
        route = "emit-message"

    return route


def validate_pattern_routes(path: Path) -> RouteValidationReport:
    """Validate route fields and summarize counts by allowed route."""
    raw = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(raw, dict):
        msg = f"Pattern file root is not an object: {path}"
        raise TypeError(msg)
    patterns = raw.get("patterns")
    if not isinstance(patterns, list):
        msg = f"Pattern file has no patterns array: {path}"
        raise TypeError(msg)

    counts: Counter[str] = Counter()
    missing_routes: list[str] = []
    invalid_routes: list[str] = []

    for index, entry in enumerate(patterns):
        if not isinstance(entry, dict):
            msg = f"patterns[{index}] is not an object"
            raise TypeError(msg)

        route = entry.get("route")
        pattern = entry.get("pattern", "<missing pattern>")
        if route is None or route == "":
            missing_routes.append(f"patterns[{index}] missing route for pattern: {pattern}")
            continue
        if not isinstance(route, str) or route not in ALLOWED_ROUTE_SET:
            invalid_routes.append(f"patterns[{index}] has invalid route '{route}' for pattern: {pattern}")
            continue

        counts[route] += 1

    ordered_counts = {route: counts.get(route, 0) for route in ALLOWED_ROUTES}
    return RouteValidationReport(
        path=path,
        counts=ordered_counts,
        missing_routes=missing_routes,
        invalid_routes=invalid_routes,
    )


def _print_report(report: RouteValidationReport) -> None:
    print(f"Pattern file: {report.path}")  # noqa: T201
    print("Route counts:")  # noqa: T201
    for route in ALLOWED_ROUTES:
        print(f"  {route}: {report.counts[route]}")  # noqa: T201

    if report.missing_routes:
        print(f"Missing route entries: {len(report.missing_routes)}")  # noqa: T201
        for issue in report.missing_routes:
            print(f"  ERROR: {issue}")  # noqa: T201

    if report.invalid_routes:
        print(f"Invalid route entries: {len(report.invalid_routes)}")  # noqa: T201
        for issue in report.invalid_routes:
            print(f"  ERROR: {issue}")  # noqa: T201

    if not report.missing_routes and not report.invalid_routes:
        print("All pattern routes are present and valid.")  # noqa: T201


def main(argv: list[str] | None = None) -> int:
    """Run the pattern route validator CLI."""
    parser = argparse.ArgumentParser(
        description="Validate route annotations in Mods/QudJP/Localization/Dictionaries/messages.ja.json.",
    )
    parser.add_argument(
        "path",
        nargs="?",
        type=Path,
        default=DEFAULT_PATTERN_FILE,
        help="Pattern dictionary path. Defaults to the repository messages.ja.json file.",
    )
    args = parser.parse_args(argv)

    try:
        report = validate_pattern_routes(args.path)
    except (FileNotFoundError, TypeError, json.JSONDecodeError) as exc:
        print(f"Error: {exc}", file=sys.stderr)  # noqa: T201
        return 1

    _print_report(report)
    if report.missing_routes or report.invalid_routes:
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
