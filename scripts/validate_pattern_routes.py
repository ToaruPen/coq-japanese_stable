"""Validate route annotations in the message pattern dictionary."""

from __future__ import annotations

import argparse
import json
import re
import sys
from collections import Counter
from dataclasses import dataclass
from pathlib import Path
from typing import Final

ALLOWED_ROUTES = (
    "message-frame",
    "popup",
    "journal",
    "leaf",
    "emit-message",
    "does-verb",
    "message-log",
    "description",
    "effect-cripple",
    "needs-harmony-patch",
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
NEEDS_HARMONY_PATCH_DEFERRAL_SOURCE: Final = "docs/superpowers/plans/2026-03-24-does-verb-manifest.md"
NEEDS_HARMONY_PATCH_DEFERRED_PATTERNS: Final = ()
NEEDS_HARMONY_PATCH_DEFERRAL_EVIDENCE: Final = dict.fromkeys(
    NEEDS_HARMONY_PATCH_DEFERRED_PATTERNS,
    NEEDS_HARMONY_PATCH_DEFERRAL_SOURCE,
)


@dataclass(frozen=True)
class RouteValidationReport:
    """Validation summary for one pattern dictionary."""

    path: Path
    counts: dict[str, int]
    missing_routes: list[str]
    invalid_routes: list[str]
    missing_needs_harmony_patch_deferrals: list[str]
    route_count_mismatches: list[str]

    @property
    def has_errors(self) -> bool:
        """Return true when any validation check failed."""
        return bool(
            self.missing_routes
            or self.invalid_routes
            or self.missing_needs_harmony_patch_deferrals
            or self.route_count_mismatches
        )


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


def validate_pattern_routes(
    path: Path,
    expected_counts: dict[str, int] | None = None,
    *,
    require_needs_harmony_patch_deferrals: bool = False,
) -> RouteValidationReport:
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
    missing_needs_harmony_patch_deferrals: list[str] = []

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
        if (
            require_needs_harmony_patch_deferrals
            and route == "needs-harmony-patch"
            and pattern not in NEEDS_HARMONY_PATCH_DEFERRAL_EVIDENCE
        ):
            missing_needs_harmony_patch_deferrals.append(
                f"patterns[{index}] needs-harmony-patch lacks explicit deferral evidence: {pattern}"
            )

    ordered_counts = {route: counts.get(route, 0) for route in ALLOWED_ROUTES}
    route_count_mismatches = []
    for route, expected_count in (expected_counts or {}).items():
        actual_count = ordered_counts.get(route)
        if actual_count != expected_count:
            route_count_mismatches.append(
                f"route '{route}' expected {expected_count} entries but found {actual_count}"
            )

    return RouteValidationReport(
        path=path,
        counts=ordered_counts,
        missing_routes=missing_routes,
        invalid_routes=invalid_routes,
        missing_needs_harmony_patch_deferrals=missing_needs_harmony_patch_deferrals,
        route_count_mismatches=route_count_mismatches,
    )


def _print_report(report: RouteValidationReport) -> None:
    print(f"Pattern file: {report.path}")  # noqa: T201
    print("Route counts:")  # noqa: T201
    for route in ALLOWED_ROUTES:
        print(f"  {route}: {report.counts[route]}")  # noqa: T201

    _print_issues("Missing route entries", report.missing_routes)
    _print_issues("Invalid route entries", report.invalid_routes)
    _print_issues("Missing needs-harmony-patch deferrals", report.missing_needs_harmony_patch_deferrals)
    _print_issues("Route count mismatches", report.route_count_mismatches)

    if not report.has_errors:
        print("All pattern routes are present and valid.")  # noqa: T201


def _print_issues(label: str, issues: list[str]) -> None:
    if not issues:
        return
    print(f"{label}: {len(issues)}")  # noqa: T201
    for issue in issues:
        print(f"  ERROR: {issue}")  # noqa: T201


def _parse_expected_count(value: str) -> tuple[str, int]:
    route, separator, raw_count = value.partition("=")
    if not separator:
        msg = f"expected count must use ROUTE=COUNT syntax: {value}"
        raise argparse.ArgumentTypeError(msg)
    if route not in ALLOWED_ROUTE_SET:
        msg = f"unknown route in expected count: {route}"
        raise argparse.ArgumentTypeError(msg)
    try:
        count = int(raw_count)
    except ValueError as exc:
        msg = f"expected count for {route} must be an integer: {raw_count}"
        raise argparse.ArgumentTypeError(msg) from exc
    if count < 0:
        msg = f"expected count for {route} must be non-negative: {count}"
        raise argparse.ArgumentTypeError(msg)
    return route, count


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
    parser.add_argument(
        "--expect-count",
        action="append",
        default=[],
        metavar="ROUTE=COUNT",
        type=_parse_expected_count,
        help="Require an exact route count. May be specified more than once.",
    )
    parser.add_argument(
        "--require-needs-harmony-patch-deferrals",
        action="store_true",
        help="Require every needs-harmony-patch row to be listed in the explicit deferral registry.",
    )
    args = parser.parse_args(argv)
    expected_counts: dict[str, int] = {}
    for route, count in args.expect_count:
        if route in expected_counts:
            parser.error(f"--expect-count for route '{route}' is duplicated")
        expected_counts[route] = count

    try:
        report = validate_pattern_routes(
            args.path,
            expected_counts,
            require_needs_harmony_patch_deferrals=args.require_needs_harmony_patch_deferrals,
        )
    except (FileNotFoundError, TypeError, json.JSONDecodeError) as exc:
        print(f"Error: {exc}", file=sys.stderr)  # noqa: T201
        return 1

    _print_report(report)
    if report.has_errors:
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
